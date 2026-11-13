using System.Text.Json;
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
public sealed class Phase15OfflineSyncTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase15OfflineSyncTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_TwoClinics_When_PushingOfflineBatchesConflictsPortalClashLwwSkewAndPull_Then_EnforcesIdempotencyIsolationAndWidgets()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero)
        };
        var currentClinic = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(currentClinic, clock);

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
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var portalService = sp.GetRequiredService<IPatientPortalService>();
        var syncService = sp.GetRequiredService<ISyncService>();
        var dashboard = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p15-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P15", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        const string device1 = "dev-activate";
        const string device2 = "dev-second";

        // Register & approve device2 on owner account
        var login2 = await authService.LoginAsync(ownerEmail, "OwnerPass#2026", device2, "Reception Tablet 2", null, null);
        login2.IsSuccess.Should().BeTrue();
        AppDevice dev2Rec = await db.AppDevices.FirstAsync(d => d.UserId == ownerId && d.DeviceId == device2);
        dev2Rec.Approve(clock.UtcNow);
        await db.SaveChangesAsync();

        // Setup Clinic A & Clinic B
        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "Phase 15 Clinic A", "UTC", "EGP", "None", device1, null, null);
        Guid clinicA = clinicARes.Value.ClinicId;
        string slugA = $"p15-a-{clinicA:N}"[..16];
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicA, slugA, ["dental"]);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "Phase 15 Clinic B", "UTC", "EGP", "None", device1, null, null);
        Guid clinicB = clinicBRes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicB, $"p15-b-{clinicB:N}"[..16], ["dental"]);

        string[] modulesOnA = ["patients", "scheduling", "clinical", "billing", "portal", "portal-booking", "offline-sync"];
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicA,
            modulesOnA.Select(m => new ModuleToggleInput(m, true, "Phase15")).ToArray(),
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        // Clinic B intentionally leaves offline-sync OFF first to verify MODULE_DISABLED
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicB,
            [new ModuleToggleInput("patients", true, "Phase15")],
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        var disabledRes = await syncService.GetSettingsAsync(ownerId, clinicB, device1);
        disabledRes.IsFailure.Should().BeTrue();
        disabledRes.Error.Code.Should().Be("MODULE_DISABLED");

        // Enable offline-sync on Clinic B too so we can test tenant pull isolation
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicB,
            [new ModuleToggleInput("offline-sync", true, "Phase15")],
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        // Pre-allocate distinct receipt blocks for Device 1 and Device 2
        var block1Res = await syncService.AllocateNumberBlockAsync(ownerId, clinicA, new AllocateNumberBlockInput(device1, "Receipt", 50), null, null);
        var block2Res = await syncService.AllocateNumberBlockAsync(ownerId, clinicA, new AllocateNumberBlockInput(device2, "Receipt", 50), null, null);
        block1Res.IsSuccess.Should().BeTrue();
        block2Res.IsSuccess.Should().BeTrue();
        block1Res.Value.ToNumber.Should().BeLessThan(block2Res.Value.FromNumber);

        // Configure working hours, room, doctor, category, and portal patient in Clinic A
        (await structureService.SaveWorkingHoursAsync(
            ownerId, clinicA, null,
            Enumerable.Range(0, 7).Select(d => new WorkingHourInput(d, "08:00", "18:00", false)).ToList(),
            device1, null, null)).IsSuccess.Should().BeTrue();
        var roomA = (await structureService.CreateRoomAsync(ownerId, clinicA, "P15-R1", "غرفة 15", "Room 15", "dental", null, device1, null, null)).Value;
        var docA = (await structureService.CreateDoctorAsync(ownerId, clinicA, null, "Dr. Salma Nabil", "استشاري", "Consultant", ["dental"], [roomA.Id], 30, device1, null, null)).Value;
        var catA = (await schedulingService.CreateCategoryAsync(ownerId, clinicA, "dental", "كشف", "Consultation", 30, 500m, "EGP", true, device1, null, null)).Value;
        (await schedulingService.UpdateDoctorBookingSettingAsync(ownerId, clinicA, docA.Id, true, 4, device1, null, null)).IsSuccess.Should().BeTrue();

        var portalPatientRes = await patientService.CreatePatientAsync(
            ownerId, clinicA,
            new CreatePatientInput(null, "طارق حسن", "01066660001", null, null, 34, "Male", null, null, null, "dental", false, null),
            device1, null, null);
        portalPatientRes.IsSuccess.Should().BeTrue();

        string portalEmail = $"p15-pat-{Guid.NewGuid():N}@portal.local";
        (await portalService.InvitePatientAsync(
            ownerId, clinicA, portalPatientRes.Value.Id,
            new InvitePatientToPortalInput(portalEmail, false, null, true, "v1", false),
            device1, null, null)).IsSuccess.Should().BeTrue();
        await outbox.ProcessPendingMessagesAsync();
        string patCode = Regex.Match(emailSender.SentMessages.Last(m => m.To == portalEmail).Body, @"\b(\d{6})\b").Groups[1].Value;
        var patAct = await portalService.ActivateWithEmailCodeAsync(slugA, new ActivatePatientPortalInput(portalEmail, patCode, "PatPass#2026"), null, null);
        patAct.IsSuccess.Should().BeTrue();

        // 1. Offline batch with a middle failing op + online-only rejection + large clock skew
        Guid offlinePatientId = GuidV7.NewGuid();
        Guid registerOpId = GuidV7.NewGuid();
        Guid badMidOpId = GuidV7.NewGuid();
        Guid onlineOnlyOpId = GuidV7.NewGuid();
        Guid chargeId = GuidV7.NewGuid();
        Guid chargeOpId = GuidV7.NewGuid();
        Guid payOpId = GuidV7.NewGuid();

        var batch1 = new SyncPushBatchInput(
            device1,
            [
                new SyncPushOperationInput(
                    registerOpId,
                    "RegisterPatient",
                    "Patient",
                    offlinePatientId,
                    JsonSerializer.Serialize(new { clientPatientId = offlinePatientId, fullName = "كريم عادل", phone = "01066660002", gender = "Male", confirmDuplicateCreation = true }),
                    clock.UtcNow.AddMinutes(-30)),
                new SyncPushOperationInput(
                    badMidOpId,
                    "RecordPayment",
                    "Payment",
                    GuidV7.NewGuid(),
                    JsonSerializer.Serialize(new { patientId = Guid.NewGuid(), isDeposit = false, splits = new[] { new { method = 0, amount = 100m } } }),
                    clock.UtcNow.AddMinutes(-2)),
                new SyncPushOperationInput(
                    onlineOnlyOpId,
                    "CloseDay",
                    "DailyClosing",
                    GuidV7.NewGuid(),
                    "{}",
                    clock.UtcNow.AddMinutes(-1)),
                new SyncPushOperationInput(
                    chargeOpId,
                    "CreateCharge",
                    "Charge",
                    chargeId,
                    JsonSerializer.Serialize(new { id = chargeId, patientId = offlinePatientId, doctorId = docA.Id, standardSessionPriceAmount = 500m, isFreeSession = false, postImmediately = true }),
                    clock.UtcNow),
                new SyncPushOperationInput(
                    payOpId,
                    "RecordPayment",
                    "Payment",
                    GuidV7.NewGuid(),
                    JsonSerializer.Serialize(new { patientId = offlinePatientId, chargeId, isDeposit = false, splits = new[] { new { method = 0, amount = 500m } } }),
                    clock.UtcNow)
            ]);

        var push1Res = await syncService.PushAsync(ownerId, clinicA, batch1, null, null);
        push1Res.IsSuccess.Should().BeTrue();
        push1Res.Value.AppliedCount.Should().Be(3);
        push1Res.Value.RejectedCount.Should().Be(2);
        push1Res.Value.Outcomes.Single(o => o.OperationId == registerOpId).IsLargeClockSkew.Should().BeTrue();
        push1Res.Value.Outcomes.Single(o => o.OperationId == onlineOnlyOpId).ErrorCode.Should().Be("ONLINE_ONLY_ACTION_FORBIDDEN");
        push1Res.Value.Outcomes.Single(o => o.OperationId == payOpId).AssignedReceiptNumbers.Should().NotBeNullOrEmpty();

        bool skewAudited = await db.AuditLogs
            .AnyAsync(a => a.ClinicId == clinicA && a.Action == "SyncLargeClockSkew");
        skewAudited.Should().BeTrue();

        // 2. Replay same RecordPayment OperationId -> DuplicateIgnored, only 1 Payment created!
        var replayRes = await syncService.PushAsync(
            ownerId, clinicA,
            new SyncPushBatchInput(device1, [batch1.Operations[4]]),
            null, null);
        replayRes.IsSuccess.Should().BeTrue();
        replayRes.Value.DuplicateCount.Should().Be(1);
        replayRes.Value.AppliedCount.Should().Be(0);

        currentClinic.SetTenant(clinicA);
        int paymentCount = await db.Payments.CountAsync(p => p.ClinicId == clinicA && p.PatientId == offlinePatientId);
        paymentCount.Should().Be(1);
        currentClinic.Clear();

        // 3. Two devices book same doctor slot offline + Portal booking clash
        DateTimeOffset slot1Start = new(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);
        Guid appt1Id = GuidV7.NewGuid();
        Guid appt2Id = GuidV7.NewGuid();

        var slotPush1 = await syncService.PushAsync(
            ownerId, clinicA,
            new SyncPushBatchInput(device1,
            [
                new SyncPushOperationInput(
                    GuidV7.NewGuid(),
                    "BookAppointment",
                    "Appointment",
                    appt1Id,
                    JsonSerializer.Serialize(new { id = appt1Id, patientId = offlinePatientId, doctorId = docA.Id, roomId = roomA.Id, categoryId = catA.Id, startUtc = slot1Start, priority = "Normal" }),
                    clock.UtcNow)
            ]),
            null, null);
        slotPush1.IsSuccess.Should().BeTrue();
        slotPush1.Value.AppliedCount.Should().Be(1);

        var slotPush2 = await syncService.PushAsync(
            ownerId, clinicA,
            new SyncPushBatchInput(device2,
            [
                new SyncPushOperationInput(
                    GuidV7.NewGuid(),
                    "BookAppointment",
                    "Appointment",
                    appt2Id,
                    JsonSerializer.Serialize(new { id = appt2Id, patientId = portalPatientRes.Value.Id, doctorId = docA.Id, roomId = roomA.Id, categoryId = catA.Id, startUtc = slot1Start, priority = "Normal" }),
                    clock.UtcNow)
            ]),
            null, null);
        slotPush2.IsSuccess.Should().BeTrue();
        slotPush2.Value.ConflictCount.Should().Be(1);
        Guid slotConflictId = slotPush2.Value.Outcomes[0].ConflictId!.Value;

        // Portal booking claims slot2 online, then offline device pushes booking for slot2 -> SyncConflict
        DateTimeOffset portalSlotStart = new(2026, 10, 16, 11, 0, 0, TimeSpan.Zero);
        var portalBooked = await portalService.BookAppointmentFromPortalAsync(
            patAct.Value.PatientAccountId,
            clinicA,
            portalPatientRes.Value.Id,
            slugA,
            new PortalBookAppointmentInput(docA.Id, catA.Id, portalSlotStart, null, GuidV7.NewGuid()),
            null,
            null);
        portalBooked.IsSuccess.Should().BeTrue();

        var offlinePortalClash = await syncService.PushAsync(
            ownerId, clinicA,
            new SyncPushBatchInput(device2,
            [
                new SyncPushOperationInput(
                    GuidV7.NewGuid(),
                    "BookAppointment",
                    "Appointment",
                    GuidV7.NewGuid(),
                    JsonSerializer.Serialize(new { id = GuidV7.NewGuid(), patientId = offlinePatientId, doctorId = docA.Id, roomId = roomA.Id, categoryId = catA.Id, startUtc = portalSlotStart, priority = "Normal" }),
                    clock.UtcNow)
            ]),
            null, null);
        offlinePortalClash.IsSuccess.Should().BeTrue();
        offlinePortalClash.Value.ConflictCount.Should().Be(1);

        // 4. Stale ClinicSettingsUpdate -> LastWriterWins + audit log captures old value
        var staleSettingsPush = await syncService.PushAsync(
            ownerId, clinicA,
            new SyncPushBatchInput(device2,
            [
                new SyncPushOperationInput(
                    GuidV7.NewGuid(),
                    "ClinicSettingsUpdate",
                    "ClinicSettings",
                    clinicA,
                    JsonSerializer.Serialize(new { name = "عيادة المرحلة ١٥ المحدثة" }),
                    clock.UtcNow,
                    ClientBaseCursor: 0)
            ]),
            null, null);
        staleSettingsPush.IsSuccess.Should().BeTrue();
        staleSettingsPush.Value.AppliedCount.Should().Be(1);

        bool lwwAudited = await db.AuditLogs
            .AnyAsync(a => a.ClinicId == clinicA && a.Action == "SyncLastWriterWinsOverwrite");
        lwwAudited.Should().BeTrue();

        // 5. Incremental Pull scoped strictly to Clinic A (Clinic B sees 0 changes)
        var pullA = await syncService.PullAsync(ownerId, clinicA, device2, 0, 50);
        pullA.IsSuccess.Should().BeTrue();
        pullA.Value.Changes.Should().NotBeEmpty();
        pullA.Value.OpenConflictsCount.Should().Be(2);

        var pullB = await syncService.PullAsync(ownerId, clinicB, device1, 0, 50);
        pullB.IsSuccess.Should().BeTrue();
        pullB.Value.Changes.Should().BeEmpty();
        pullB.Value.OpenConflictsCount.Should().Be(0);

        // 6. Resolve 1 conflict and verify Dashboard widgets & Super Admin Inspect
        var resolvedRes = await syncService.ResolveConflictAsync(
            ownerId, clinicA, slotConflictId,
            new ResolveSyncConflictInput(SyncConflictStatus.ResolvedKeepServer, "Kept first offline booking"),
            device1, null, null);
        resolvedRes.IsSuccess.Should().BeTrue();
        resolvedRes.Value.Status.Should().Be(SyncConflictStatus.ResolvedKeepServer);

        DateTimeOffset fromUtc = clock.UtcNow.AddDays(-1);
        DateTimeOffset toUtc = clock.UtcNow.AddDays(1);
        var wDevices = await dashboard.QueryWidgetAsync(ownerId, clinicA, "offline-sync.registered-devices", fromUtc, toUtc, null, device1);
        var wConflicts = await dashboard.QueryWidgetAsync(ownerId, clinicA, "offline-sync.open-conflicts", fromUtc, toUtc, null, device1);
        wDevices.IsSuccess.Should().BeTrue();
        wDevices.Value.PrimaryValue.Should().Be(2m);
        wConflicts.IsSuccess.Should().BeTrue();
        wConflicts.Value.PrimaryValue.Should().Be(1m);

        var inspectRes = await syncService.InspectClinicOfflineSyncAsync(admin.Id, clinicA);
        inspectRes.IsSuccess.Should().BeTrue();
        inspectRes.Value.Conflicts.Count.Should().Be(2);
        inspectRes.Value.Devices.Count.Should().Be(2);

        // 7. Revoked device rejected cleanly on Push
        (await authService.RevokeDeviceAsync(ownerId, device2, null, null)).IsSuccess.Should().BeTrue();
        var revokedPush = await syncService.PushAsync(
            ownerId, clinicA,
            new SyncPushBatchInput(device2, []),
            null, null);
        revokedPush.IsFailure.Should().BeTrue();
        revokedPush.Error.Code.Should().Be("DEVICE_REVOKED");
    }

    private static async Task OnboardClinicWithSlugAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        string slug,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة المزامنة", "12 شارع النيل", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
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
