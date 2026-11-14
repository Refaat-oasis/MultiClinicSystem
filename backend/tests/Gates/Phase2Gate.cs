using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase2Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate2_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase2Gate(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
                if (dbDescriptor != null)
                {
                    services.Remove(dbDescriptor);
                }

                services.AddDbContext<CoreDbContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<TenantSessionInterceptor>();
                    options.UseSqlServer(GateConnectionString)
                           .AddInterceptors(interceptor);
                });

                var clockDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IClock));
                if (clockDescriptor != null)
                {
                    services.Remove(clockDescriptor);
                }

                services.AddSingleton<IClock>(_clock);
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await db.ApplyRowLevelSecurityAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateAdminClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Host-Type", "admin");
        client.DefaultRequestHeaders.Host = "admin.clinic.local";
        if (!string.IsNullOrEmpty(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private HttpClient CreateClinicClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Host = "app.clinic.local";
        if (!string.IsNullOrEmpty(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private async Task ResetPlatformAdminStateAsync()
    {
        _clock.SetTime(new DateTimeOffset(2026, 6, 15, 10, 15, 0, TimeSpan.Zero));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
    }

    private async Task<(PlatformAdminLoginViewModel FirstLogin, TotpEnrollmentCompletedViewModel Enrolled)> EnrollSeededAdminAsync(
        string newPassword = "SuperStrongPassword#2026")
    {
        await ResetPlatformAdminStateAsync();
        var adminClient = CreateAdminClient();
        var loginResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "AdminInitial#2026"
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstLogin = (await loginResp.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>())!;

        string code;
        using (var scope = _factory.Services.CreateScope())
        {
            var totp = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            code = totp.ComputeCurrentCode(firstLogin.ManualSetupKey!, _clock.UtcNow);
        }

        var enrolledClient = CreateAdminClient(firstLogin.AccessToken);
        var enrollResp = await enrolledClient.PostAsJsonAsync("/api/v1/platform/auth/enroll", new PlatformAdminEnrollDto
        {
            NewPassword = newPassword,
            HourlyCode = code
        });
        enrollResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var enrolled = (await enrollResp.Content.ReadFromJsonAsync<TotpEnrollmentCompletedViewModel>())!;
        return (firstLogin, enrolled);
    }

    [Fact]
    public async Task Gate_01_HostIsolation_AdminAndClinicRoutesReturn404OnWrongHost_AndCrossRealmTokensRejected()
    {
        var clinicClient = CreateClinicClient();
        var adminClient = CreateAdminClient();

        (await clinicClient.GetAsync("/platform/dashboard")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clinicClient.GetAsync("/api/v1/platform/dashboard")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await adminClient.GetAsync("/c/demo-clinic")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await adminClient.GetAsync("/api/v1/auth/access-logs")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await adminClient.GetAsync("/api/v1/clinics")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (_, enrolled) = await EnrollSeededAdminAsync();

        // Admin token rejected on clinic host
        var clinicWithAdminToken = CreateClinicClient(enrolled.AccessToken);
        (await clinicWithAdminToken.GetAsync("/api/v1/auth/access-logs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gate_02_SeededSuperAdmin_FirstLoginForcesEnrollment_AndCompletesEnrollmentWith10RecoveryKeys()
    {
        await ResetPlatformAdminStateAsync();
        var adminClient = CreateAdminClient();
        var firstResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "AdminInitial#2026"
        });

        firstResp.StatusCode.Should().Be(HttpStatusCode.OK);
        firstResp.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        string joinedCookies = string.Join(";", setCookies!);
        joinedCookies.Should().Contain("__Host-Platform-Token=");
        joinedCookies.ToLowerInvariant().Should().Contain("httponly");

        var firstBody = (await firstResp.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>())!;
        firstBody.RequiresEnrollment.Should().BeTrue();
        firstBody.MustChangePassword.Should().BeTrue();
        firstBody.ManualSetupKey.Should().NotBeNullOrWhiteSpace();
        firstBody.OtpAuthUri.Should().Contain("period=3600");

        // Before completing enrollment, calling full admin endpoints returns 403 Forbidden
        var preEnrollAdmin = CreateAdminClient(firstBody.AccessToken);
        (await preEnrollAdmin.GetAsync("/api/v1/platform/dashboard")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        string code;
        using (var scope = _factory.Services.CreateScope())
        {
            var totp = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            code = totp.ComputeCurrentCode(firstBody.ManualSetupKey!, _clock.UtcNow);
        }

        var enrollResp = await preEnrollAdmin.PostAsJsonAsync("/api/v1/platform/auth/enroll", new PlatformAdminEnrollDto
        {
            NewPassword = "SuperStrongPassword#2026",
            HourlyCode = code
        });

        enrollResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var enrolledBody = (await enrollResp.Content.ReadFromJsonAsync<TotpEnrollmentCompletedViewModel>())!;
        enrolledBody.RecoveryKeys.Should().HaveCount(10);
        enrolledBody.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Gate_03_HourlyTotp_RequiresCode_RefusesReplay_AllowsPreviousHourInFirst5Min_AndAcceptsSingleUseRecoveryKey()
    {
        var (firstLogin, enrolled) = await EnrollSeededAdminAsync();
        string secret = firstLogin.ManualSetupKey!;
        var adminClient = CreateAdminClient();

        // Missing TOTP fails
        var missingTotp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026"
        });
        missingTotp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Same hour replay of enrollment code refused
        string hour10Code;
        using (var scope = _factory.Services.CreateScope())
        {
            hour10Code = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>().ComputeCurrentCode(secret, _clock.UtcNow);
        }

        var replayResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026",
            HourlyCode = hour10Code
        });
        replayResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Jump to hour 12:03:00Z; hour 11 code is previous hour & inside first 5 minutes -> accepted
        var hour11 = _clock.UtcNow.AddHours(1);
        string hour11Code;
        using (var scope = _factory.Services.CreateScope())
        {
            hour11Code = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>().ComputeCurrentCode(secret, hour11);
        }

        _clock.SetTime(new DateTimeOffset(2026, 6, 15, 12, 3, 0, TimeSpan.Zero));
        var graceResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026",
            HourlyCode = hour11Code
        });
        graceResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Advance past 5 minutes of hour 13 -> hour 12 code rejected
        var hour12 = new DateTimeOffset(2026, 6, 15, 12, 15, 0, TimeSpan.Zero);
        string hour12Code;
        using (var scope = _factory.Services.CreateScope())
        {
            hour12Code = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>().ComputeCurrentCode(secret, hour12);
        }

        _clock.SetTime(new DateTimeOffset(2026, 6, 15, 13, 5, 10, TimeSpan.Zero));
        var expiredGraceResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026",
            HourlyCode = hour12Code
        });
        expiredGraceResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Single-use recovery key succeeds once and is rejected on reuse
        string recoveryKey = enrolled.RecoveryKeys[0];
        var recoveryOk = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026",
            RecoveryKey = recoveryKey
        });
        recoveryOk.StatusCode.Should().Be(HttpStatusCode.OK);

        var recoveryReuse = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026",
            RecoveryKey = recoveryKey
        });
        recoveryReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gate_04_OwnerLifecycle_CreateOwner_SendsInviteEmail_OwnerActivatesOnClinicHost_AndDeactivationBlocksLogin()
    {
        var (_, enrolled) = await EnrollSeededAdminAsync();
        var adminClient = CreateAdminClient(enrolled.AccessToken);

        string ownerEmail = $"o1_{Guid.NewGuid():N}@clinic.local";
        var createResp = await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Dr. Tarek Owner",
            Email = ownerEmail,
            Phone = "+201000000001",
            MaxClinics = 3
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var ownerSummary = (await createResp.Content.ReadFromJsonAsync<OwnerSummaryViewModel>())!;
        ownerSummary.MaxClinics.Should().Be(3);

        // Phase 1 outbox / fake email sender receives activation code
        var emailSender = _factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender;
        var inviteEmail = emailSender!.SentMessages.Last(m => m.To == ownerEmail);
        string activationCode = Regex.Match(inviteEmail.Body, @"\b\d{6}\b").Value;
        activationCode.Should().HaveLength(6);

        // Owner activates on Clinic host & cannot login on Admin host
        var clinicClient = CreateClinicClient();
        var activateResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = ownerEmail,
            Code = activationCode,
            Password = "OwnerStrongPassword#2026"
        });
        activateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerLoginClinic = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = ownerEmail,
            Password = "OwnerStrongPassword#2026",
            DeviceId = "owner-laptop-1",
            DeviceName = "Chrome"
        });
        ownerLoginClinic.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerAuth = (await ownerLoginClinic.Content.ReadFromJsonAsync<AuthResponseViewModel>())!;
        (await CreateAdminClient(ownerAuth.AccessToken).GetAsync("/api/v1/platform/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Deactivate Owner with step-up password
        var deactivateResp = await adminClient.PostAsJsonAsync($"/api/v1/platform/owners/{ownerSummary.OwnerId}/deactivate", new DeactivateOwnerDto
        {
            StepUpPassword = "SuperStrongPassword#2026",
            Reason = "Compliance audit"
        });
        deactivateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Login blocked after deactivation
        var loginAfterDeactivation = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = ownerEmail,
            Password = "OwnerStrongPassword#2026",
            DeviceId = "owner-laptop-1"
        });
        loginAfterDeactivation.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gate_05_OwnerDetailAndPlatformDashboard_ReturnAggregatesAndMonitoringMetrics()
    {
        var (_, enrolled) = await EnrollSeededAdminAsync();
        var adminClient = CreateAdminClient(enrolled.AccessToken);

        string ownerEmail = $"dash_owner_{Guid.NewGuid():N}@clinic.local";
        var createResp = await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Dashboard Owner",
            Email = ownerEmail,
            MaxClinics = 5
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var owner = (await createResp.Content.ReadFromJsonAsync<OwnerSummaryViewModel>())!;

        var detailResp = await adminClient.GetAsync($"/api/v1/platform/owners/{owner.OwnerId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await detailResp.Content.ReadFromJsonAsync<OwnerDetailViewModel>())!;
        detail.OwnerId.Should().Be(owner.OwnerId);
        detail.MaxClinics.Should().Be(5);

        var dashboardResp = await adminClient.GetAsync("/api/v1/platform/dashboard");
        dashboardResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = (await dashboardResp.Content.ReadFromJsonAsync<PlatformDashboardViewModel>())!;
        dashboard.TotalOwners.Should().BeGreaterThanOrEqualTo(1);
        dashboard.RecentAuditEntries.Should().NotBeNull();
    }

    [Fact]
    public async Task Gate_06_BreakGlassReset_ResetsTwoFactorEnrollment_AndWritesAuditRow()
    {
        var (_, enrolled) = await EnrollSeededAdminAsync();
        var anonAdminClient = CreateAdminClient();

        var resetResp = await anonAdminClient.PostAsJsonAsync("/api/v1/platform/auth/break-glass-reset", new BreakGlassResetDto
        {
            Email = "ahmed@platform.local",
            BreakGlassSecret = "dev-break-glass-secret-key-2026"
        });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin must enroll again with new password after break-glass reset
        var reLoginResp = await anonAdminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperStrongPassword#2026"
        });
        reLoginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var reLoginBody = (await reLoginResp.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>())!;
        reLoginBody.RequiresEnrollment.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        bool auditFound = await db.AuditLogs.AnyAsync(a => a.Action == "PlatformAdminBreakGlassReset2FA");
        auditFound.Should().BeTrue();
    }

    [Fact]
    public async Task Gate_07_InspectClinicAndMoveOwner_WritesPatientAccessLog_OwnerReadsAccessLog_RefusesInspectWrites_AndMoveTransfersClinicWithStepUp()
    {
        var (_, enrolled) = await EnrollSeededAdminAsync();
        var adminClient = CreateAdminClient(enrolled.AccessToken);

        string o1Email = $"o1_move_{Guid.NewGuid():N}@clinic.local";
        string o2Email = $"o2_move_{Guid.NewGuid():N}@clinic.local";
        var o1 = (await (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner One",
            Email = o1Email,
            MaxClinics = 3
        })).Content.ReadFromJsonAsync<OwnerSummaryViewModel>())!;

        var o2 = (await (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner Two",
            Email = o2Email,
            MaxClinics = 3
        })).Content.ReadFromJsonAsync<OwnerSummaryViewModel>())!;

        // Activate Owner One so they can check PatientAccessLogs via clinic host
        var emailSender = _factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender;
        string code1 = Regex.Match(emailSender!.SentMessages.Last(m => m.To == o1Email).Body, @"\b\d{6}\b").Value;
        var clinicAnon = CreateClinicClient();
        (await clinicAnon.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = o1Email,
            Code = code1,
            Password = "OwnerOnePassword#2026"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var o1Login = (await (await clinicAnon.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = o1Email,
            Password = "OwnerOnePassword#2026",
            DeviceId = "o1-device"
        })).Content.ReadFromJsonAsync<AuthResponseViewModel>())!;

        // Seed clinic owned by O1
        var clinicId = GuidV7.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var clinic = new Clinic(clinicId, "Alexandria Dental", $"alex-{Guid.NewGuid():N}", _clock.UtcNow, o1.OwnerId);
            clinic.Activate();
            db.Clinics.Add(clinic);
            db.UserClinics.Add(new UserClinic(GuidV7.NewGuid(), o1.UserId, clinicId, "Owner", _clock.UtcNow));
            await db.SaveChangesAsync();
        }

        // 1. Inspect clinic succeeds read-only and logs PatientAccessLog
        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicId}/inspect");
        inspectResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var inspectBody = (await inspectResp.Content.ReadFromJsonAsync<InspectClinicViewModel>())!;
        inspectBody.ReadOnlyMode.Should().BeTrue();
        inspectBody.ClinicId.Should().Be(clinicId);

        // 2. Inspect mode refuses writes (403)
        var writeInInspect = await adminClient.PostAsJsonAsync($"/api/v1/platform/clinics/{clinicId}/inspect/patients", new { Name = "Blocked" });
        writeInInspect.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Owner O1 can read the PatientAccessLog entry on Clinic host
        var o1AuthedClient = CreateClinicClient(o1Login.AccessToken);
        var ownerLogsResp = await o1AuthedClient.GetAsync($"/api/v1/auth/access-logs?clinicId={clinicId}");
        ownerLogsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerLogs = (await ownerLogsResp.Content.ReadFromJsonAsync<List<PatientAccessLogItemViewModel>>())!;
        ownerLogs.Should().Contain(l => l.ClinicId == clinicId && l.Details != null && l.Details.Contains("ActorType:SuperAdmin"));

        // 4. Advance beyond 10-min step-up window -> move fails without step-up, succeeds with step-up, removes O1 mapping
        _clock.Advance(TimeSpan.FromMinutes(11));
        var moveFail = await adminClient.PostAsJsonAsync($"/api/v1/platform/clinics/{clinicId}/move", new MoveClinicDto
        {
            TargetOwnerId = o2.OwnerId
        });
        moveFail.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var moveOk = await adminClient.PostAsJsonAsync($"/api/v1/platform/clinics/{clinicId}/move", new MoveClinicDto
        {
            TargetOwnerId = o2.OwnerId,
            StepUpPassword = "SuperStrongPassword#2026"
        });
        moveOk.StatusCode.Should().Be(HttpStatusCode.OK);

        // After move, O1 no longer has access to clinic logs -> 404 NotFound (cross-clinic/non-owned clinic isolation rule)
        var o1LogsAfterMove = await o1AuthedClient.GetAsync($"/api/v1/auth/access-logs?clinicId={clinicId}");
        o1LogsAfterMove.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
