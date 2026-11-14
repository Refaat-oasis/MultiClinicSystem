using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class Phase13TimeEngineTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase13TimeEngineTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_TimeEngineModule_When_LearningSamplesExcusedToggledScheduledAndDisabled_Then_AllRulesEnforced()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero)
        };
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var availabilityQueries = sp.GetRequiredService<IAvailabilityQueries>();
        var clinicalService = sp.GetRequiredService<IClinicalService>();
        var timeEngineService = sp.GetRequiredService<ITimeEngineService>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p13-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P13", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "عيادة الزمن", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicA = clinicARes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicA, $"time-p13-{clinicA:N}"[..16], ["dental"]);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة بدون محرك", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicB = clinicBRes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicB, $"notime-p13-{clinicB:N}"[..16], ["dental"]);

        // Clinic A: time-engine + scheduling + clinical + patients ON
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicA,
            new[]
            {
                new ModuleToggleInput("patients", true, "Phase13"),
                new ModuleToggleInput("scheduling", true, "Phase13"),
                new ModuleToggleInput("clinical", true, "Phase13"),
                new ModuleToggleInput("time-engine", true, "Phase13")
            },
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        // Clinic B: scheduling + clinical + patients ON, time-engine intentionally OFF
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicB,
            new[]
            {
                new ModuleToggleInput("patients", true, "Phase13"),
                new ModuleToggleInput("scheduling", true, "Phase13"),
                new ModuleToggleInput("clinical", true, "Phase13")
            },
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        var roomA = (await structureService.CreateRoomAsync(ownerId, clinicA, "T-R1", "غرفة 1", "Room 1", "dental", null, "dev-owner", null, null)).Value;
        var docA = (await structureService.CreateDoctorAsync(ownerId, clinicA, null, "د. عادل حسني", "أخصائي", "Specialist", ["dental"], [roomA.Id], 30, "dev-owner", null, null)).Value;
        await structureService.SaveWorkingHoursAsync(
            ownerId,
            clinicA,
            docA.Id,
            [new WorkingHourInput((int)DayOfWeek.Monday, "09:00", "17:00", false)],
            "dev-owner",
            null,
            null);

        var catA = (await schedulingService.CreateCategoryAsync(
            ownerId, clinicA, "dental", "علاج جذور", "Root Canal", 30, 500m, "EGP", true, "dev-owner", null, null)).Value;
        var patA = (await patientService.CreatePatientAsync(
            ownerId, clinicA, new CreatePatientInput(null, "سامي نادر", "01077000001", null, new DateOnly(1992, 4, 12), null, "Male", null, null, null, "dental", false, null), "dev-owner", null, null)).Value;

        // Configure clinic settings: minSample=5, trim=0.15, maxStep=50% so step allows adopting trimmed 20m -> let's test step limit first!
        (await timeEngineService.UpdateClinicSettingsAsync(
            ownerId, clinicA, new UpdateClinicTimeEngineSettingInput(5, 0.15m, 20m, GuidV7.NewGuid()), "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        // Complete 3 sessions (< minSample 5): 45, 45, 45 -> baseline 30 stays unchanged
        Guid thirdVisitId = Guid.Empty;
        for (int i = 0; i < 3; i++)
        {
            var startAt = clock.UtcNow;
            var visit = (await clinicalService.StartOrResumeVisitAsync(
                ownerId, clinicA, new StartVisitInput(null, null, patA.Id, docA.Id, "dental", roomA.Id, 30, true, null, catA.Id), "dev-owner", null, null)).Value;
            thirdVisitId = visit.Id;
            clock.UtcNow = startAt.AddMinutes(45);
            (await clinicalService.CompleteVisitAsync(ownerId, clinicA, visit.Id, new CompleteVisitInput(false, null, null, null), "dev-owner", null, null)).IsSuccess.Should().BeTrue();
        }
        await outbox.ProcessPendingMessagesAsync();

        var statsBeforeMin = (await timeEngineService.ListCategoryStatsAsync(ownerId, clinicA, docA.Id, catA.Id, "dev-owner")).Value;
        statsBeforeMin.Should().ContainSingle();
        statsBeforeMin[0].SampleCount.Should().Be(3);
        statsBeforeMin[0].SuggestedDurationMinutes.Should().Be(30);
        statsBeforeMin[0].AppliedDurationMinutes.Should().Be(30);
        statsBeforeMin[0].IsAdopted.Should().BeFalse();

        // Complete 2 more sessions (45m) + 1 extreme outlier (180m) + 1 excused (120m):
        // Let's also make 1 session excused at completion -> must NOT enter non-excused sample count!
        foreach (int mins in new[] { 45, 45 })
        {
            var sAt = clock.UtcNow;
            var v = (await clinicalService.StartOrResumeVisitAsync(
                ownerId, clinicA, new StartVisitInput(null, null, patA.Id, docA.Id, "dental", roomA.Id, 30, true, null, catA.Id), "dev-owner", null, null)).Value;
            clock.UtcNow = sAt.AddMinutes(mins);
            await clinicalService.CompleteVisitAsync(ownerId, clinicA, v.Id, new CompleteVisitInput(false, null, null, null), "dev-owner", null, null);
        }

        // Complete an excused emergency session (120m)
        var exStart = clock.UtcNow;
        var exVisit = (await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicA, new StartVisitInput(null, null, patA.Id, docA.Id, "dental", roomA.Id, 30, true, null, catA.Id), "dev-owner", null, null)).Value;
        clock.UtcNow = exStart.AddMinutes(120);
        await clinicalService.CompleteVisitAsync(ownerId, clinicA, exVisit.Id, new CompleteVisitInput(true, "Emergency complication", null, null), "dev-owner", null, null);

        // Process outbox twice to assert idempotency (same session never double-counted)
        await outbox.ProcessPendingMessagesAsync();
        await outbox.ProcessPendingMessagesAsync();

        var statsAfterFive = (await timeEngineService.ListCategoryStatsAsync(ownerId, clinicA, docA.Id, catA.Id, "dev-owner")).Value;
        statsAfterFive.Should().ContainSingle();
        statsAfterFive[0].SampleCount.Should().Be(5);
        statsAfterFive[0].ExcusedCount.Should().Be(1);
        statsAfterFive[0].TrimmedAverageMinutes.Should().Be(45m);
        // Step limit 20% from 30m caps step to max 36m
        statsAfterFive[0].SuggestedDurationMinutes.Should().Be(36);
        statsAfterFive[0].AppliedDurationMinutes.Should().Be(36);
        statsAfterFive[0].IsAdopted.Should().BeTrue();

        // Next recalculation clamps 36m + 20% -> 43m (within 43.2) and approaches 45m cleanly
        // Verify Scheduling booking with EndUtc == null uses applied 36m duration!
        var bookingMonday = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero); // Future Monday
        var bookedOn = await schedulingService.BookAppointmentAsync(
            ownerId,
            clinicA,
            new BookAppointmentInput(null, patA.Id, docA.Id, roomA.Id, catA.Id, bookingMonday, null, "Normal", null, null, null),
            "dev-owner",
            null,
            null);
        bookedOn.IsSuccess.Should().BeTrue();
        (bookedOn.Value.EndUtc - bookedOn.Value.StartUtc).TotalMinutes.Should().Be(36);

        // Availability free-slots also report 36m slots for docA!
        var slotsOn = await availabilityQueries.GetFreeSlotsAsync(
            ownerId,
            clinicA,
            catA.Id,
            docA.Id,
            bookingMonday.Date,
            bookingMonday.Date.AddDays(1),
            "dev-owner");
        slotsOn.IsSuccess.Should().BeTrue();
        slotsOn.Value.Should().NotBeEmpty();
        slotsOn.Value.All(s => s.DurationMinutes == 36).Should().BeTrue();

        // Retroactively excuse one of the 45m sessions -> drops active samples from 5 to 4 (< minSample 5)
        var excuseUpdate = await timeEngineService.SetSessionDurationExcusedAsync(
            ownerId,
            clinicA,
            thirdVisitId,
            new SetSessionDurationExcusedInput(true, "Special sedation case", GuidV7.NewGuid()),
            "dev-owner",
            null,
            null);
        excuseUpdate.IsSuccess.Should().BeTrue();
        excuseUpdate.Value.IsExcused.Should().BeTrue();

        var statsAfterRetroExcuse = (await timeEngineService.ListCategoryStatsAsync(ownerId, clinicA, docA.Id, catA.Id, "dev-owner")).Value;
        statsAfterRetroExcuse[0].SampleCount.Should().Be(4);
        statsAfterRetroExcuse[0].ExcusedCount.Should().Be(2);

        // Un-excuse + add 200m outlier -> test outlier trimming with 10 samples vs 5 samples, and doctor OFF toggle!
        await timeEngineService.SetSessionDurationExcusedAsync(
            ownerId, clinicA, thirdVisitId, new SetSessionDurationExcusedInput(false, null, GuidV7.NewGuid()), "dev-owner", null, null);

        var outlierStart = clock.UtcNow;
        var outlierVisit = (await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicA, new StartVisitInput(null, null, patA.Id, docA.Id, "dental", roomA.Id, 30, true, null, catA.Id), "dev-owner", null, null)).Value;
        clock.UtcNow = outlierStart.AddMinutes(200);
        await clinicalService.CompleteVisitAsync(ownerId, clinicA, outlierVisit.Id, new CompleteVisitInput(false, null, null, null), "dev-owner", null, null);
        await outbox.ProcessPendingMessagesAsync();

        var statsWithOutlier = (await timeEngineService.ListCategoryStatsAsync(ownerId, clinicA, docA.Id, catA.Id, "dev-owner")).Value;
        statsWithOutlier[0].SampleCount.Should().Be(6);
        statsWithOutlier[0].TrimmedAverageMinutes.Should().Be(45m);

        // Toggle doctor OFF -> applied duration reverts to category default 30m in stats, Scheduling, and Availability!
        (await timeEngineService.SetDoctorEngineEnabledAsync(
            ownerId, clinicA, docA.Id, new SetDoctorTimeEngineInput(false, GuidV7.NewGuid()), "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        var statsDocOff = (await timeEngineService.ListCategoryStatsAsync(ownerId, clinicA, docA.Id, catA.Id, "dev-owner")).Value;
        statsDocOff[0].IsDoctorEngineEnabled.Should().BeFalse();
        statsDocOff[0].AppliedDurationMinutes.Should().Be(30);

        var bookedDocOff = await schedulingService.BookAppointmentAsync(
            ownerId,
            clinicA,
            new BookAppointmentInput(null, patA.Id, docA.Id, roomA.Id, catA.Id, bookingMonday.AddHours(2), null, "Normal", null, null, null),
            "dev-owner",
            null,
            null);
        bookedDocOff.IsSuccess.Should().BeTrue();
        (bookedDocOff.Value.EndUtc - bookedDocOff.Value.StartUtc).TotalMinutes.Should().Be(30);

        var slotsOff = await availabilityQueries.GetFreeSlotsAsync(
            ownerId, clinicA, catA.Id, docA.Id, bookingMonday.Date, bookingMonday.Date.AddDays(1), "dev-owner");
        slotsOff.Value.All(s => s.DurationMinutes == 30).Should().BeTrue();

        // Turn doctor back ON for widgets verification
        await timeEngineService.SetDoctorEngineEnabledAsync(
            ownerId, clinicA, docA.Id, new SetDoctorTimeEngineInput(true, GuidV7.NewGuid()), "dev-owner", null, null);

        foreach (string key in new[]
        {
            "time-engine.avg-wait",
            "time-engine.suggested-vs-applied",
            "time-engine.sample-counts",
            "time-engine.adoption-rate"
        })
        {
            var widget = await dashboardService.QueryWidgetAsync(ownerId, clinicA, key, null, null, null, "dev-owner");
            widget.IsSuccess.Should().BeTrue($"widget {key} should succeed");
            if (key == "time-engine.sample-counts")
            {
                widget.Value.PrimaryValue.Should().Be(6m);
                widget.Value.Breakdown["excusedSampleCount"].Should().Be(1m);
            }
            if (key == "time-engine.adoption-rate")
            {
                widget.Value.PrimaryValue.Should().Be(100m);
            }
        }

        // Super Admin inspect mode
        var inspect = await timeEngineService.InspectClinicTimeEngineAsync(admin.Id, clinicA);
        inspect.IsSuccess.Should().BeTrue();
        inspect.Value.Stats.Should().ContainSingle();
        inspect.Value.RecentDurations.Count.Should().BeGreaterThanOrEqualTo(7);

        // Clinic B (time-engine OFF): API returns MODULE_DISABLED while clinical & scheduling continue using category defaults cleanly
        var offStats = await timeEngineService.ListCategoryStatsAsync(ownerId, clinicB, null, null, "dev-owner");
        offStats.Error.Code.Should().Be("MODULE_DISABLED");

        var roomB = (await structureService.CreateRoomAsync(ownerId, clinicB, "B-R1", "غرفة ب", "Room B", "dental", null, "dev-owner", null, null)).Value;
        var docB = (await structureService.CreateDoctorAsync(ownerId, clinicB, null, "د. طارق", "أخصائي", "Specialist", ["dental"], [roomB.Id], 25, "dev-owner", null, null)).Value;
        await structureService.SaveWorkingHoursAsync(
            ownerId,
            clinicB,
            docB.Id,
            [new WorkingHourInput((int)DayOfWeek.Monday, "09:00", "17:00", false)],
            "dev-owner",
            null,
            null);
        var catB = (await schedulingService.CreateCategoryAsync(ownerId, clinicB, "dental", "فحص", "Checkup", 25, 200m, "EGP", true, "dev-owner", null, null)).Value;
        var patB = (await patientService.CreatePatientAsync(ownerId, clinicB, new CreatePatientInput(null, "منى هاني", "01077000002", null, new DateOnly(1996, 5, 1), null, "Female", null, null, null, "dental", false, null), "dev-owner", null, null)).Value;

        var bookedB = await schedulingService.BookAppointmentAsync(
            ownerId, clinicB, new BookAppointmentInput(null, patB.Id, docB.Id, roomB.Id, catB.Id, bookingMonday, null, "Normal", null, null, null), "dev-owner", null, null);
        bookedB.IsSuccess.Should().BeTrue();
        (bookedB.Value.EndUtc - bookedB.Value.StartUtc).TotalMinutes.Should().Be(25);
    }

    private static async Task OnboardClinicWithSlugAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        string slug,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة الاختبار", "12 شارع النيل", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, specialties.Select(s => new SpecialtySelectionInput(s, null, null)).ToList(), null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, slug, null, null);
        (await clinicsService.CompleteOnboardingAsync(ownerId, clinicId, null, null)).IsSuccess.Should().BeTrue();
    }

    private static async Task<PlatformAdmin> EnsureEnrolledAdminAsync(
        CoreDbContext db,
        IPlatformAdminAuthService adminAuth,
        IHourlyTotpService totp,
        IntegrationTestClock clock)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
        var firstLogin = await adminAuth.LoginAsync("ahmed@platform.local", "AdminInitial#2026", null, null, null, null);
        firstLogin.IsSuccess.Should().BeTrue();
        string code = totp.ComputeCurrentCode(firstLogin.Value.ManualSetupKey!, clock.UtcNow);
        var enrollRes = await adminAuth.CompleteEnrollmentAsync(firstLogin.Value.AdminId, "SuperStrongPass#2026", code, null, null);
        enrollRes.IsSuccess.Should().BeTrue();
        return await db.PlatformAdmins.FirstAsync(a => a.Id == firstLogin.Value.AdminId);
    }

    private static async Task ActivateUserFromOutboxAsync(
        OutboxService outbox,
        FakeEmailSender emailSender,
        IAuthService authService,
        string email,
        string password)
    {
        await outbox.ProcessPendingMessagesAsync();
        var sent = emailSender.SentMessages.Last(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));
        string code = Regex.Match(sent.Body, @"\b(\d{6})\b").Groups[1].Value;
        var res = await authService.ActivateAccountAsync(email, code, password, "dev-activate", "TestBrowser", null, null);
        res.IsSuccess.Should().BeTrue();
    }
}
