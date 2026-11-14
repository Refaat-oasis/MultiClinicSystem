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

public sealed class Phase10Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate10_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase10Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero));
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
    public async Task Phase10_IntegrationGate_PatientPortalReadOnlyIsolationSecurityAndModules()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        string adminToken = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerAEmail = $"g10-owner-a-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g10-owner-b-{Guid.NewGuid():N}@clinic.local";

        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G10 A",
            Email = ownerAEmail,
            MaxClinics = 2
        });
        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G10 B",
            Email = ownerBEmail,
            MaxClinics = 2
        });

        using var clinicAClient = _factory.CreateClient();
        clinicAClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerAToken = await ActivateAndLoginClinicUserAsync(clinicAClient, ownerAEmail, "OwnerPass#2026", "dev-owner-a");
        clinicAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAToken);
        clinicAClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-a");

        using var clinicBClient = _factory.CreateClient();
        clinicBClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerBToken = await ActivateAndLoginClinicUserAsync(clinicBClient, ownerBEmail, "OwnerPass#2026", "dev-owner-b");
        clinicBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerBToken);
        clinicBClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-b");

        var createClinicA = await clinicAClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Smile Dental Clinic", "UTC", "EGP", "None", "dev-owner-a"));
        var createClinicB = await clinicBClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Other Clinic B", "UTC", "EGP", "None", "dev-owner-b"));
        Guid clinicAId = (await createClinicA.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        Guid clinicBId = (await createClinicB.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;

        await CompleteOnboardingAsync(clinicAClient, clinicAId, "smile-dental", ["dental"]);
        await CompleteOnboardingAsync(clinicBClient, clinicBId, "clinic-b-portal", ["dental"]);

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "patient-history", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "attachments", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "treatment-plans", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "portal", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicBId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "portal", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        // 1. Seed structural, clinical, scheduling, and treatment plan data in Clinic A
        (await clinicAClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/working-hours", new SaveWorkingHoursDto
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

        var patAResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients",
            new CreatePatientDto(null, "Nadia Hassan", "01092000001", null, new DateOnly(1993, 4, 12), null, "Female", null, null, null, "dental", false, null));
        patAResp.EnsureSuccessStatusCode();
        var patA = await patAResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();
        ArgumentNullException.ThrowIfNull(patA);

        var otherPatResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients",
            new CreatePatientDto(null, "Omar Youssef", "01092000002", null, new DateOnly(1988, 9, 5), null, "Male", null, null, null, "dental", false, null));
        otherPatResp.EnsureSuccessStatusCode();
        var otherPat = await otherPatResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/patients/{patA.Id}/allergies",
            new AddPatientAllergyDto("Aspirin", "Severe", "Bronchospasm"))).EnsureSuccessStatusCode();
        (await clinicAClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/patients/{patA.Id}/history",
            new UpdatePatientMedicalHistoryDto("Healthy", ["Asthma"], ["Inhaler"], [], true))).EnsureSuccessStatusCode();

        var roomResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/rooms",
            new CreateClinicRoomDto { Code = "G10-R1", NameAr = "عيادة 1", NameEn = "Operatory 1", SpecialtyCode = "dental" });
        var room = await roomResp.Content.ReadFromJsonAsync<ClinicRoomViewModel>();

        var docResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/doctors",
            new CreateClinicDoctorDto
            {
                FullName = "Dr. Salma Adel",
                TitleAr = "استشاري",
                TitleEn = "Consultant",
                SpecialtyCodes = ["dental"],
                AssignedRoomIds = [room!.Id],
                SlotDurationMinutes = 30
            });
        var doc = await docResp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        var catResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/scheduling/categories",
            new UpsertAppointmentCategoryDto
            {
                SpecialtyCode = "dental",
                NameAr = "علاج عصب",
                NameEn = "Root Canal",
                DefaultDurationMinutes = 30,
                DefaultPriceAmount = 600m,
                DefaultPriceCurrency = "EGP",
                BookableOnline = true
            });
        var cat = await catResp.Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();

        // Upcoming appointment
        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/scheduling/appointments", new BookAppointmentDto
        {
            PatientId = patA.Id,
            DoctorId = doc!.Id,
            RoomId = room.Id,
            CategoryId = cat!.Id,
            StartUtc = _clock.UtcNow.AddDays(3),
            EndUtc = _clock.UtcNow.AddDays(3).AddMinutes(30),
            Priority = "Normal"
        })).EnsureSuccessStatusCode();

        // Completed Visit 1 (visible) + Visit 2 (to hide) + Other Patient's Visit 3
        (Guid visit1Id, Guid rx1Id) = await CreateCompletedVisitWithRxAsync(clinicAClient, clinicAId, patA.Id, doc.Id, room.Id);
        (Guid visit2Id, _) = await CreateCompletedVisitWithRxAsync(clinicAClient, clinicAId, patA.Id, doc.Id, room.Id);
        (Guid otherVisitId, _) = await CreateCompletedVisitWithRxAsync(clinicAClient, clinicAId, otherPat!.Id, doc.Id, room.Id);

        // 4-session treatment plan with material costs that must NEVER leak to patient portal
        var planResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/treatment-plans", new CreateTreatmentPlanDto(
            null, patA.Id, doc.Id, "Dental Crown Plan", TreatmentPlanMode.FixedTotal, 4000m, 950m, 1000m,
            new DateOnly(2026, 6, 8), true, null,
            [
                new CreatePlanSessionDto(1, "Prep", 0, 1, 2, 0m, 200m, null),
                new CreatePlanSessionDto(2, "Impression", 7, 1, 2, 0m, 250m, null),
                new CreatePlanSessionDto(3, "Try-in", 7, 1, 2, 0m, 250m, null),
                new CreatePlanSessionDto(4, "Cementation", 7, 1, 2, 0m, 250m, null)
            ],
            [new CreatePlanInstallmentDto(1, false, new DateOnly(2026, 6, 20), 3000m)],
            GuidV7.NewGuid()));
        planResp.EnsureSuccessStatusCode();
        var plan = await planResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();

        // Gate Criterion 1: Invite patient by email -> capture code -> activate -> sign in at /c/smile-dental
        string patientEmail = $"nadia-{Guid.NewGuid():N}@patient.local";
        var inviteResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/portal/patients/{patA.Id}/invite",
            new InvitePatientToPortalDto(patientEmail, false, null, true, "v1", false));
        inviteResp.EnsureSuccessStatusCode();

        string activationCode = await ExtractLatestCodeFromOutboxAsync(patientEmail);
        var actResp = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/c/smile-dental/portal/activate",
            new ActivatePatientPortalDto(patientEmail, activationCode, "PatientSecret#2026"));
        actResp.EnsureSuccessStatusCode();

        using var portalClient = _factory.CreateClient();
        portalClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        var loginResp = await portalClient.PostAsJsonAsync(
            "/api/v1/c/smile-dental/portal/login",
            new PatientPortalLoginDto(patientEmail, "PatientSecret#2026"));
        loginResp.EnsureSuccessStatusCode();
        loginResp.Headers.CacheControl!.NoStore.Should().BeTrue();
        loginResp.Headers.GetValues("X-Robots-Tag").First().Should().Contain("noindex");

        var session = await loginResp.Content.ReadFromJsonAsync<PatientPortalSessionViewModel>();
        ArgumentNullException.ThrowIfNull(session);
        portalClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        // Gate Criterion 2: Allergies, conditions, meds, ended visit, Rx PDF, upcoming session, 4-session plan, NO toolCost/markup
        var overviewResp = await portalClient.GetAsync("/api/v1/c/smile-dental/portal/overview");
        overviewResp.EnsureSuccessStatusCode();
        overviewResp.Headers.CacheControl!.NoStore.Should().BeTrue();
        string overviewRaw = await overviewResp.Content.ReadAsStringAsync();
        overviewRaw.Should().NotContainEquivalentOf("toolCost");
        overviewRaw.Should().NotContainEquivalentOf("materialsCost");
        overviewRaw.Should().NotContainEquivalentOf("markup");
        overviewRaw.Should().NotContainEquivalentOf("privateDoctorNotes");
        overviewRaw.Should().NotContainEquivalentOf("Secret doctor note");

        var overview = await overviewResp.Content.ReadFromJsonAsync<PortalHomeOverviewViewModel>();
        overview!.MedicalBackground!.Allergies.Should().ContainSingle(a => a.Allergen == "Aspirin");
        overview.MedicalBackground.ChronicConditions.Should().Contain("Asthma");
        overview.MedicalBackground.CurrentMedications.Should().Contain("Inhaler");
        overview.UpcomingAppointments.Should().HaveCount(1);
        overview.Visits.Should().HaveCount(2);
        overview.TreatmentPlans.Should().ContainSingle(p => p.TotalSessionsCount == 4 && p.TotalCostToPatient == 4000m);

        var signedResp = await portalClient.PostAsync($"/api/v1/c/smile-dental/portal/prescriptions/{rx1Id}/signed-url", null);
        signedResp.EnsureSuccessStatusCode();
        var signedUrl = await signedResp.Content.ReadFromJsonAsync<PortalSignedUrlViewModel>();
        var pdfDownload = await portalClient.GetAsync(signedUrl!.Url);
        pdfDownload.EnsureSuccessStatusCode();
        (await pdfDownload.Content.ReadAsByteArrayAsync()).Take(4).ToArray().Should().Equal("%PDF"u8.ToArray());

        // Gate Criterion 3: Hide visit 2 and turn off plan cost -> patient view updates immediately
        (await clinicAClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/portal/visits/{visit2Id}/visibility",
            new SetPortalVisibilityDto(false))).EnsureSuccessStatusCode();
        (await clinicAClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{plan!.Id}/portal-cost-visibility",
            new SetPlanPortalCostVisibilityDto(false))).EnsureSuccessStatusCode();

        var updatedOverview = await portalClient.GetFromJsonAsync<PortalHomeOverviewViewModel>("/api/v1/c/smile-dental/portal/overview");
        updatedOverview!.Visits.Should().ContainSingle(v => v.VisitId == visit1Id);
        updatedOverview.TreatmentPlans![0].ShowCostToPatient.Should().BeFalse();
        updatedOverview.TreatmentPlans![0].TotalCostToPatient.Should().BeNull();

        // Gate Criterion 4: Cross-patient 404, cross-clinic 404, staff vs portal cookie realm isolation
        var otherPatientVisitResp = await portalClient.GetAsync($"/api/v1/c/smile-dental/portal/visits/{otherVisitId}");
        otherPatientVisitResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var clinicBPortalResp = await portalClient.GetAsync("/api/v1/c/clinic-b-portal/portal/overview");
        clinicBPortalResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var portalCookieOnStaffClient = _factory.CreateClient();
        portalCookieOnStaffClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        portalCookieOnStaffClient.DefaultRequestHeaders.Add("Cookie", $"__Host-Portal-Token={session.AccessToken}");
        var portalOnStaffResp = await portalCookieOnStaffClient.GetAsync($"/api/v1/clinics/{clinicAId}/patients");
        ((int)portalOnStaffResp.StatusCode).Should().BeOneOf(401, 403);

        using var staffCookieOnPortalClient = _factory.CreateClient();
        staffCookieOnPortalClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        staffCookieOnPortalClient.DefaultRequestHeaders.Add("Cookie", $"__Host-Staff-Token={ownerAToken}");
        var staffOnPortalResp = await staffCookieOnPortalClient.GetAsync("/api/v1/c/smile-dental/portal/overview");
        ((int)staffOnPortalResp.StatusCode).Should().BeOneOf(401, 403);

        // Gate Criterion 5: Super Admin turns portal off -> landing 404, API MODULE_DISABLED; staff unaffected
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "portal", Enabled = false }]
        })).EnsureSuccessStatusCode();

        var offLandingResp = await _factory.CreateClient().GetAsync("/api/v1/c/smile-dental/portal/landing");
        offLandingResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var offPortalApiResp = await portalClient.GetAsync("/api/v1/c/smile-dental/portal/overview");
        offPortalApiResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await offPortalApiResp.Content.ReadAsStringAsync()).Should().Contain("MODULE_DISABLED");

        var staffCatsStillWork = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/scheduling/categories");
        staffCatsStillWork.EnsureSuccessStatusCode();

        // Turn portal back ON and turn treatment-plans OFF -> removes only plans section
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "portal", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "treatment-plans", Enabled = false }
            ]
        })).EnsureSuccessStatusCode();

        var noPlansOverview = await portalClient.GetFromJsonAsync<PortalHomeOverviewViewModel>("/api/v1/c/smile-dental/portal/overview");
        noPlansOverview!.AvailableSections.Should().NotContain("treatment-plans");
        noPlansOverview.TreatmentPlans.Should().BeNull();
        noPlansOverview.Visits.Should().NotBeNull();

        var directPlansDisabled = await portalClient.GetAsync("/api/v1/c/smile-dental/portal/treatment-plans");
        directPlansDisabled.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Gate Criterion 6: Owner sees access logs; password reset revokes old portal token
        var accessLogsResp = await clinicAClient.GetFromJsonAsync<List<PortalAccessLogItemViewModel>>($"/api/v1/clinics/{clinicAId}/portal/access-logs");
        accessLogsResp.Should().NotBeNullOrEmpty();

        (await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/c/smile-dental/portal/forgot-password",
            new PatientPortalForgotPasswordDto(patientEmail))).EnsureSuccessStatusCode();
        string resetCode = await ExtractLatestCodeFromOutboxAsync(patientEmail);
        (await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/c/smile-dental/portal/reset-password",
            new PatientPortalResetPasswordDto(patientEmail, resetCode, "BrandNewPass#2026"))).EnsureSuccessStatusCode();

        var staleTokenResp = await portalClient.GetAsync("/api/v1/c/smile-dental/portal/overview");
        staleTokenResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Bad login attempt to exercise portal.failed-logins widget
        await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/c/smile-dental/portal/login",
            new PatientPortalLoginDto(patientEmail, "WrongPass#123"));

        // Gate Criterion 7: Dashboard widgets match source data & Super Admin inspect portal works read-only
        var accountsWidget = await clinicAClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/portal.accounts-summary");
        accountsWidget!.PrimaryValue.Should().Be(1m);

        var loginsWidget = await clinicAClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/portal.logins-week");
        loginsWidget!.PrimaryValue.Should().BeGreaterOrEqualTo(1m);

        var failedWidget = await clinicAClient.GetFromJsonAsync<DashboardWidgetDataViewModel>(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/portal.failed-logins");
        failedWidget!.PrimaryValue.Should().Be(1m);

        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/portal");
        inspectResp.EnsureSuccessStatusCode();
        var inspectPortal = await inspectResp.Content.ReadFromJsonAsync<ClinicPortalInspectViewModel>();
        inspectPortal!.TotalAccountsCount.Should().Be(1);
    }

    private static async Task<(Guid VisitId, Guid RxId)> CreateCompletedVisitWithRxAsync(
        HttpClient client,
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        Guid roomId)
    {
        var startResp = await client.PostAsJsonAsync($"/api/v1/clinics/{clinicId}/clinical/visits", new StartVisitDto
        {
            PatientId = patientId,
            DoctorId = doctorId,
            SpecialtyCode = "dental",
            RoomId = roomId,
            PlannedDurationMinutes = 30,
            IsVisibleToPatient = true
        });
        startResp.EnsureSuccessStatusCode();
        var visit = await startResp.Content.ReadFromJsonAsync<VisitSessionViewModel>();
        Guid visitId = visit!.Id;

        (await client.PutAsJsonAsync($"/api/v1/clinics/{clinicId}/clinical/visits/{visitId}/notes", new UpdateVisitNotesDto
        {
            DiagnosisSummary = "Pulpitis tooth #16",
            Advice = "Warm saline rinse",
            PrivateDoctorNotes = "Secret doctor note",
            IsVisibleToPatient = true
        })).EnsureSuccessStatusCode();

        var rxResp = await client.PostAsJsonAsync($"/api/v1/clinics/{clinicId}/clinical/visits/{visitId}/prescriptions", new CreatePrescriptionDto
        {
            AcknowledgeAllergyWarning = true,
            Items =
            [
                new PrescriptionItemDto
                {
                    DrugName = "Amoxicillin",
                    ActiveIngredient = "Amoxicillin",
                    Dosage = "500mg",
                    Frequency = "TID",
                    Duration = "5 days"
                }
            ]
        });
        rxResp.EnsureSuccessStatusCode();
        var rx = await rxResp.Content.ReadFromJsonAsync<PrescriptionViewModel>();

        (await client.PostAsJsonAsync($"/api/v1/clinics/{clinicId}/clinical/visits/{visitId}/complete", new CompleteVisitDto())).EnsureSuccessStatusCode();
        return (visitId, rx!.Id);
    }

    private static async Task CompleteOnboardingAsync(HttpClient clinicClient, Guid clinicId, string slug, string[] specialties)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("Smile Clinic", "10 Nile St", "Maadi", "Cairo", "01001112233", null, null, null))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/types",
            new OnboardingTypesDto(specialties.Select(s => new SpecialtySelectionDto(s, null, null)).ToList()))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/slug",
            new OnboardingSlugDto(slug))).EnsureSuccessStatusCode();
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/complete", null)).EnsureSuccessStatusCode();
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
            DeviceName = "Gate10Browser"
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

