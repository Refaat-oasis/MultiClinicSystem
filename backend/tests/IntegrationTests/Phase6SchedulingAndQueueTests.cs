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
public sealed class Phase6SchedulingAndQueueTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase6SchedulingAndQueueTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_SchedulingAndQueueModules_When_BookingArrivingPushingVipCancellingClosingAndToggling_Then_EnforcesAllDomainAndConcurrencyInvariants()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 6, 1, 6, 0, 0, TimeSpan.Zero) // Monday
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
        var authzService = sp.GetRequiredService<IClinicAuthorizationService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var availabilityQueries = sp.GetRequiredService<IAvailabilityQueries>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p6-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P6", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");

        Guid ownerId = ownerRes.Value.UserId;
        var clinicRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة المواعيد", "UTC", "EGP", "NearestPound", "dev-owner", null, null);
        Guid clinicId = clinicRes.Value.ClinicId;

        // Finish onboarding so clinic operates normally
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة المواعيد", "شارع 1", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, [new SpecialtySelectionInput("dental", null, null), new SpecialtySelectionInput("dermatology", null, null)], null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, $"p6-clinic-{clinicId:N}"[..20], null, null);
        (await clinicsService.CompleteOnboardingAsync(ownerId, clinicId, null, null)).IsSuccess.Should().BeTrue();

        // Enable patients + scheduling + queue
        var enableModules = await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicId,
            [
                new ModuleToggleInput("patients", true, "P6"),
                new ModuleToggleInput("scheduling", true, "P6"),
                new ModuleToggleInput("queue", true, "P6")
            ],
            "SuperStrongPass#2026",
            null,
            null);
        enableModules.IsSuccess.Should().BeTrue();

        // Set clinic working hours: Monday (1) 09:00-13:00
        var whRes = await structureService.SaveWorkingHoursAsync(
            ownerId,
            clinicId,
            null,
            [
                new WorkingHourInput((int)DayOfWeek.Monday, "09:00", "13:00", false)
            ],
            "dev-owner",
            null,
            null);
        whRes.IsSuccess.Should().BeTrue();

        // Create room & two doctors in two specialties
        var room1 = await structureService.CreateRoomAsync(ownerId, clinicId, "R1", "غرفة 1", "Room 1", "dental", null, "dev-owner", null, null);
        var room2 = await structureService.CreateRoomAsync(ownerId, clinicId, "R2", "غرفة 2", "Room 2", "dermatology", null, "dev-owner", null, null);

        string doc1Email = $"p6-doc1-{Guid.NewGuid():N}@clinic.local";
        var doc1User = await authzService.InviteStaffAsync(ownerId, clinicId, "Dr One", doc1Email, "Doctor", "dev-owner", null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, doc1Email, "DocPass#2026");

        string doc2Email = $"p6-doc2-{Guid.NewGuid():N}@clinic.local";
        var doc2User = await authzService.InviteStaffAsync(ownerId, clinicId, "Dr Two", doc2Email, "Doctor", "dev-owner", null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, doc2Email, "DocPass#2026");

        var doc1 = await structureService.CreateDoctorAsync(ownerId, clinicId, doc1User.Value.UserId, "د. واحد", "Dr One", "Doctor", ["dental"], [room1.Value.Id], 30, "dev-owner", null, null);
        var doc2 = await structureService.CreateDoctorAsync(ownerId, clinicId, doc2User.Value.UserId, "د. اثنان", "Dr Two", "Doctor", ["dermatology"], [room2.Value.Id], 30, "dev-owner", null, null);

        // Doctor 1 override hours: Monday 10:00-12:00 only
        await structureService.SaveWorkingHoursAsync(
            ownerId,
            clinicId,
            doc1.Value.Id,
            [new WorkingHourInput((int)DayOfWeek.Monday, "10:00", "12:00", false)],
            "dev-owner",
            null,
            null);

        // Approve doctor devices
        var devices = await authzService.ListClinicDevicesAsync(ownerId, clinicId, "dev-owner");
        foreach (var d in devices.Value.Where(x => !x.IsApproved))
        {
            await authzService.ApproveClinicDeviceAsync(ownerId, clinicId, d.DeviceId, "dev-owner", null, null);
        }

        // Create appointment category (30 min, 250 EGP) & configure settings (VipJumpLimit=1, MaxQueuePushes=1, Step=30)
        var catRes = await schedulingService.CreateCategoryAsync(
            ownerId, clinicId, "dental", "كشف أسنان", "Dental Exam", 30, 250m, "EGP", false, "dev-owner", null, null);
        catRes.IsSuccess.Should().BeTrue();

        await schedulingService.UpdateSchedulingSettingsAsync(
            ownerId, clinicId, vipJumpLimit: 1, lateGraceMinutes: 10, maxQueuePushes: 1,
            defaultSlotStepMinutes: 30, minBookingLeadMinutes: 0, rescheduleOptionsJson: null,
            "dev-owner", null, null);

        // Register 4 patients
        var p1 = await patientService.CreatePatientAsync(ownerId, clinicId, new CreatePatientInput(null, "مريض أول", "01010000001", null, null, 30, "Male", null, null, null, null, false, null), "dev-owner", null, null);
        var p2 = await patientService.CreatePatientAsync(ownerId, clinicId, new CreatePatientInput(null, "مريض ثاني", "01010000002", null, null, 25, "Female", null, null, null, null, false, null), "dev-owner", null, null);
        var p3 = await patientService.CreatePatientAsync(ownerId, clinicId, new CreatePatientInput(null, "مريض ثالث", "01010000003", null, null, 40, "Male", null, null, null, null, false, null), "dev-owner", null, null);
        var p4 = await patientService.CreatePatientAsync(ownerId, clinicId, new CreatePatientInput(null, "مريض رابع", "01010000004", null, null, 35, "Female", null, null, null, null, false, null), "dev-owner", null, null);

        // Working hour validation: booking doc1 at 09:00 must fail because doc1 override starts at 10:00
        DateTimeOffset nineAm = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var outsideOverride = await schedulingService.BookAppointmentAsync(
            ownerId, clinicId,
            new BookAppointmentInput(null, p1.Value.Id, doc1.Value.Id, null, catRes.Value.Id, nineAm, null, "Normal", null, null, null),
            "dev-owner", null, null);
        outsideOverride.IsFailure.Should().BeTrue();
        outsideOverride.Error.Code.Should().Be("OUTSIDE_WORKING_HOURS");

        // Free-slot invariant: get free slots for doc1 on Monday -> book the first slot -> verify it disappears from free slots
        DateTimeOffset dayStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset dayEnd = new(2026, 6, 1, 23, 59, 0, TimeSpan.Zero);
        var freeBefore = await availabilityQueries.GetFreeSlotsAsync(ownerId, clinicId, catRes.Value.Id, doc1.Value.Id, dayStart, dayEnd, "dev-owner");
        freeBefore.IsSuccess.Should().BeTrue();
        freeBefore.Value.Select(s => s.StartUtc.Hour * 100 + s.StartUtc.Minute).Should().Equal([1000, 1030, 1100, 1130]);

        DateTimeOffset firstSlot = freeBefore.Value[0].StartUtc; // 10:00
        var concurrentA = Task.Run(async () =>
        {
            var (pA, _) = TestServiceFactory.CreateServices(new CurrentClinicContext(), clock);
            using var sA = pA.CreateScope();
            var svcA = sA.ServiceProvider.GetRequiredService<ISchedulingService>();
            return await svcA.BookAppointmentAsync(
                ownerId, clinicId,
                new BookAppointmentInput(null, p1.Value.Id, doc1.Value.Id, null, catRes.Value.Id, firstSlot, null, "Normal", null, null, null),
                "dev-owner", null, null);
        });
        var concurrentB = Task.Run(async () =>
        {
            var (pB, _) = TestServiceFactory.CreateServices(new CurrentClinicContext(), clock);
            using var sB = pB.CreateScope();
            var svcB = sB.ServiceProvider.GetRequiredService<ISchedulingService>();
            return await svcB.BookAppointmentAsync(
                ownerId, clinicId,
                new BookAppointmentInput(null, p2.Value.Id, doc1.Value.Id, null, catRes.Value.Id, firstSlot, null, "Normal", null, null, null),
                "dev-owner", null, null);
        });

        var concurrentResults = await Task.WhenAll(concurrentA, concurrentB);
        concurrentResults.Count(r => r.IsSuccess).Should().Be(1);
        concurrentResults.Count(r => r.IsFailure && r.Error.Code == "APPOINTMENT_CONFLICT").Should().Be(1);

        var freeAfter = await availabilityQueries.GetFreeSlotsAsync(ownerId, clinicId, catRes.Value.Id, doc1.Value.Id, dayStart, dayEnd, "dev-owner");
        freeAfter.Value.Should().NotContain(s => s.StartUtc == firstSlot);

        // Doctor scoping: book appointment for doc2; verify doc1 cannot list or view doc2's appointment (404)
        var doc2Booking = await schedulingService.BookAppointmentAsync(
            ownerId, clinicId,
            new BookAppointmentInput(null, p4.Value.Id, doc2.Value.Id, null, catRes.Value.Id, nineAm, null, "Normal", null, null, null),
            "dev-owner", null, null);
        doc2Booking.IsSuccess.Should().BeTrue();

        var doc1List = await schedulingService.ListAppointmentsAsync(doc1User.Value.UserId, clinicId, null, null, null, null, "dev-activate");
        doc1List.IsSuccess.Should().BeTrue();
        doc1List.Value.Should().OnlyContain(a => a.DoctorId == doc1.Value.Id);

        var doc1ReadDoc2 = await schedulingService.GetAppointmentAsync(doc1User.Value.UserId, clinicId, doc2Booking.Value.Id, "dev-activate");
        doc1ReadDoc2.IsFailure.Should().BeTrue();
        doc1ReadDoc2.Error.Code.Should().Be("APPOINTMENT_NOT_FOUND");

        // Priority permission enforcement: doc1 (lacks scheduling.priority) cannot book VIP -> rejected
        var deniedVip = await schedulingService.BookAppointmentAsync(
            doc1User.Value.UserId, clinicId,
            new BookAppointmentInput(null, p3.Value.Id, doc1.Value.Id, null, catRes.Value.Id, freeAfter.Value[0].StartUtc, null, "Vip", null, null, null),
            "dev-activate", null, null);
        deniedVip.IsFailure.Should().BeTrue();
        deniedVip.Error.Code.Should().Be("PRIORITY_CLAIM_REQUIRED");

        // Waiting queue + late push-back + VIP jump limit (owner has scheduling.priority)
        var appt1 = concurrentResults.Single(r => r.IsSuccess).Value;
        Guid secondNormalPatientId = appt1.PatientId == p1.Value.Id ? p2.Value.Id : p1.Value.Id;

        var bookedNormal2 = await schedulingService.BookAppointmentAsync(
            ownerId, clinicId,
            new BookAppointmentInput(null, secondNormalPatientId, doc1.Value.Id, null, catRes.Value.Id, new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero), null, "Normal", null, null, null),
            "dev-owner", null, null);
        var bookedVip = await schedulingService.BookAppointmentAsync(
            ownerId, clinicId,
            new BookAppointmentInput(null, p3.Value.Id, doc1.Value.Id, null, catRes.Value.Id, new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), null, "Vip", null, null, null),
            "dev-owner", null, null);

        await schedulingService.TransitionAppointmentStatusAsync(ownerId, clinicId, appt1.Id, "Arrived", null, "dev-owner", null, null);
        await schedulingService.TransitionAppointmentStatusAsync(ownerId, clinicId, bookedNormal2.Value.Id, "Arrived", null, "dev-owner", null, null);
        await schedulingService.TransitionAppointmentStatusAsync(ownerId, clinicId, bookedVip.Value.Id, "Arrived", null, "dev-owner", null, null);

        // With VipJumpLimit=1, queue order for doc1 must be [appt1.PatientId (Normal), p3 (Vip jumped 1 spot over secondNormalPatientId), secondNormalPatientId (Normal)]
        var board = await schedulingService.GetQueueBoardAsync(ownerId, clinicId, doc1.Value.Id, "2026-06-01", "dev-owner");
        board.IsSuccess.Should().BeTrue();
        board.Value.Select(x => x.PatientId).Should().Equal([appt1.PatientId, p3.Value.Id, secondNormalPatientId]);
        board.Value.Single(x => x.PatientId == p3.Value.Id).VipJumpCount.Should().Be(1);

        // Push p1 (currently #1) back once (within MaxQueuePushes=1) -> moves to #2 behind p3
        Guid p1QueueId = board.Value[0].Id;
        var push1 = await schedulingService.PushLateQueueEntryAsync(ownerId, clinicId, p1QueueId, true, "dev-owner", null, null);
        push1.IsSuccess.Should().BeTrue();
        push1.Value.OrderIndex.Should().Be(2);
        push1.Value.PushCount.Should().Be(1);

        // Second push exceeds MaxQueuePushes=1 -> becomes NoShow!
        var push2 = await schedulingService.PushLateQueueEntryAsync(ownerId, clinicId, p1QueueId, true, "dev-owner", null, null);
        push2.IsSuccess.Should().BeTrue();
        push2.Value.Status.Should().Be("NoShow");

        // Waiting list: p4 wants 2026-06-01 with doc1 -> cancel doc1 11:30 slot -> SlotOffer generated -> AcceptSlotOffer books p4!
        DateTimeOffset slot1130 = new(2026, 6, 1, 11, 30, 0, TimeSpan.Zero);
        var appt1130 = await schedulingService.BookAppointmentAsync(
            ownerId, clinicId,
            new BookAppointmentInput(null, p1.Value.Id, doc1.Value.Id, null, catRes.Value.Id, slot1130, null, "Normal", null, null, null),
            "dev-owner", null, null);
        appt1130.IsSuccess.Should().BeTrue();

        var wl = await schedulingService.AddToWaitingListAsync(ownerId, clinicId, p4.Value.Id, doc1.Value.Id, catRes.Value.Id, "2026-06-01", "Any freed slot", null, "dev-owner", null, null);
        wl.IsSuccess.Should().BeTrue();

        var cancelled = await schedulingService.TransitionAppointmentStatusAsync(ownerId, clinicId, appt1130.Value.Id, "Cancelled", "Patient cancelled", "dev-owner", null, null);
        cancelled.IsSuccess.Should().BeTrue();

        var offers = await schedulingService.ListSlotOffersAsync(ownerId, clinicId, "dev-owner");
        offers.Value.Should().ContainSingle(o => o.PatientId == p4.Value.Id && o.Status == "Pending" && o.SlotStartUtc == slot1130);

        var accepted = await schedulingService.AcceptSlotOfferAsync(ownerId, clinicId, offers.Value[0].Id, "dev-owner", null, null);
        accepted.IsSuccess.Should().BeTrue();
        accepted.Value.PatientId.Should().Be(p4.Value.Id);
        accepted.Value.StartUtc.Should().Be(slot1130);

        // Close day cascade & reschedule after_week (also configure Next Monday 2026-06-08 working hours already covered by DayOfWeek.Monday!)
        var closeRes = await schedulingService.CloseDayAsync(ownerId, clinicId, null, "2026-06-01", "2026-06-01", "عطلة", "Holiday", "dev-owner", null, null);
        closeRes.IsSuccess.Should().BeTrue();
        closeRes.Value.AffectedAppointments.Should().Contain(a => a.Id == accepted.Value.Id);

        var rescheduled = await schedulingService.RescheduleAppointmentAsync(
            ownerId, clinicId, accepted.Value.Id,
            new RescheduleAppointmentInput("after_week", null, null, null, null, null),
            "dev-owner", null, null);
        rescheduled.IsSuccess.Should().BeTrue();
        rescheduled.Value.StartUtc.Should().Be(slot1130.AddDays(7));

        // Dashboard widgets match live aggregates
        var schedWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicId, "scheduling.appointments-summary", dayStart.AddDays(-1), dayEnd.AddDays(10), null, "dev-owner");
        schedWidget.IsSuccess.Should().BeTrue();
        schedWidget.Value.PrimaryValue.Should().BeGreaterThanOrEqualTo(4m);

        var queueWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicId, "queue.waiting-count", dayStart.AddDays(-1), dayEnd.AddDays(10), null, "dev-owner");
        queueWidget.IsSuccess.Should().BeTrue();
        queueWidget.Value.Breakdown.Should().NotContainKey("offersAccepted"); // offersAccepted is in queue.waiting-list
        var wlWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicId, "queue.waiting-list", dayStart.AddDays(-1), dayEnd.AddDays(10), null, "dev-owner");
        wlWidget.IsSuccess.Should().BeTrue();
        wlWidget.Value.Breakdown["offersAccepted"].Should().Be(1m);

        // Turn queue module OFF -> /queue operations return MODULE_DISABLED, scheduling continues working
        await adminService.UpdateClinicModulesAsync(admin.Id, clinicId, [new ModuleToggleInput("queue", false, "Off")], "SuperStrongPass#2026", null, null);
        var queueOffBoard = await schedulingService.GetQueueBoardAsync(ownerId, clinicId, doc1.Value.Id, "2026-06-01", "dev-owner");
        queueOffBoard.IsFailure.Should().BeTrue();
        queueOffBoard.Error.Code.Should().Be("MODULE_DISABLED");

        var listStillWorks = await schedulingService.ListAppointmentsAsync(ownerId, clinicId, null, null, null, null, "dev-owner");
        listStillWorks.IsSuccess.Should().BeTrue();
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
