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
public sealed class Phase11PortalBookingTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase11PortalBookingTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_PortalBookingModule_When_TestingAvailabilityLimitsApprovalsExpiryCancellationWaitlistNoShowsAndWidgets_Then_AllRulesEnforced()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero)
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
        var portalService = sp.GetRequiredService<IPatientPortalService>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p11-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P11", ownerEmail, null, 2, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        var clinicRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة النخبة", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicId = clinicRes.Value.ClinicId;
        string slug = $"elite-p11-{clinicId:N}"[..16];
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicId, slug, ["dental"]);

        string[] modules = ["patients", "scheduling", "queue", "billing", "portal", "portal-booking"];
        await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicId,
            modules.Select(m => new ModuleToggleInput(m, true, "Phase11")).ToList(),
            "SuperStrongPass#2026",
            null,
            null);

        await structureService.SaveWorkingHoursAsync(
            ownerId, clinicId, null,
            Enumerable.Range(0, 7).Select(d => new WorkingHourInput(d, "08:00", "18:00", false)).ToList(),
            "dev-owner", null, null);

        var roomRes = await structureService.CreateRoomAsync(ownerId, clinicId, "P11-R1", "عيادة 1", "Room 1", "dental", null, "dev-owner", null, null);
        Guid roomId = roomRes.Value.Id;

        var docOnlineRes = await structureService.CreateDoctorAsync(
            ownerId, clinicId, null, "د. عمر سالم", "استشاري", "Consultant", ["dental"], [roomId], 30, "dev-owner", null, null);
        Guid docOnlineId = docOnlineRes.Value.Id;

        var docOfflineRes = await structureService.CreateDoctorAsync(
            ownerId, clinicId, null, "د. ليلى حسن", "أخصائي", "Specialist", ["dental"], [], 30, "dev-owner", null, null);
        Guid docOfflineId = docOfflineRes.Value.Id;

        (await schedulingService.UpdateDoctorBookingSettingAsync(ownerId, clinicId, docOnlineId, true, 4, "dev-owner", null, null)).IsSuccess.Should().BeTrue();
        (await schedulingService.UpdateDoctorBookingSettingAsync(ownerId, clinicId, docOfflineId, false, null, "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        var catOnlineRes = await schedulingService.CreateCategoryAsync(
            ownerId, clinicId, "dental", "كشف أسنان", "Dental Exam", 30, 450m, "EGP", true, "dev-owner", null, null);
        Guid catOnlineId = catOnlineRes.Value.Id;

        var catOfflineRes = await schedulingService.CreateCategoryAsync(
            ownerId, clinicId, "dental", "جراحة معقدة", "Complex Surgery", 60, 1500m, "EGP", false, "dev-owner", null, null);
        Guid catOfflineId = catOfflineRes.Value.Id;

        // Seed 2 activated patients
        (Guid patAId, Guid accAId) = await CreateAndActivatePatientAsync(
            patientService, portalService, outbox, emailSender, ownerId, clinicId, slug, "أحمد فؤاد", "01020001001");
        (Guid patBId, Guid accBId) = await CreateAndActivatePatientAsync(
            patientService, portalService, outbox, emailSender, ownerId, clinicId, slug, "منى علي", "01020001002");

        // 1. Availability shows only BookableOnline category + AcceptsOnlineBooking doctor, price visible by default
        DateTimeOffset fromUtc = new(2026, 8, 12, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset toUtc = new(2026, 8, 12, 13, 0, 0, TimeSpan.Zero);
        var avail = await portalService.GetPortalAvailabilityAsync(accAId, clinicId, patAId, slug, catOnlineId, null, fromUtc, toUtc);
        avail.IsSuccess.Should().BeTrue();
        avail.Value.Categories.Should().ContainSingle(c => c.CategoryId == catOnlineId && c.PriceAmount == 450m);
        avail.Value.Doctors.Should().ContainSingle(d => d.DoctorId == docOnlineId && d.DisplayName == "د. عمر سالم");
        avail.Value.Slots.Should().NotBeEmpty();
        avail.Value.Slots.All(s => s.DoctorId == docOnlineId).Should().BeTrue();

        // Trying offline category or offline doctor must be rejected
        var offCatRes = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOnlineId, catOfflineId, fromUtc, null, GuidV7.NewGuid()), null, null);
        offCatRes.Error.Code.Should().Be("ONLINE_BOOKING_DISABLED");

        var offDocRes = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOfflineId, catOnlineId, fromUtc, null, GuidV7.NewGuid()), null, null);
        offDocRes.Error.Code.Should().Be("ONLINE_BOOKING_DISABLED");

        // 2. AutoConfirm booking, OperationId idempotency, SLOT_TAKEN conflict, MaxActiveFutureBookingsPerPatient (default 2)
        Guid op1 = GuidV7.NewGuid();
        var book1 = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, fromUtc, "First online booking", op1), null, null);
        book1.IsSuccess.Should().BeTrue();
        book1.Value.Status.Should().Be("Booked");

        var book1Replay = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, fromUtc, "First online booking", op1), null, null);
        book1Replay.IsSuccess.Should().BeTrue();
        book1Replay.Value.AppointmentId.Should().Be(book1.Value.AppointmentId);

        var slotTaken = await portalService.BookAppointmentFromPortalAsync(
            accBId, clinicId, patBId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, fromUtc, null, GuidV7.NewGuid()), null, null);
        slotTaken.IsFailure.Should().BeTrue();
        slotTaken.Error.Code.Should().Be("SLOT_TAKEN");

        var book2 = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, fromUtc.AddHours(1), null, GuidV7.NewGuid()), null, null);
        book2.IsSuccess.Should().BeTrue();

        var overLimit = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, fromUtc.AddHours(2), null, GuidV7.NewGuid()), null, null);
        overLimit.IsFailure.Should().BeTrue();
        overLimit.Error.Code.Should().Be("MAX_ACTIVE_FUTURE_BOOKINGS_REACHED");

        // 3. Cancellation isolation + cutoff enforcement + waitlist offer trigger
        var crossPatCancel = await portalService.CancelAppointmentFromPortalAsync(accBId, clinicId, patBId, slug, book1.Value.AppointmentId, "Hack", null, null);
        crossPatCancel.Error.Code.Should().Be("APPOINTMENT_NOT_FOUND");

        // Add patient B to waitlist for 2026-08-12 so cancellation triggers SlotOffer
        await schedulingService.AddToWaitingListAsync(ownerId, clinicId, patBId, docOnlineId, catOnlineId, "2026-08-12", "Early slot", GuidV7.NewGuid(), "dev-owner", null, null);

        var cancelBook2 = await portalService.CancelAppointmentFromPortalAsync(accAId, clinicId, patAId, slug, book2.Value.AppointmentId, "Schedule conflict", null, null);
        cancelBook2.IsSuccess.Should().BeTrue();
        cancelBook2.Value.Status.Should().Be("Cancelled");

        var offers = await schedulingService.ListSlotOffersAsync(ownerId, clinicId, "dev-owner");
        offers.Value.Should().Contain(o => o.PatientId == patBId && o.SlotStartUtc == fromUtc.AddHours(1));

        // Move clock within 12h cutoff of book1 -> cancellation blocked
        clock.UtcNow = fromUtc.AddHours(-6);
        var tooLateCancel = await portalService.CancelAppointmentFromPortalAsync(accAId, clinicId, patAId, slug, book1.Value.AppointmentId, "Too late", null, null);
        tooLateCancel.Error.Code.Should().Be("CANCELLATION_CUTOFF_PASSED");

        // 4. RequiresApproval mode: pending request holds slot until approved, declined, or expired
        (await portalService.UpdateClinicPortalBookingSettingsAsync(
            ownerId,
            clinicId,
            new UpdateClinicPortalBookingSettingInput("RequiresApproval", 60, 30, 30, 5, 20, 12, 3, 2, true),
            "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        DateTimeOffset day3Slot = fromUtc.AddDays(3); // 2026-08-15 08:00Z
        var req1 = await portalService.BookAppointmentFromPortalAsync(
            accBId, clinicId, patBId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, day3Slot, "Needs approval", GuidV7.NewGuid()), null, null);
        req1.IsSuccess.Should().BeTrue();
        req1.Value.Status.Should().Be("Requested");

        // Slot held while Requested
        var reqConflict = await portalService.BookAppointmentFromPortalAsync(
            accAId, clinicId, patAId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, day3Slot, null, GuidV7.NewGuid()), null, null);
        reqConflict.Error.Code.Should().Be("SLOT_TAKEN");

        var pendingReqs = await portalService.ListPortalBookingRequestsAsync(ownerId, clinicId, "Requested", "dev-owner");
        pendingReqs.Value.Should().ContainSingle(r => r.AppointmentId == req1.Value.AppointmentId && !r.IsExpired);

        var approved = await portalService.ApprovePortalBookingRequestAsync(ownerId, clinicId, req1.Value.AppointmentId, "Confirmed by reception", "dev-owner", null, null);
        approved.IsSuccess.Should().BeTrue();
        approved.Value.Status.Should().Be("Booked");

        // Second request -> Declined -> frees slot
        var req2 = await portalService.BookAppointmentFromPortalAsync(
            accBId, clinicId, patBId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, day3Slot.AddHours(1), null, GuidV7.NewGuid()), null, null);
        req2.Value.Status.Should().Be("Requested");

        var declined = await portalService.DeclinePortalBookingRequestAsync(ownerId, clinicId, req2.Value.AppointmentId, "Doctor off hour", "dev-owner", null, null);
        declined.Value.Status.Should().Be("Declined");

        // Third request -> hold expires after 2h -> slot opens cleanly and Approve returns REQUEST_EXPIRED
        var req3 = await portalService.BookAppointmentFromPortalAsync(
            accBId, clinicId, patBId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, day3Slot.AddHours(1), null, GuidV7.NewGuid()), null, null);
        req3.Value.Status.Should().Be("Requested");

        clock.UtcNow = clock.UtcNow.AddHours(3); // past 2h hold
        var expiredApprove = await portalService.ApprovePortalBookingRequestAsync(ownerId, clinicId, req3.Value.AppointmentId, "Too late", "dev-owner", null, null);
        expiredApprove.Error.Code.Should().Be("REQUEST_EXPIRED");

        // 5. Three No-Shows auto-block online booking -> manual unblock restores booking
        for (int i = 0; i < 3; i++)
        {
            DateTimeOffset nsStart = new(clock.UtcNow.UtcDateTime.Date.AddDays(5 + i).AddHours(9), TimeSpan.Zero);
            var staffAppt = await schedulingService.BookAppointmentAsync(
                ownerId, clinicId,
                new BookAppointmentInput(null, patBId, docOnlineId, roomId, catOnlineId, nsStart, nsStart.AddMinutes(30), "Normal", null, $"NoShow-{i}", null),
                "dev-owner", null, null);
            staffAppt.IsSuccess.Should().BeTrue(staffAppt.IsFailure ? staffAppt.Error.Code : null);
            (await schedulingService.TransitionAppointmentStatusAsync(ownerId, clinicId, staffAppt.Value.Id, "NoShow", "Absent", "dev-owner", null, null)).IsSuccess.Should().BeTrue();
        }

        var adminPatStatus = await portalService.GetPatientPortalAdminStatusAsync(ownerId, clinicId, patBId, "dev-owner");
        adminPatStatus.Value.NoShowCount.Should().Be(3);
        adminPatStatus.Value.IsOnlineBookingBlocked.Should().BeTrue();

        var blockedAttempt = await portalService.BookAppointmentFromPortalAsync(
            accBId, clinicId, patBId, slug, new PortalBookAppointmentInput(docOnlineId, catOnlineId, day3Slot.AddDays(4), null, GuidV7.NewGuid()), null, null);
        blockedAttempt.Error.Code.Should().Be("PATIENT_ONLINE_BOOKING_BLOCKED");

        var unblocked = await portalService.SetPatientOnlineBookingBlockAsync(ownerId, clinicId, patBId, false, null, "dev-owner", null, null);
        unblocked.Value.IsOnlineBookingBlocked.Should().BeFalse();

        // 6. All 6 dashboard widgets + Super Admin inspect + module-off check
        string[] widgetKeys =
        [
            "portal-booking.daily-count",
            "portal-booking.cancellations",
            "portal-booking.pending-requests",
            "portal-booking.no-shows",
            "portal-booking.slot-utilization",
            "portal-booking.portal-share"
        ];
        foreach (string key in widgetKeys)
        {
            var w = await dashboardService.QueryWidgetAsync(ownerId, clinicId, key, clock.UtcNow.AddDays(-10), clock.UtcNow.AddDays(30), null, "dev-owner");
            w.IsSuccess.Should().BeTrue($"widget {key} should succeed");
        }

        var inspectBooking = await portalService.InspectClinicPortalBookingAsync(admin.Id.ToString(), clinicId);
        inspectBooking.IsSuccess.Should().BeTrue();
        inspectBooking.Value.Settings.Mode.Should().Be("RequiresApproval");

        // Turn portal-booking OFF -> availability & booking return MODULE_DISABLED
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicId, [new ModuleToggleInput("portal-booking", false, "Disable booking")], "SuperStrongPass#2026", null, null);

        var offAvail = await portalService.GetPortalAvailabilityAsync(accAId, clinicId, patAId, slug, catOnlineId, null, null, null);
        offAvail.Error.Code.Should().Be("MODULE_DISABLED");
    }

    private static async Task<(Guid PatientId, Guid AccountId)> CreateAndActivatePatientAsync(
        IPatientService patientService,
        IPatientPortalService portalService,
        OutboxService outbox,
        FakeEmailSender emailSender,
        Guid ownerId,
        Guid clinicId,
        string slug,
        string fullName,
        string phone)
    {
        var patRes = await patientService.CreatePatientAsync(
            ownerId, clinicId,
            new CreatePatientInput(null, fullName, phone, null, new DateOnly(1992, 5, 1), null, "Male", null, null, null, "dental", false, null),
            "dev-owner", null, null);
        patRes.IsSuccess.Should().BeTrue();
        Guid patientId = patRes.Value.Id;

        string email = $"pat-{Guid.NewGuid():N}@patient.local";
        (await portalService.InvitePatientAsync(
            ownerId, clinicId, patientId,
            new InvitePatientToPortalInput(email, false, null, true, "v1", false),
            "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        await outbox.ProcessPendingMessagesAsync();
        string code = Regex.Match(emailSender.SentMessages.Last(m => m.To == email).Body, @"\b(\d{6})\b").Groups[1].Value;
        var act = await portalService.ActivateWithEmailCodeAsync(slug, new ActivatePatientPortalInput(email, code, "PatPass#2026"), null, null);
        act.IsSuccess.Should().BeTrue();
        return (patientId, act.Value.PatientAccountId);
    }

    private static async Task OnboardClinicWithSlugAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        string slug,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة النخبة", "شارع النيل", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
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
