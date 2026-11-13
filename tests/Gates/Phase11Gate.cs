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

public sealed class Phase11Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate11_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase11Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero));
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
    public async Task Phase11_IntegrationGate_PortalSelfBookingApprovalsLimitsCutoffNoShowsWaitlistAndModules()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        (string adminToken, string adminTotpKey) = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerAEmail = $"g11-owner-a-{Guid.NewGuid():N}@clinic.local";
        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G11",
            Email = ownerAEmail,
            MaxClinics = 3
        });

        using var clinicClient = _factory.CreateClient();
        clinicClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerToken = await ActivateAndLoginClinicUserAsync(clinicClient, ownerAEmail, "OwnerPass#2026", "dev-owner-g11");
        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        clinicClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-g11");

        var createClinic = await clinicClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Elite Gate 11", "UTC", "EGP", "None", "dev-owner-g11"));
        Guid clinicId = (await createClinic.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await CompleteOnboardingAsync(clinicClient, clinicId, "elite-p11", ["dental"]);

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "queue", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "portal", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "portal-booking", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        // Setup working hours, room, doctor, category
        (await clinicClient.PutAsJsonAsync($"/api/v1/clinics/{clinicId}/working-hours", new SaveWorkingHoursDto
        {
            DoctorId = null,
            Items = Enumerable.Range(0, 7).Select(d => new WorkingHourItemDto
            {
                DayOfWeek = d,
                StartTime = "08:00",
                EndTime = "18:00",
                IsClosed = false
            }).ToList()
        })).EnsureSuccessStatusCode();

        var room = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/rooms",
            new CreateClinicRoomDto { Code = "G11-R1", NameAr = "غرفة 1", NameEn = "Room 1", SpecialtyCode = "dental" }))
            .Content.ReadFromJsonAsync<ClinicRoomViewModel>();

        var doc = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/doctors",
            new CreateClinicDoctorDto
            {
                FullName = "Dr. Hany Kamal",
                TitleAr = "استشاري",
                TitleEn = "Consultant",
                SpecialtyCodes = ["dental"],
                AssignedRoomIds = [room!.Id],
                SlotDurationMinutes = 30
            })).Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/scheduling/doctors/{doc!.Id}/booking-settings",
            new UpdateDoctorBookingSettingDto { AcceptsOnlineBooking = true, MaxDailyOnlineBookings = 10 })).EnsureSuccessStatusCode();

        var cat = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/scheduling/categories",
            new UpsertAppointmentCategoryDto
            {
                SpecialtyCode = "dental",
                NameAr = "فحص",
                NameEn = "Checkup",
                DefaultDurationMinutes = 30,
                DefaultPriceAmount = 350m,
                DefaultPriceCurrency = "EGP",
                BookableOnline = true
            })).Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();

        // Seed 2 patients and activate portal sessions
        (Guid patAId, HttpClient patAClient, string patAEmail) = await SeedAndLoginPortalPatientAsync(clinicClient, clinicId, "elite-p11", "Tarek Amin", "01093000001");
        (Guid patBId, HttpClient patBClient, string patBEmail) = await SeedAndLoginPortalPatientAsync(clinicClient, clinicId, "elite-p11", "Salma Nabil", "01093000002");

        // Gate Criterion 1: Landing advertises portal-booking, availability lists slots & respects headers
        var landing = await patAClient.GetFromJsonAsync<PublicClinicLandingViewModel>("/api/v1/c/elite-p11/portal/landing");
        landing!.AvailableSections.Should().Contain("portal-booking");

        DateTimeOffset slot1 = new(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset slot2 = new(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var availResp = await patAClient.GetAsync($"/api/v1/c/elite-p11/portal/availability?categoryId={cat!.Id}&fromUtc={Uri.EscapeDataString(slot1.ToString("O"))}&toUtc={Uri.EscapeDataString(slot2.AddHours(4).ToString("O"))}");
        availResp.EnsureSuccessStatusCode();
        availResp.Headers.CacheControl!.NoStore.Should().BeTrue();
        var avail = await availResp.Content.ReadFromJsonAsync<PortalAvailabilityViewModel>();
        avail!.Slots.Should().NotBeEmpty();
        avail.Categories.Should().ContainSingle(c => c.CategoryId == cat.Id && c.PriceAmount == 350m);

        // Gate Criterion 2: AutoConfirm book, OperationId idempotency, 409 SLOT_TAKEN collision
        Guid opA = GuidV7.NewGuid();
        var bookAResp = await patAClient.PostAsJsonAsync("/api/v1/c/elite-p11/portal/appointments", new PortalBookAppointmentDto
        {
            DoctorId = doc.Id,
            CategoryId = cat.Id,
            StartAtUtc = slot1,
            Notes = "Gate11 book",
            OperationId = opA
        });
        bookAResp.EnsureSuccessStatusCode();
        var bookedA = await bookAResp.Content.ReadFromJsonAsync<PortalUpcomingAppointmentItemViewModel>();
        bookedA!.Status.Should().Be("Booked");

        var bookAConflictResp = await patBClient.PostAsJsonAsync("/api/v1/c/elite-p11/portal/appointments", new PortalBookAppointmentDto
        {
            DoctorId = doc.Id,
            CategoryId = cat.Id,
            StartAtUtc = slot1,
            OperationId = GuidV7.NewGuid()
        });
        bookAConflictResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await bookAConflictResp.Content.ReadAsStringAsync()).Should().Contain("SLOT_TAKEN");

        // Gate Criterion 3: Cancellation frees slot + triggers waiting list offer; cross-patient cancel 404
        var crossCancelResp = await patBClient.PostAsJsonAsync($"/api/v1/c/elite-p11/portal/appointments/{bookedA.AppointmentId}/cancel", new CancelPortalAppointmentDto { Reason = "Invalid" });
        crossCancelResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await clinicClient.PostAsJsonAsync($"/api/v1/clinics/{clinicId}/queue/waiting-list", new AddToWaitingListDto
        {
            PatientId = patBId,
            DoctorId = doc.Id,
            CategoryId = cat.Id,
            PreferredDate = "2026-08-12",
            Notes = "Waiting for 09:00",
            OperationId = GuidV7.NewGuid()
        })).EnsureSuccessStatusCode();

        var cancelAResp = await patAClient.PostAsJsonAsync($"/api/v1/c/elite-p11/portal/appointments/{bookedA.AppointmentId}/cancel", new CancelPortalAppointmentDto { Reason = "Changed plans" });
        cancelAResp.EnsureSuccessStatusCode();

        var slotOffers = await clinicClient.GetFromJsonAsync<List<SlotOfferItemViewModel>>($"/api/v1/clinics/{clinicId}/queue/slot-offers");
        slotOffers.Should().Contain(o => o.PatientId == patBId && o.SlotStartUtc == slot1);

        // Gate Criterion 4: RequiresApproval workflow -> approve, decline, expire stale
        (await clinicClient.PutAsJsonAsync($"/api/v1/clinics/{clinicId}/portal/booking-settings", new UpdateClinicPortalBookingSettingDto
        {
            Mode = "RequiresApproval",
            MinLeadTimeMinutes = 60,
            MaxDaysAhead = 30,
            SlotStepMinutes = 30,
            MaxActiveFutureBookingsPerPatient = 3,
            MaxBookingsPerDay = 20,
            CancellationCutoffHours = 12,
            AutoBlockAfterNoShows = 3,
            ApprovalHoldHours = 2,
            ShowPricesWhenAvailable = true
        })).EnsureSuccessStatusCode();

        var reqResp = await patAClient.PostAsJsonAsync("/api/v1/c/elite-p11/portal/appointments", new PortalBookAppointmentDto
        {
            DoctorId = doc.Id,
            CategoryId = cat.Id,
            StartAtUtc = slot2,
            OperationId = GuidV7.NewGuid()
        });
        reqResp.EnsureSuccessStatusCode();
        var reqBody = await reqResp.Content.ReadFromJsonAsync<PortalUpcomingAppointmentItemViewModel>();
        reqBody!.Status.Should().Be("Requested");

        var requestsList = await clinicClient.GetFromJsonAsync<List<PortalBookingRequestItemViewModel>>($"/api/v1/clinics/{clinicId}/portal/booking-requests?status=Requested");
        requestsList.Should().ContainSingle(r => r.AppointmentId == reqBody.AppointmentId);

        var approveResp = await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/portal/booking-requests/{reqBody.AppointmentId}/approve",
            new ReviewPortalBookingRequestDto { ReviewNotes = "Approved in gate" });
        approveResp.EnsureSuccessStatusCode();
        (await approveResp.Content.ReadFromJsonAsync<PortalBookingRequestItemViewModel>())!.Status.Should().Be("Booked");

        // Second request -> expire after hold
        DateTimeOffset slot3 = new(2026, 8, 13, 9, 0, 0, TimeSpan.Zero);
        var staleReq = await (await patBClient.PostAsJsonAsync("/api/v1/c/elite-p11/portal/appointments", new PortalBookAppointmentDto
        {
            DoctorId = doc.Id,
            CategoryId = cat.Id,
            StartAtUtc = slot3,
            OperationId = GuidV7.NewGuid()
        })).Content.ReadFromJsonAsync<PortalUpcomingAppointmentItemViewModel>();

        _clock.Advance(TimeSpan.FromHours(3));
        clinicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginClinicUserAsync(clinicClient, ownerAEmail, "OwnerPass#2026", "dev-owner-g11"));
        patAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginPortalPatientAsync(patAClient, "elite-p11", patAEmail));
        patBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginPortalPatientAsync(patBClient, "elite-p11", patBEmail));
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginSuperAdminAsync(adminClient, "ahmed@platform.local", "SuperAdminGate#2026", adminTotpKey));

        var expireRes = await (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/portal/booking-requests/expire-stale", null))
            .Content.ReadFromJsonAsync<PortalExpiredRequestsViewModel>();
        expireRes!.ExpiredCount.Should().BeGreaterThanOrEqualTo(1);

        // Gate Criterion 5: 3 NoShows block online booking -> manual unblock restores
        for (int i = 0; i < 3; i++)
        {
            DateTimeOffset nsStart = new(_clock.UtcNow.UtcDateTime.Date.AddDays(3 + i).AddHours(11), TimeSpan.Zero);
            var staffBook = await (await clinicClient.PostAsJsonAsync($"/api/v1/clinics/{clinicId}/scheduling/appointments", new BookAppointmentDto
            {
                PatientId = patBId,
                DoctorId = doc.Id,
                RoomId = room.Id,
                CategoryId = cat.Id,
                StartUtc = nsStart,
                EndUtc = nsStart.AddMinutes(30),
                Priority = "Normal"
            })).Content.ReadFromJsonAsync<AppointmentItemViewModel>();

            (await clinicClient.PostAsJsonAsync(
                $"/api/v1/clinics/{clinicId}/scheduling/appointments/{staffBook!.Id}/status",
                new TransitionAppointmentStatusDto { NextStatus = "NoShow", Reason = "Absent" })).EnsureSuccessStatusCode();
        }

        var blockedBookResp = await patBClient.PostAsJsonAsync("/api/v1/c/elite-p11/portal/appointments", new PortalBookAppointmentDto
        {
            DoctorId = doc.Id,
            CategoryId = cat.Id,
            StartAtUtc = new DateTimeOffset(_clock.UtcNow.UtcDateTime.Date.AddDays(2).AddHours(10), TimeSpan.Zero),
            OperationId = GuidV7.NewGuid()
        });
        blockedBookResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await blockedBookResp.Content.ReadAsStringAsync()).Should().Contain("PATIENT_ONLINE_BOOKING_BLOCKED");

        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/portal/patients/{patBId}/booking-block",
            new SetPatientOnlineBookingBlockDto { Blocked = false })).EnsureSuccessStatusCode();

        // Gate Criterion 6: Widgets, Super Admin inspect (read-only), and module-off isolation
        var dailyWidgetResp = await clinicClient.GetAsync($"/api/v1/clinics/{clinicId}/dashboards/widgets/portal-booking.daily-count");
        dailyWidgetResp.EnsureSuccessStatusCode();

        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicId}/inspect/portal-booking");
        inspectResp.EnsureSuccessStatusCode();
        var inspectWrite = await adminClient.PostAsync($"/api/v1/platform/clinics/{clinicId}/inspect/portal-booking", null);
        inspectWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Turn portal-booking OFF -> portal availability blocked while staff scheduling keeps working
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "portal-booking", Enabled = false }]
        })).EnsureSuccessStatusCode();

        var disabledAvailResp = await patAClient.GetAsync("/api/v1/c/elite-p11/portal/availability");
        disabledAvailResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await disabledAvailResp.Content.ReadAsStringAsync()).Should().Contain("MODULE_DISABLED");

        (await clinicClient.GetAsync($"/api/v1/clinics/{clinicId}/scheduling/categories")).EnsureSuccessStatusCode();
    }

    private async Task<(Guid PatientId, HttpClient PortalClient, string Email)> SeedAndLoginPortalPatientAsync(
        HttpClient clinicClient,
        Guid clinicId,
        string slug,
        string fullName,
        string phone)
    {
        var pat = await (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/patients",
            new CreatePatientDto(null, fullName, phone, null, new DateOnly(1993, 4, 12), null, "Male", null, null, null, "dental", false, null)))
            .Content.ReadFromJsonAsync<PatientDetailViewModel>();

        string email = $"{Guid.NewGuid():N}@patient.local";
        (await clinicClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/portal/patients/{pat!.Id}/invite",
            new InvitePatientToPortalDto(email, false, null, true, "v1", false))).EnsureSuccessStatusCode();

        string code = await ExtractLatestCodeFromOutboxAsync(email);
        (await _factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/c/{slug}/portal/activate",
            new ActivatePatientPortalDto(email, code, "PatientSecret#2026"))).EnsureSuccessStatusCode();

        var portalClient = _factory.CreateClient();
        portalClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string token = await LoginPortalPatientAsync(portalClient, slug, email);
        portalClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (pat.Id, portalClient, email);
    }

    private static async Task<string> LoginPortalPatientAsync(HttpClient portalClient, string slug, string email)
    {
        var loginResp = await portalClient.PostAsJsonAsync(
            $"/api/v1/c/{slug}/portal/login",
            new PatientPortalLoginDto(email, "PatientSecret#2026"));
        loginResp.EnsureSuccessStatusCode();
        var session = await loginResp.Content.ReadFromJsonAsync<PatientPortalSessionViewModel>();
        return session!.AccessToken;
    }

    private static async Task CompleteOnboardingAsync(HttpClient clinicClient, Guid clinicId, string slug, string[] specialties)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("Elite Gate 11", "10 Nile St", "Maadi", "Cairo", "01001112233", null, null, null))).EnsureSuccessStatusCode();
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

    private async Task<string> LoginSuperAdminAsync(HttpClient adminClient, string email, string password, string manualSetupKey)
    {
        adminClient.DefaultRequestHeaders.Authorization = null;
        string totpCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var totpService = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            totpCode = totpService.ComputeCurrentCode(manualSetupKey, _clock.UtcNow);
        }

        var loginResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = email,
            Password = password,
            HourlyCode = totpCode
        });
        loginResp.EnsureSuccessStatusCode();
        var body = await loginResp.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>();
        return body!.AccessToken;
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

        return await LoginClinicUserAsync(clinicClient, email, password, deviceId);
    }

    private static async Task<string> LoginClinicUserAsync(HttpClient clinicClient, string email, string password, string deviceId)
    {
        clinicClient.DefaultRequestHeaders.Authorization = null;
        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "Gate11Browser"
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
