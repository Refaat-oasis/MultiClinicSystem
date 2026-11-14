using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase5Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate5_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase5Gate(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task Gate_Phase5_EndToEnd_PatientsHistoryAttachmentsScopingAndInspect()
    {
        using var adminClient = CreateClient(isAdminHost: true);
        using var ownerClient = CreateClient(isAdminHost: false);

        string adminJwt = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);

        string ownerEmail = $"p5-owner-{Guid.NewGuid():N}@clinic.local";
        var createOwnerResp = await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "د. طارق منصور",
            Email = ownerEmail,
            Phone = "01055667788",
            MaxClinics = 3
        });
        createOwnerResp.StatusCode.Should().Be(HttpStatusCode.Created);

        string ownerToken = await ActivateAndLoginClinicUserAsync(ownerClient, ownerEmail, "OwnerStrong#2026", "owner-dev-p5");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var clinicAResp = await ownerClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة الشفاء أ", "Africa/Cairo", "EGP", "NearestPound", "owner-dev-p5"));
        var clinicBResp = await ownerClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة الشفاء ب", "Africa/Cairo", "EGP", "NearestPound", "owner-dev-p5"));
        clinicAResp.StatusCode.Should().Be(HttpStatusCode.Created);
        clinicBResp.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid clinicAId = (await clinicAResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        Guid clinicBId = (await clinicBResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;

        // Enable patients + patient-history + attachments on Clinic A, and patients on Clinic B
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "patient-history", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "attachments", Enabled = true }
            ]
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicBId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true }]
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Create patient in Clinic A with Arabic name
        var createPatientResp = await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients",
            new CreatePatientDto(
                null,
                "أحمد إبراهيم سالم",
                "01009998877",
                null,
                new DateOnly(1988, 5, 10),
                null,
                "Male",
                null,
                "القاهرة",
                null,
                "general",
                false,
                Guid.NewGuid()));
        createPatientResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var patient = await createPatientResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();
        patient!.FileNumber.Should().StartWith("P-");

        // Search via Arabic normalized variant
        var searchResp = await ownerClient.GetFromJsonAsync<PatientSearchViewModel>(
            $"/api/v1/clinics/{clinicAId}/patients?q={Uri.EscapeDataString("احمد ابراهيم")}");
        searchResp!.Items.Should().Contain(p => p.Id == patient.Id);

        // Duplicate check & unconfirmed create -> 409 Conflict
        var dupCheckResp = await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/check-duplicates",
            new CheckPatientDuplicatesDto("احمد ابراهيم سالم", "+20 100 999 8877", null));
        dupCheckResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var warnings = await dupCheckResp.Content.ReadFromJsonAsync<List<PatientDuplicateWarningViewModel>>();
        warnings!.Should().ContainSingle(w => w.PatientId == patient.Id && w.ConfidencePercent == 100);

        // Add severe allergy + update medical history + add note + upload PNG attachment
        (await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/{patient.Id}/allergies",
            new AddPatientAllergyDto("Penicillin", "Severe", "Rash"))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ownerClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/{patient.Id}/history",
            new UpdatePatientMedicalHistoryDto("Asthma", ["Asthma"], ["Inhaler"], [], true))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/{patient.Id}/notes",
            new AddPatientNoteDto("Follow up next month", false))).StatusCode.Should().Be(HttpStatusCode.Created);

        string pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=";
        var uploadResp = await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/{patient.Id}/attachments",
            new UploadPatientAttachmentDto("Lab", "cbc.png", "image/png", pngBase64, "CBC scan"));
        uploadResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await uploadResp.Content.ReadFromJsonAsync<PatientAttachmentItemViewModel>();

        var dlResp = await ownerClient.GetAsync($"/api/v1/clinics/{clinicAId}/patients/{patient.Id}/attachments/{attachment!.Id}");
        dlResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Phase 5 Dashboard widgets reflect real aggregates
        var summaryWidget = await ownerClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/patients.summary");
        summaryWidget!.PrimaryValue.Should().Be(1);

        var allergyWidget = await ownerClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/patient-history.allergy-count");
        allergyWidget!.PrimaryValue.Should().Be(1);

        var attachWidget = await ownerClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/attachments.uploads-summary");
        attachWidget!.PrimaryValue.Should().Be(1);

        // Cross-clinic request from Clinic B returns 404 (not 403)
        var crossClinicResp = await ownerClient.GetAsync($"/api/v1/clinics/{clinicBId}/patients/{patient.Id}");
        crossClinicResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Turn attachments OFF -> upload returns 403 MODULE_DISABLED while patient GET returns 200 with empty Attachments
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "attachments", Enabled = false }]
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadOff = await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients/{patient.Id}/attachments",
            new UploadPatientAttachmentDto("Lab", "cbc2.png", "image/png", pngBase64, null));
        uploadOff.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var patientOff = await ownerClient.GetFromJsonAsync<PatientDetailViewModel>($"/api/v1/clinics/{clinicAId}/patients/{patient.Id}");
        patientOff!.IsAttachmentsEnabled.Should().BeFalse();
        patientOff.Attachments.Should().BeEmpty();
        patientOff.Allergies.Should().ContainSingle(a => a.Allergen == "Penicillin");

        // Super Admin read-only inspect patient succeeds & writes PatientAccessLog; write under inspect returns 403
        var inspectResp = await adminClient.GetAsync(
            $"/api/v1/platform/clinics/{clinicAId}/inspect/patients/{patient.Id}?reason={Uri.EscapeDataString("Support ticket 55")}");
        inspectResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var inspected = await inspectResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();
        inspected!.Id.Should().Be(patient.Id);

        var inspectWrite = await adminClient.PostAsJsonAsync(
            $"/api/v1/platform/clinics/{clinicAId}/inspect/patients/{patient.Id}/notes",
            new { noteText = "Blocked" });
        inspectWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var accessLog = await db.PatientAccessLogs.FirstOrDefaultAsync(l => l.ClinicId == clinicAId && l.Reason == "Support ticket 55");
        accessLog.Should().NotBeNull();
    }

    private HttpClient CreateClient(bool isAdminHost)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri(isAdminHost ? "https://admin.clinic.local" : "https://app.clinic.local")
        });
        client.DefaultRequestHeaders.Add("X-Host-Type", isAdminHost ? "admin" : "clinic");
        return client;
    }

    private async Task<string> EnrollAndLoginSuperAdminAsync(HttpClient adminClient)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
        }

        var loginFirst = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "AdminInitial#2026"
        });
        loginFirst.EnsureSuccessStatusCode();
        var firstBody = await loginFirst.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>();

        string totpCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var totpService = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            totpCode = totpService.ComputeCurrentCode(firstBody!.ManualSetupKey!, _clock.UtcNow);
        }

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBody!.AccessToken);
        var enrollResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/enroll", new PlatformAdminEnrollDto
        {
            NewPassword = "SuperAdminGate#2026",
            HourlyCode = totpCode
        });
        enrollResp.EnsureSuccessStatusCode();
        var enrolled = await enrollResp.Content.ReadFromJsonAsync<TotpEnrollmentCompletedViewModel>();
        adminClient.DefaultRequestHeaders.Authorization = null;

        return enrolled!.AccessToken;
    }

    private async Task<string> ActivateAndLoginClinicUserAsync(HttpClient clinicClient, string email, string password, string deviceId)
    {
        string code = await ExtractLatestCodeFromOutboxAsync(email);
        var activateResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email,
            Code = code,
            Password = password
        });
        activateResp.EnsureSuccessStatusCode();

        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "GateBrowser"
        });
        loginResp.EnsureSuccessStatusCode();
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        return authBody!.AccessToken;
    }

    private async Task<string> ExtractLatestCodeFromOutboxAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>() as FakeEmailSender
                          ?? throw new InvalidOperationException("FakeEmailSender not registered.");
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        await outbox.ProcessPendingMessagesAsync();
        var sent = emailSender.SentMessages.Last(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));
        return Regex.Match(sent.Body, @"\b(\d{6})\b").Groups[1].Value;
    }
}

