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

public sealed class Phase13Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate13_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase13Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero));
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
    public async Task Phase13_IntegrationGate_MinSamplesOutlierTrimStepLimitExcusedToggleSchedulingWidgetsAndInspect()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        (string adminToken, string manualKey) = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerEmail = $"g13-owner-{Guid.NewGuid():N}@clinic.local";
        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G13",
            Email = ownerEmail,
            MaxClinics = 3
        })).EnsureSuccessStatusCode();

        using var clinicClient = _factory.CreateClient();
        clinicClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerToken = await ActivateAndLoginClinicUserAsync(clinicClient, ownerEmail, "OwnerPass#2026", "dev-owner-g13");
        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        clinicClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-g13");

        var createARes = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Gate 13 Clinic", "UTC", "EGP", "None", "dev-owner-g13"));
        createARes.EnsureSuccessStatusCode();
        Guid clinicA = (await createARes.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await CompleteOnboardingAsync(clinicClient, clinicA, $"g13-a-{clinicA:N}"[..16], ["dental"]);

        var createBRes = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Gate 13 Off Clinic", "UTC", "EGP", "None", "dev-owner-g13"));
        createBRes.EnsureSuccessStatusCode();
        Guid clinicB = (await createBRes.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await CompleteOnboardingAsync(clinicClient, clinicB, $"g13-b-{clinicB:N}"[..16], ["dental"]);

        // Clinic A: time-engine ON; Clinic B: time-engine OFF
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true, Note = "Gate 13" },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true, Note = "Gate 13" },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true, Note = "Gate 13" },
                new ModuleToggleItemDto { ModuleKey = "time-engine", Enabled = true, Note = "Gate 13" }
            ]
        })).EnsureSuccessStatusCode();

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicB}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true, Note = "Gate 13 Off" },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true, Note = "Gate 13 Off" },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true, Note = "Gate 13 Off" }
            ]
        })).EnsureSuccessStatusCode();

        var roomA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/rooms",
            new CreateClinicRoomDto { Code = "G13-R1", NameAr = "غرفة 1", NameEn = "Room 1", SpecialtyCode = "dental" }))
            .Content.ReadFromJsonAsync<ClinicRoomViewModel>();
        var docA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/doctors",
            new CreateClinicDoctorDto { FullName = "د. كريم نبيل", TitleAr = "أخصائي", TitleEn = "Specialist", SpecialtyCodes = ["dental"], AssignedRoomIds = [roomA!.Id], SlotDurationMinutes = 30 }))
            .Content.ReadFromJsonAsync<ClinicDoctorViewModel>();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/working-hours",
            new SaveWorkingHoursDto
            {
                DoctorId = docA!.Id,
                Items = [new WorkingHourItemDto { DayOfWeek = (int)DayOfWeek.Monday, StartTime = "09:00", EndTime = "17:00", IsClosed = false }]
            })).EnsureSuccessStatusCode();

        var catA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/scheduling/categories",
            new UpsertAppointmentCategoryDto { SpecialtyCode = "dental", NameAr = "حشو", NameEn = "Filling", DefaultDurationMinutes = 30, DefaultPriceAmount = 350m, BookableOnline = true }))
            .Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();
        var patA = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/patients",
            new CreatePatientDto(null, "مصطفى كامل", "01077880001", null, new DateOnly(1990, 8, 1), null, "Male", null, null, null, "dental", false, null)))
            .Content.ReadFromJsonAsync<PatientDetailViewModel>();

        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/time-engine/settings",
            new UpdateClinicTimeEngineSettingDto { MinSampleSize = 5, TrimFraction = 0.15m, MaxStepChangePercent = 20m, OperationId = GuidV7.NewGuid() }))
            .EnsureSuccessStatusCode();

        // Complete 4 sessions of 45m + 1 extreme 190m + 1 excused 120m
        foreach (int mins in new[] { 45, 45, 45, 45, 190 })
        {
            var t0 = _clock.UtcNow;
            var visit = await (await clinicClient.PostAsJsonAsync(
                $"/api/v1/clinics/{clinicA}/clinical/visits",
                new StartVisitDto { PatientId = patA!.Id, DoctorId = docA.Id, SpecialtyCode = "dental", RoomId = roomA.Id, PlannedDurationMinutes = 30, CategoryId = catA!.Id }))
                .Content.ReadFromJsonAsync<VisitSessionViewModel>();
            _clock.SetTime(t0.AddMinutes(mins));
            await ReLoginClinicAsync(clinicClient, ownerEmail, "OwnerPass#2026", "dev-owner-g13");
            (await clinicClient.PostAsJsonAsync(
                $"/api/v1/clinics/{clinicA}/clinical/visits/{visit!.Id}/complete",
                new CompleteVisitDto { OperationId = GuidV7.NewGuid() })).EnsureSuccessStatusCode();
        }

        // 1 excused session at completion -> excluded from sample count and trimmed avg
        var excusedT0 = _clock.UtcNow;
        var excusedVisit = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/clinical/visits",
            new StartVisitDto { PatientId = patA!.Id, DoctorId = docA.Id, SpecialtyCode = "dental", RoomId = roomA.Id, PlannedDurationMinutes = 30, CategoryId = catA!.Id }))
            .Content.ReadFromJsonAsync<VisitSessionViewModel>();
        _clock.SetTime(excusedT0.AddMinutes(120));
        await ReLoginClinicAsync(clinicClient, ownerEmail, "OwnerPass#2026", "dev-owner-g13");
        (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/clinical/visits/{excusedVisit!.Id}/complete",
            new CompleteVisitDto { IsOvertimeExcused = true, OvertimeExcuseReason = "Surgical complexity", OperationId = GuidV7.NewGuid() })).EnsureSuccessStatusCode();

        // Flush outbox + call recalc
        using (var scope = _factory.Services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
            await outbox.ProcessPendingMessagesAsync();
            await outbox.ProcessPendingMessagesAsync();
        }

        var stats = await clinicClient.GetFromJsonAsync<IReadOnlyList<CategoryTimeStatViewModel>>(
            $"/api/v1/clinics/{clinicA}/time-engine/stats?doctorId={docA.Id}&categoryId={catA.Id}");
        stats!.Should().ContainSingle();
        stats[0].SampleCount.Should().Be(5);
        stats[0].ExcusedCount.Should().Be(1);
        stats[0].TrimmedAverageMinutes.Should().Be(45m); // Trimmed 45 & 190 -> (45,45,45) = 45
        stats[0].AppliedDurationMinutes.Should().Be(36);  // 30m + max 20% = 36m

        // Booking with EndUtc == null + free-slots reflect 36m
        var futureMonday = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        var bookedOn = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/scheduling/appointments",
            new BookAppointmentDto { PatientId = patA.Id, DoctorId = docA.Id, RoomId = roomA.Id, CategoryId = catA.Id, StartUtc = futureMonday }))
            .Content.ReadFromJsonAsync<AppointmentItemViewModel>();
        (bookedOn!.EndUtc - bookedOn.StartUtc).TotalMinutes.Should().Be(36);

        var freeSlotsOn = await clinicClient.GetFromJsonAsync<IReadOnlyList<FreeSlotItemViewModel>>(
            $"/api/v1/clinics/{clinicA}/scheduling/free-slots?categoryId={catA.Id}&doctorId={docA.Id}&fromUtc={Uri.EscapeDataString(futureMonday.Date.ToString("O"))}&toUtc={Uri.EscapeDataString(futureMonday.Date.AddDays(1).ToString("O"))}");
        freeSlotsOn!.Should().NotBeEmpty();
        freeSlotsOn.All(s => s.DurationMinutes == 36).Should().BeTrue();

        // Doctor OFF -> reverts to 30m
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/time-engine/doctors/{docA.Id}/enabled",
            new SetDoctorTimeEngineDto { IsEngineEnabled = false, OperationId = GuidV7.NewGuid() })).EnsureSuccessStatusCode();

        var freeSlotsOff = await clinicClient.GetFromJsonAsync<IReadOnlyList<FreeSlotItemViewModel>>(
            $"/api/v1/clinics/{clinicA}/scheduling/free-slots?categoryId={catA.Id}&doctorId={docA.Id}&fromUtc={Uri.EscapeDataString(futureMonday.Date.ToString("O"))}&toUtc={Uri.EscapeDataString(futureMonday.Date.AddDays(1).ToString("O"))}");
        freeSlotsOff!.All(s => s.DurationMinutes == 30).Should().BeTrue();

        // Turn back ON + toggle excused on a record via API + inspect + widgets + Clinic B module off
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/time-engine/doctors/{docA.Id}/enabled",
            new SetDoctorTimeEngineDto { IsEngineEnabled = true, OperationId = GuidV7.NewGuid() })).EnsureSuccessStatusCode();

        var wSamples = await clinicClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicA}/dashboards/widgets/time-engine.sample-counts");
        wSamples!.PrimaryValue.Should().Be(5m);
        wSamples.Breakdown["excusedSampleCount"].Should().Be(1m);

        await ReLoginAdminAsync(adminClient, manualKey);
        var inspect = await adminClient.GetFromJsonAsync<ClinicTimeEngineInspectViewModel>(
            $"/api/v1/platform/clinics/{clinicA}/inspect/time-engine");
        inspect!.Stats.Should().ContainSingle();

        // Clinic B (time-engine OFF): endpoint returns 403 MODULE_DISABLED
        var offResp = await clinicClient.GetAsync($"/api/v1/clinics/{clinicB}/time-engine/stats");
        offResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task CompleteOnboardingAsync(HttpClient clinicClient, Guid clinicId, string slug, string[] specialties)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("Gate 13 Clinic", "10 Nile St", "Maadi", "Cairo", "01001112233", null, null, null))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/types",
            new OnboardingTypesDto(specialties.Select(s => new SpecialtySelectionDto(s, null, null)).ToList()))).EnsureSuccessStatusCode();
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
            DeviceName = "Gate13Browser"
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

    private static async Task ReLoginClinicAsync(HttpClient clinicClient, string email, string password, string deviceId)
    {
        clinicClient.DefaultRequestHeaders.Authorization = null;
        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "Gate13Browser"
        });
        loginResp.EnsureSuccessStatusCode();
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authBody!.AccessToken);
    }

    private async Task ReLoginAdminAsync(HttpClient adminClient, string manualSetupKey)
    {
        string totpCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var totpService = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            totpCode = totpService.ComputeCurrentCode(manualSetupKey, _clock.UtcNow);
        }

        adminClient.DefaultRequestHeaders.Authorization = null;
        var resp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "SuperAdminGate#2026",
            HourlyCode = totpCode
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }
}
