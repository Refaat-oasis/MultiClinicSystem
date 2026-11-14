using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

public sealed class Phase3Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate3_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase3Gate(WebApplicationFactory<Program> factory)
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
    public async Task Gate_Phase3_EndToEnd_ClinicsOnboardingSettingsAndProfile()
    {
        // Step 1: Super Admin enrolls and creates Owner O1 (quota 3) + Owner O2
        using var adminClient = CreateClient(isAdminHost: true);
        using var clinicClient = CreateClient(isAdminHost: false);

        string adminJwt = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);

        string owner1Email = $"o1-{Guid.NewGuid():N}@clinic.local";
        string owner2Email = $"o2-{Guid.NewGuid():N}@clinic.local";

        var createO1Resp = await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "د. عمر أحمد",
            Email = owner1Email,
            Phone = "01011223344",
            MaxClinics = 3
        });
        createO1Resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var o1Summary = await createO1Resp.Content.ReadFromJsonAsync<OwnerSummaryViewModel>();
        o1Summary.Should().NotBeNull();
        o1Summary!.MaxClinics.Should().Be(3);

        var createO2Resp = await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "د. ليلى حسن",
            Email = owner2Email,
            Phone = "01055667788",
            MaxClinics = 2
        });
        createO2Resp.StatusCode.Should().Be(HttpStatusCode.Created);

        string o1Token = await ActivateAndLoginClinicUserAsync(clinicClient, owner1Email, "OwnerStrong#2026", "o1-device-1");
        string o2Token = await ActivateAndLoginClinicUserAsync(clinicClient, owner2Email, "OwnerStrong#2026", "o2-device-1");

        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o1Token);

        // O1 has no clinics initially, adds Clinic A -> blocked from dashboard/settings until onboarding finishes
        var initialList = await clinicClient.GetFromJsonAsync<List<MyClinicCardViewModel>>("/api/v1/clinics/mine");
        initialList.Should().BeEmpty();

        var addAResp = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة النور للأسنان والقلب", "Africa/Cairo", "EGP", "NearestPound", "o1-device-1"));
        addAResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var clinicA = await addAResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>();
        clinicA.Should().NotBeNull();
        clinicA!.Status.Should().Be("PendingOnboarding");

        var blockedDash = await clinicClient.GetAsync($"/api/v1/clinics/{clinicA.ClinicId}/dashboard");
        blockedDash.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blockedDash.Content.ReadAsStringAsync()).Should().Contain("ONBOARDING_REQUIRED");

        // Step 2: Complete Welcome -> Details + Logo -> Dental & Cardiology Specialties -> Slug -> Finish
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicA.ClinicId}/onboarding/welcome", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var logoResp = await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA.ClinicId}/onboarding/logo",
            new UploadLogoDto("logo.png", "image/png", Convert.ToBase64String(BuildCleanPng())));
        logoResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadedLogo = await logoResp.Content.ReadFromJsonAsync<LogoUploadViewModel>();

        var detailsResp = await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA.ClinicId}/onboarding/details",
            new OnboardingDetailsDto(
                "عيادة النور للأسنان والقلب",
                "12 شارع التحرير",
                "الدقي",
                "الجيزة",
                "01012345678",
                ["0223456789"],
                "noor@clinic.local",
                uploadedLogo!.AssetId));
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var typesResp = await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA.ClinicId}/onboarding/types",
            new OnboardingTypesDto([
                new SpecialtySelectionDto("dental", null, null),
                new SpecialtySelectionDto("cardiology", null, null)
            ]));
        typesResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var typesState = await typesResp.Content.ReadFromJsonAsync<OnboardingStateViewModel>();
        typesState!.Specialties.Should().Contain(s => s.CatalogCode == "dental" && s.HasDentalPlugin);
        typesState.Specialties.Should().Contain(s => s.CatalogCode == "cardiology" && !s.HasDentalPlugin);

        string slugA1 = $"noor-clinic-{Guid.NewGuid():N}"[..20];
        var slugCheck = await clinicClient.GetFromJsonAsync<SlugCheckViewModel>($"/api/v1/clinics/slug-check?slug={slugA1}&clinicId={clinicA.ClinicId}");
        slugCheck!.IsAvailable.Should().BeTrue();

        (await clinicClient.PutAsJsonAsync($"/api/v1/clinics/{clinicA.ClinicId}/onboarding/slug", new OnboardingSlugDto(slugA1))).StatusCode.Should().Be(HttpStatusCode.OK);

        var completeResp = await clinicClient.PostAsync($"/api/v1/clinics/{clinicA.ClinicId}/onboarding/complete", null);
        completeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashA = await completeResp.Content.ReadFromJsonAsync<ClinicDashboardViewModel>();
        dashA!.AwaitingModuleSetup.Should().BeTrue();
        dashA.StatusMessageEn.Should().Contain("Waiting for the platform admin to enable your features");

        // Step 3: O1 adds Clinic B and C; adding 4th is refused; switcher isolates clinics; O2 gets 404 on O1 clinics
        var addB = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة الشفاء ب", null, null, null, "o1-device-1"));
        var addC = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة الأمل ج", null, null, null, "o1-device-1"));
        string addCBody = await addC.Content.ReadAsStringAsync();
        addB.StatusCode.Should().Be(HttpStatusCode.Created);
        addC.StatusCode.Should().Be(HttpStatusCode.Created, because: $"Response body was: {addCBody}");
        var clinicB = await addB.Content.ReadFromJsonAsync<MyClinicCardViewModel>();

        var addD = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة رابعة", null, null, null, "o1-device-1"));
        addD.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await addD.Content.ReadAsStringAsync()).Should().Contain("CLINIC_QUOTA_REACHED");

        var switchToB = await clinicClient.PostAsync($"/api/v1/clinics/{clinicB!.ClinicId}/switch", null);
        switchToB.StatusCode.Should().Be(HttpStatusCode.OK);
        var switchBackA = await clinicClient.PostAsync($"/api/v1/clinics/{clinicA.ClinicId}/switch", null);
        switchBackA.StatusCode.Should().Be(HttpStatusCode.OK);

        using var o2Client = CreateClient(isAdminHost: false);
        o2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o2Token);
        var o2AccessA = await o2Client.GetAsync($"/api/v1/clinics/{clinicA.ClinicId}/dashboard");
        o2AccessA.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Step 4: Super Admin owner detail shows O1's 3 clinics with status and 0 modules; dashboard shows awaiting setup
        var o1Detail = await adminClient.GetFromJsonAsync<OwnerDetailViewModel>($"/api/v1/platform/owners/{o1Summary!.OwnerId}");
        o1Detail!.Clinics.Should().HaveCount(3);
        o1Detail.Clinics.Should().OnlyContain(c => c.ModulesEnabled.Count == 0);

        var platformDash = await adminClient.GetFromJsonAsync<PlatformDashboardViewModel>("/api/v1/platform/dashboard");
        platformDash!.ClinicsAwaitingModuleSetup.Should().BeGreaterOrEqualTo(3);

        // Step 5: Only O1 edits Clinic A settings; old slug keeps redirecting
        string updatedSlug = $"noor-v2-{Guid.NewGuid():N}"[..20];
        var editSettingsResp = await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA.ClinicId}/settings",
            new UpdateClinicSettingsDto(
                "عيادة النور المحدثة",
                "12 شارع التحرير",
                "الدقي",
                "الجيزة",
                "01012345678",
                null,
                "noor@clinic.local",
                updatedSlug,
                "Africa/Cairo",
                "EGP",
                "NearestPound",
                null));
        editSettingsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolveOld = await clinicClient.GetFromJsonAsync<PublicClinicResolutionViewModel>($"/api/v1/c/{slugA1}");
        resolveOld!.IsRedirect.Should().BeTrue();
        resolveOld.CurrentSlug.Should().Be(updatedSlug);
        resolveOld.RedirectUrl.Should().Be($"/c/{updatedSlug}");

        // Step 6: O1 changes email via code & changes password; old token rejected; new token works
        string newO1Email = $"o1-new-{Guid.NewGuid():N}@clinic.local";
        (await clinicClient.PostAsJsonAsync("/api/v1/profile/request-email-change", new RequestEmailChangeDto { NewEmail = newO1Email })).StatusCode.Should().Be(HttpStatusCode.OK);
        string emailChangeCode = await ExtractLatestCodeFromOutboxAsync(newO1Email);
        var confirmEmailResp = await clinicClient.PostAsJsonAsync("/api/v1/profile/confirm-email-change", new ConfirmEmailChangeDto { Code = emailChangeCode });
        confirmEmailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var emailTokens = await confirmEmailResp.Content.ReadFromJsonAsync<TokenResult>();

        // Old token o1Token is now rejected due to SecurityStamp rotation
        var staleClient = CreateClient(isAdminHost: false);
        staleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o1Token);
        var staleResp = await staleClient.GetAsync("/api/v1/profile");
        staleResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Use fresh token to change password and confirm old token again rejected
        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", emailTokens!.AccessToken);
        var changePwResp = await clinicClient.PostAsJsonAsync(
            "/api/v1/profile/change-password",
            new ChangePasswordDto("OwnerStrong#2026", "NewOwnerStrong#2026!", true, "o1-device-1"));
        changePwResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalTokens = await changePwResp.Content.ReadFromJsonAsync<TokenResult>();

        var staleEmailTokenClient = CreateClient(isAdminHost: false);
        staleEmailTokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", emailTokens.AccessToken);
        (await staleEmailTokenClient.GetAsync("/api/v1/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", finalTokens!.AccessToken);
        var finalProfile = await clinicClient.GetFromJsonAsync<UserProfileViewModel>("/api/v1/profile");
        finalProfile!.Email.Should().Be(newO1Email);
        finalProfile.Clinics.Should().HaveCount(3);
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

    private static byte[] BuildCleanPng()
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR", new byte[13]);
        WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01]);
        WriteChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)data.Length);
        stream.Write(lenBytes, 0, 4);
        stream.Write(Encoding.ASCII.GetBytes(type), 0, 4);
        stream.Write(data, 0, data.Length);
        stream.Write(new byte[4], 0, 4);
    }
}
