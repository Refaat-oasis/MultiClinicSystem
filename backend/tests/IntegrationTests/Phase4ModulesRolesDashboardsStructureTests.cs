using System.Text.RegularExpressions;
using BuildingBlocks.Application;
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
public sealed class Phase4ModulesRolesDashboardsStructureTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase4ModulesRolesDashboardsStructureTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_Clinic_When_SuperAdminTogglesModulesWithDependencies_Then_EnforcesGraphEmitsOutboxAndAudits()
    {
        var clock = new IntegrationTestClock();
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

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"mod-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner Modules", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");

        var clinicRes = await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة الميزات", null, null, null, "owner-dev", null, null);
        Guid clinicId = clinicRes.Value.ClinicId;

        // Invalid step-up password rejected
        var badStepUp = await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicId, [new ModuleToggleInput("patients", true, null)], "WrongPass!", null, null);
        badStepUp.IsFailure.Should().BeTrue();
        badStepUp.Error.Code.Should().Be("STEP_UP_INVALID");

        // Enabling clinical without patients & scheduling fails
        var missingPrereq = await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicId, [new ModuleToggleInput("clinical", true, null)], "SuperStrongPass#2026", null, null);
        missingPrereq.IsFailure.Should().BeTrue();
        missingPrereq.Error.Code.Should().Be("MODULE_PREREQUISITE_OFF");

        // Enable patients + scheduling + clinical together succeeds
        var enableBundle = await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicId,
            [
                new ModuleToggleInput("patients", true, "Bundle start"),
                new ModuleToggleInput("scheduling", true, "Bundle start"),
                new ModuleToggleInput("clinical", true, "Bundle start")
            ],
            "SuperStrongPass#2026",
            null,
            null);
        enableBundle.IsSuccess.Should().BeTrue();
        enableBundle.Value.Count(m => m.Enabled).Should().Be(3);

        // Disabling patients while scheduling & clinical are on fails
        var hasDep = await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicId, [new ModuleToggleInput("patients", false, null)], "SuperStrongPass#2026", null, null);
        hasDep.IsFailure.Should().BeTrue();
        hasDep.Error.Code.Should().Be("MODULE_HAS_DEPENDENTS");

        // Outbox & Audit recorded
        var outboxEvents = await db.OutboxMessages.Where(o => o.Type == "ClinicModuleChanged").ToListAsync();
        outboxEvents.Should().NotBeEmpty();
        var auditEvents = await db.AuditLogs.Where(a => a.ClinicId == clinicId && a.Action == "ClinicModulesUpdated").ToListAsync();
        auditEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_RolesOverridesAndStaffDevices_When_TogglingModulesAndApprovingDevices_Then_PreservesDormantPermissionsAndEnforcesDeviceApproval()
    {
        var clock = new IntegrationTestClock();
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
        var clinicAuth = sp.GetRequiredService<IClinicAuthorizationService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"rbac-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner RBAC", ownerEmail, null, 2, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");

        var clinicRes = await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة الصلاحيات", null, null, null, "owner-dev", null, null);
        Guid clinicId = clinicRes.Value.ClinicId;

        // Enable patients + scheduling + billing so Owner holds those permissions
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicId,
            [
                new ModuleToggleInput("patients", true, null),
                new ModuleToggleInput("scheduling", true, null),
                new ModuleToggleInput("billing", true, null)
            ],
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        // Owner lists default roles and clones Receptionist -> Senior Receptionist
        var roles = await clinicAuth.ListRolesAsync(ownerRes.Value.UserId, clinicId, "owner-dev");
        roles.IsSuccess.Should().BeTrue();
        roles.Value.Select(r => r.Name).Should().Contain(["Owner", "Doctor", "Assistant", "Receptionist"]);
        var receptionist = roles.Value.First(r => r.Name == "Receptionist");

        var cloned = await clinicAuth.CloneRoleAsync(
            ownerRes.Value.UserId, clinicId, receptionist.RoleId, "Senior Receptionist", "استقبال أول", "Senior Receptionist", "owner-dev", null, null);
        cloned.IsSuccess.Should().BeTrue();
        cloned.Value.Permissions.Should().Contain(["billing.read", "billing.write"]);

        // Reserved owner permission cannot be added to cloned custom role
        var reservedAttempt = await clinicAuth.UpdateRolePermissionsAsync(
            ownerRes.Value.UserId, clinicId, cloned.Value.RoleId, ["patients.read", "roles.manage"], "owner-dev", null, null);
        reservedAttempt.IsFailure.Should().BeTrue();
        reservedAttempt.Error.Code.Should().Be("RESERVED_PERMISSION_FORBIDDEN");

        // Owner cannot be removed or demoted (LAST_OWNER_PROTECTED)
        var removeOwner = await clinicAuth.RemoveStaffAsync(ownerRes.Value.UserId, clinicId, ownerRes.Value.UserId, "owner-dev", null, null);
        removeOwner.IsFailure.Should().BeTrue();
        removeOwner.Error.Code.Should().Be("LAST_OWNER_PROTECTED");

        // Invite staff member under Senior Receptionist
        emailSender.Clear();
        string staffEmail = $"staff-{Guid.NewGuid():N}@clinic.local";
        var invited = await clinicAuth.InviteStaffAsync(
            ownerRes.Value.UserId, clinicId, "أحمد استقبال", staffEmail, "Senior Receptionist", "owner-dev", null, null);
        invited.IsSuccess.Should().BeTrue();
        Guid staffUserId = invited.Value.UserId;

        // Staff activates on unapproved device "dev-activate"
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, staffEmail, "StaffPass#2026!");

        // Before owner approves device, staff clinic access fails with DEVICE_APPROVAL_REQUIRED
        var blockedStaff = await clinicAuth.EnsureClinicAccessAsync(staffUserId, clinicId, "billing.read", "billing", "dev-activate");
        blockedStaff.IsFailure.Should().BeTrue();
        blockedStaff.Error.Code.Should().Be("DEVICE_APPROVAL_REQUIRED");

        // Owner approves staff device
        var devices = await clinicAuth.ListClinicDevicesAsync(ownerRes.Value.UserId, clinicId, "owner-dev");
        devices.IsSuccess.Should().BeTrue();
        devices.Value.Should().Contain(d => d.UserId == staffUserId && d.DeviceId == "dev-activate" && !d.IsApproved);

        (await clinicAuth.ApproveClinicDeviceAsync(ownerRes.Value.UserId, clinicId, "dev-activate", "owner-dev", null, null)).IsSuccess.Should().BeTrue();

        // After approval, staff has billing.read
        (await clinicAuth.EnsureClinicAccessAsync(staffUserId, clinicId, "billing.read", "billing", "dev-activate")).IsSuccess.Should().BeTrue();

        // Owner sets Grant billing.discount and Deny patients.write (Deny wins)
        var overridden = await clinicAuth.SetUserPermissionOverridesAsync(
            ownerRes.Value.UserId,
            clinicId,
            staffUserId,
            [
                new PermissionOverrideInput("billing.discount", "Grant"),
                new PermissionOverrideInput("patients.write", "Deny")
            ],
            "owner-dev",
            null,
            null);
        overridden.IsSuccess.Should().BeTrue();
        overridden.Value.EffectivePermissions.Should().Contain("billing.discount");
        overridden.Value.EffectivePermissions.Should().NotContain("patients.write");

        // Turn billing OFF -> billing.read, billing.write, billing.discount become dormant (preserved, not erased)
        (await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicId, [new ModuleToggleInput("billing", false, "Temporary pause")], "SuperStrongPass#2026", null, null)).IsSuccess.Should().BeTrue();

        var permsWhileOff = await clinicAuth.GetUserEffectivePermissionsAsync(staffUserId, clinicId);
        permsWhileOff.Value.EffectivePermissions.Should().NotContain("billing.read");
        permsWhileOff.Value.EffectivePermissions.Should().NotContain("billing.discount");
        permsWhileOff.Value.DormantPermissions.Should().Contain(["billing.read", "billing.write", "billing.discount"]);

        // Turn billing BACK ON -> dormant permissions automatically restore!
        (await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicId, [new ModuleToggleInput("billing", true, "Resumed")], "SuperStrongPass#2026", null, null)).IsSuccess.Should().BeTrue();

        var permsRestored = await clinicAuth.GetUserEffectivePermissionsAsync(staffUserId, clinicId);
        permsRestored.Value.EffectivePermissions.Should().Contain(["billing.read", "billing.write", "billing.discount"]);
        permsRestored.Value.DormantPermissions.Should().NotContain(["billing.read", "billing.write", "billing.discount"]);
    }

    [Fact]
    public async Task Given_StructurePortfolioAndConcurrentNumberBlocks_When_Executed_Then_IsolatesClinicsAndAllocatesNonOverlappingBlocks()
    {
        var clock = new IntegrationTestClock();
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        Guid ownerAUserId;
        Guid ownerBUserId;
        Guid clinicAId;
        Guid clinicBId;
        Guid clinicOtherId;

        using (var scope = provider.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<CoreDbContext>();
            var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
            var totp = sp.GetRequiredService<IHourlyTotpService>();
            var adminService = sp.GetRequiredService<IPlatformAdminService>();
            var authService = sp.GetRequiredService<IAuthService>();
            var outbox = sp.GetRequiredService<OutboxService>();
            var clinicsService = sp.GetRequiredService<IClinicManagementService>();
            var structureService = sp.GetRequiredService<IClinicStructureService>();
            var dashboardService = sp.GetRequiredService<IDashboardService>();
            var portfolioService = sp.GetRequiredService<IOwnerPortfolio>();

            var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);

            string ownerAEmail = $"struct-a-{Guid.NewGuid():N}@clinic.local";
            string ownerBEmail = $"struct-b-{Guid.NewGuid():N}@clinic.local";
            var ownerARes = await adminService.CreateOwnerAsync(admin.Id, "Owner Portfolio", ownerAEmail, null, 3, null, null);
            var ownerBRes = await adminService.CreateOwnerAsync(admin.Id, "Owner Other", ownerBEmail, null, 2, null, null);
            ownerAUserId = ownerARes.Value.UserId;
            ownerBUserId = ownerBRes.Value.UserId;

            await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerAEmail, "OwnerPass#2026");
            await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerBEmail, "OwnerPass#2026");

            clinicAId = (await clinicsService.CreateClinicAsync(ownerAUserId, "فرع المعادي", null, null, null, "dev-a", null, null)).Value.ClinicId;
            clinicBId = (await clinicsService.CreateClinicAsync(ownerAUserId, "فرع مدينة نصر", null, null, null, "dev-a", null, null)).Value.ClinicId;
            clinicOtherId = (await clinicsService.CreateClinicAsync(ownerBUserId, "عيادة أخرى", null, null, null, "dev-b", null, null)).Value.ClinicId;

            // Create room, doctor, working hours, day off in Clinic A
            var roomRes = await structureService.CreateRoomAsync(
                ownerAUserId, clinicAId, "R-101", "غرفة الكشف 1", "Exam Room 1", "dental", "Floor 1", "dev-a", null, null);
            roomRes.IsSuccess.Should().BeTrue();

            var docRes = await structureService.CreateDoctorAsync(
                ownerAUserId, clinicAId, null, "د. طارق المنصور", "أخصائي أسنان", "Dental Specialist",
                ["dental"], [roomRes.Value.Id], 20, "dev-a", null, null);
            docRes.IsSuccess.Should().BeTrue();

            var hoursRes = await structureService.SaveWorkingHoursAsync(
                ownerAUserId, clinicAId, null, [new WorkingHourInput(0, "09:00", "17:00", false)], "dev-a", null, null);
            hoursRes.IsSuccess.Should().BeTrue();

            var dayOffRes = await structureService.CreateDayOffAsync(
                ownerAUserId, clinicAId, null, "2026-12-25", "2026-12-26", "إجازة رسمية", "Public Holiday", "dev-a", null, null);
            dayOffRes.IsSuccess.Should().BeTrue();

            // Dashboard query for core.rooms-count returns 1; disabled module widget returns MODULE_DISABLED; range > 366 rejected
            var roomsWidget = await dashboardService.QueryWidgetAsync(ownerAUserId, clinicAId, "core.rooms-count", null, null, null, "dev-a");
            roomsWidget.IsSuccess.Should().BeTrue();
            roomsWidget.Value.PrimaryValue.Should().Be(1m);

            var disabledWidget = await dashboardService.QueryWidgetAsync(ownerAUserId, clinicAId, "patients.summary", null, null, null, "dev-a");
            disabledWidget.IsFailure.Should().BeTrue();
            disabledWidget.Error.Code.Should().Be("MODULE_DISABLED");

            var badRangeWidget = await dashboardService.QueryWidgetAsync(
                ownerAUserId, clinicAId, "core.rooms-count", clock.UtcNow.AddDays(-400), clock.UtcNow, null, "dev-a");
            badRangeWidget.IsFailure.Should().BeTrue();
            badRangeWidget.Error.Code.Should().Be("WIDGET_DATE_RANGE_EXCEEDED");

            // Portfolio aggregates across Clinic A + Clinic B under RLS
            var portfolio = await portfolioService.GetPortfolioAsync(ownerAUserId, null, null, null);
            portfolio.IsSuccess.Should().BeTrue();
            portfolio.Value.TotalClinics.Should().Be(2);
            portfolio.Value.TotalDoctorsAcrossClinics.Should().Be(1);

            // Cross-owner portfolio filter returns 404
            var crossPortfolio = await portfolioService.GetPortfolioAsync(ownerAUserId, [clinicOtherId], null, null);
            crossPortfolio.IsFailure.Should().BeTrue();
            crossPortfolio.Error.Code.Should().Be("CLINIC_NOT_FOUND");

            // Owner audit viewer shows Clinic A audit entries only
            var auditA = await structureService.GetClinicAuditLogsAsync(ownerAUserId, clinicAId, null, 1, 20, "dev-a");
            auditA.IsSuccess.Should().BeTrue();
            auditA.Value.Should().Contain(a => a.Action == "ClinicRoomCreated");
            auditA.Value.Should().OnlyContain(a => a.ClinicId == clinicAId);
        }

        // Concurrent NumberBlock reservation across 2 separate scopes/devices produces non-overlapping ranges 1..500 and 501..1000
        async Task<BuildingBlocks.Domain.Result<NumberBlockResult>> ReserveInOwnScopeAsync(string devId)
        {
            var localCtx = new CurrentClinicContext();
            var (localProvider, _) = TestServiceFactory.CreateServices(localCtx, clock);
            using var s = localProvider.CreateScope();
            var svc = s.ServiceProvider.GetRequiredService<IClinicStructureService>();
            return await svc.ReserveNumberBlockAsync(ownerAUserId, clinicAId, devId, "Receipt", NumberBlock.DefaultBlockSize, "dev-a", null, null);
        }

        var results = await Task.WhenAll(
            ReserveInOwnScopeAsync("tablet-1"),
            ReserveInOwnScopeAsync("tablet-2"));

        results.Should().OnlyContain(r => r.IsSuccess);
        var ordered = results.Select(r => r.Value).OrderBy(b => b.FromNumber).ToList();
        ordered[0].FromNumber.Should().Be(1);
        ordered[0].ToNumber.Should().Be(500);
        ordered[1].FromNumber.Should().Be(501);
        ordered[1].ToNumber.Should().Be(1000);
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
