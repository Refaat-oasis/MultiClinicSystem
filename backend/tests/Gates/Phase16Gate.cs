using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase16Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate16_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase16Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
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
    public async Task Phase16IntegrationGate_SecurityHeadersExportAndAutomatedCrossTenantFuzzer()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Host = "app.clinic.localhost";
        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Host = "app.clinic.localhost";

        var (adminToken, _) = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Security Headers Middleware check across API & Production HSTS
        var healthResp = await clientA.GetAsync("/api/v1/health");
        healthResp.StatusCode.Should().Be(HttpStatusCode.OK);
        healthResp.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
        nosniff!.Should().Contain("nosniff");
        healthResp.Headers.TryGetValues("X-Frame-Options", out var xfo).Should().BeTrue();
        xfo!.Should().Contain("DENY");
        healthResp.Headers.TryGetValues("Referrer-Policy", out var refPol).Should().BeTrue();
        refPol!.Should().Contain("no-referrer");
        healthResp.Headers.TryGetValues("Permissions-Policy", out _).Should().BeTrue();
        healthResp.Headers.TryGetValues("Content-Security-Policy", out _).Should().BeTrue();
        healthResp.Headers.TryGetValues("Strict-Transport-Security", out var hsts).Should().BeTrue();
        hsts!.Should().Contain("max-age=31536000; includeSubDomains");
        healthResp.Headers.CacheControl?.NoStore.Should().BeTrue();

        // 2. Host boundary + unknown portal slug check
        var wrongHostPlatform = await clientA.GetAsync("/api/v1/platform/clinics");
        wrongHostPlatform.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var unknownPortal = await clientA.GetAsync("/api/v1/c/nonexistent-portal-slug/portal/info");
        unknownPortal.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Provision Owner A (Clinic A) & Owner B (Clinic B)
        string ownerAEmail = $"g16-ownera-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g16-ownerb-{Guid.NewGuid():N}@clinic.local";
        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner A Gate16",
            Email = ownerAEmail,
            MaxClinics = 2
        })).EnsureSuccessStatusCode();
        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner B Gate16",
            Email = ownerBEmail,
            MaxClinics = 2
        })).EnsureSuccessStatusCode();

        const string devA = "gate16-dev-a";
        const string devB = "gate16-dev-b";
        string tokenA = await ActivateAndLoginClinicUserAsync(clientA, ownerAEmail, "Gate16Pass#A", devA);
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        clientA.DefaultRequestHeaders.Add("X-Device-Id", devA);

        string tokenB = await ActivateAndLoginClinicUserAsync(clientB, ownerBEmail, "Gate16Pass#B", devB);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        clientB.DefaultRequestHeaders.Add("X-Device-Id", devB);

        Guid clinicA = (await (await clientA.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة بوابة ١٦ أ", "UTC", "EGP", "None", devA))).Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await OnboardClinicAsync(clientA, clinicA, $"g16-a-{clinicA:N}"[..16]);

        Guid clinicB = (await (await clientB.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة بوابة ١٦ ب", "UTC", "EGP", "None", devB))).Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await OnboardClinicAsync(clientB, clinicB, $"g16-b-{clinicB:N}"[..16]);

        string[] allModules =
        [
            "patients", "patient-history", "attachments", "scheduling", "queue", "clinical",
            "billing", "treatment-plans", "portal", "portal-booking", "inventory",
            "time-engine", "finance", "reports", "excel-import", "offline-sync"
        ];
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = allModules.Select(m => new ModuleToggleItemDto { ModuleKey = m, Enabled = true, Note = "Gate16" }).ToList()
        })).EnsureSuccessStatusCode();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await (await clientA.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/switch", new { })).Content.ReadFromJsonAsync<SwitchClinicViewModel>())!.AccessToken);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await (await clientB.PostAsJsonAsync($"/api/v1/clinics/{clinicB}/switch", new { })).Content.ReadFromJsonAsync<SwitchClinicViewModel>())!.AccessToken);

        // Seed Patient in Clinic A
        Guid patientAId = GuidV7.NewGuid();
        (await clientA.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/patients",
            new CreatePatientDto(patientAId, "مريض الفحص ١٦", "01077771616", null, null, 29, "Male", null, null, null, "dental", true, null)))
            .EnsureSuccessStatusCode();

        // 4. Online-only export check + ZIP export verification
        using var offlineReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/clinics/{clinicA}/export");
        offlineReq.Headers.Add("X-Offline-Sync", "true");
        var offlineResp = await clientA.SendAsync(offlineReq);
        offlineResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var exportResp = await clientA.GetAsync($"/api/v1/clinics/{clinicA}/export");
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK);
        byte[] zipBytes = await exportResp.Content.ReadAsByteArrayAsync();
        using (var ms = new MemoryStream(zipBytes))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            zip.Entries.Select(e => e.FullName).Should().Contain(["manifest.json", "patients.json", "clinic.json"]);
            using var reader = new StreamReader(zip.GetEntry("patients.json")!.Open(), Encoding.UTF8);
            string json = await reader.ReadToEndAsync();
            json.Should().Contain("مريض الفحص ١٦");
        }

        // 5. Automated Cross-Tenant Isolation Fuzzer: Owner B hitting ALL Clinic A routes -> MUST all return 404 NotFound
        string[] clinicScopedRoutes =
        [
            $"/api/v1/clinics/{clinicA}/rooms",
            $"/api/v1/clinics/{clinicA}/doctors",
            $"/api/v1/clinics/{clinicA}/working-hours",
            $"/api/v1/clinics/{clinicA}/days-off",
            $"/api/v1/clinics/{clinicA}/number-blocks",
            $"/api/v1/clinics/{clinicA}/patients",
            $"/api/v1/clinics/{clinicA}/scheduling/appointments",
            $"/api/v1/clinics/{clinicA}/scheduling/queue",
            $"/api/v1/clinics/{clinicA}/scheduling/waiting-list",
            $"/api/v1/clinics/{clinicA}/clinical/forms",
            $"/api/v1/clinics/{clinicA}/clinical/patients/{patientAId}/visits",
            $"/api/v1/clinics/{clinicA}/billing/price-list",
            $"/api/v1/clinics/{clinicA}/billing/charges",
            $"/api/v1/clinics/{clinicA}/billing/closing?date=2026-10-20",
            $"/api/v1/clinics/{clinicA}/treatment-plans",
            $"/api/v1/clinics/{clinicA}/portal/settings",
            $"/api/v1/clinics/{clinicA}/inventory/items",
            $"/api/v1/clinics/{clinicA}/time-engine/stats",
            $"/api/v1/clinics/{clinicA}/finance/fixed-costs",
            $"/api/v1/clinics/{clinicA}/reports/income-vs-expenses?fromDate=2026-10-01&toDate=2026-10-31",
            $"/api/v1/clinics/{clinicA}/import/targets",
            $"/api/v1/clinics/{clinicA}/import/history",
            $"/api/v1/clinics/{clinicA}/sync/settings",
            $"/api/v1/clinics/{clinicA}/sync/pull?cursor=0",
            $"/api/v1/clinics/{clinicA}/sync/conflicts",
            $"/api/v1/clinics/{clinicA}/sync/devices",
            $"/api/v1/clinics/{clinicA}/export",
            $"/api/v1/clinics/{clinicA}/roles",
            $"/api/v1/clinics/{clinicA}/staff",
            $"/api/v1/clinics/{clinicA}/devices",
            $"/api/v1/clinics/{clinicA}/audit",
            $"/api/v1/clinics/{clinicA}/dashboards/widgets",
            $"/api/v1/clinics/{clinicA}/dashboards/widgets/patients.summary"
        ];

        foreach (string route in clinicScopedRoutes)
        {
            HttpResponseMessage fuzzResp = await clientB.GetAsync(route);
            fuzzResp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"cross-tenant GET {route} MUST return 404 NotFound");
        }
    }

    private static async Task OnboardClinicAsync(HttpClient clinicClient, Guid clinicId, string slug)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("عيادة بوابة ١٦", "12 شارع التحرير", "Cairo", "Cairo", "01000000016", null, null, null))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/types",
            new OnboardingTypesDto([new SpecialtySelectionDto("dental", null, null)]))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/slug",
            new OnboardingSlugDto(slug))).EnsureSuccessStatusCode();
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/complete", null)).EnsureSuccessStatusCode();
    }

    private async Task<(string Token, string ManualSetupKey)> EnrollAndLoginSuperAdminAsync(HttpClient adminClient)
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

        return (enrolled!.AccessToken, firstBody.ManualSetupKey!);
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

        clinicClient.DefaultRequestHeaders.Authorization = null;
        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "Gate16Browser"
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

