using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class PlatformAdminTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public PlatformAdminTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_SeededSuperAdmin_When_FirstLoginAndEnrollCompleted_Then_PasswordUpdatedAndTenRecoveryKeysIssued()
    {
        var clock = new IntegrationTestClock();
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, _) = TestServiceFactory.CreateServices(context, clock);
        using var scope = provider.CreateScope();

        var auth = scope.ServiceProvider.GetRequiredService<IPlatformAdminAuthService>();
        var totp = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");

        var firstLogin = await auth.LoginAsync("ahmed@platform.local", "AdminInitial#2026", null, null, "127.0.0.1", "req-1");
        firstLogin.IsSuccess.Should().BeTrue();
        firstLogin.Value.RequiresEnrollment.Should().BeTrue();
        firstLogin.Value.ManualSetupKey.Should().NotBeNullOrWhiteSpace();

        string code = totp.ComputeCurrentCode(firstLogin.Value.ManualSetupKey!, clock.UtcNow);
        var enroll = await auth.CompleteEnrollmentAsync(firstLogin.Value.AdminId, "SuperStrongPass#2026", code, "127.0.0.1", "req-2");

        enroll.IsSuccess.Should().BeTrue();
        enroll.Value.RecoveryKeys.Should().HaveCount(10);
        enroll.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Given_EnrolledSuperAdmin_When_LoggingInWithHourlyCodeOrRecoveryKey_Then_RefusesReplayAndSingleUseKeyReuse()
    {
        var clock = new IntegrationTestClock();
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, _) = TestServiceFactory.CreateServices(context, clock);
        using var scope = provider.CreateScope();

        var auth = scope.ServiceProvider.GetRequiredService<IPlatformAdminAuthService>();
        var totp = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");

        var firstLogin = await auth.LoginAsync("ahmed@platform.local", "AdminInitial#2026", null, null, null, null);
        firstLogin.IsSuccess.Should().BeTrue();
        string secret = firstLogin.Value.ManualSetupKey!;

        string enrollCode = totp.ComputeCurrentCode(secret, clock.UtcNow);
        var enroll = await auth.CompleteEnrollmentAsync(firstLogin.Value.AdminId, "SuperStrongPass#2026", enrollCode, null, null);
        enroll.IsSuccess.Should().BeTrue();

        // Advance to next hour and verify normal login + replay protection
        clock.Advance(TimeSpan.FromHours(1));
        string nextHourCode = totp.ComputeCurrentCode(secret, clock.UtcNow);

        var missingCode = await auth.LoginAsync("ahmed@platform.local", "SuperStrongPass#2026", null, null, null, null);
        missingCode.IsSuccess.Should().BeFalse();
        missingCode.Error.Code.Should().Be("TOTP_CODE_REQUIRED");

        var validLogin = await auth.LoginAsync("ahmed@platform.local", "SuperStrongPass#2026", nextHourCode, null, null, null);
        validLogin.IsSuccess.Should().BeTrue();
        validLogin.Value.RequiresEnrollment.Should().BeFalse();

        var replayLogin = await auth.LoginAsync("ahmed@platform.local", "SuperStrongPass#2026", nextHourCode, null, null, null);
        replayLogin.IsSuccess.Should().BeFalse();
        replayLogin.Error.Code.Should().Be("CODE_REPLAYED");

        // Recovery key succeeds once and fails second time
        string recoveryKey = enroll.Value.RecoveryKeys[0];
        var recoveryLogin = await auth.LoginAsync("ahmed@platform.local", "SuperStrongPass#2026", null, recoveryKey, null, null);
        recoveryLogin.IsSuccess.Should().BeTrue();

        var secondRecovery = await auth.LoginAsync("ahmed@platform.local", "SuperStrongPass#2026", null, recoveryKey, null, null);
        secondRecovery.IsSuccess.Should().BeFalse();
        secondRecovery.Error.Code.Should().Be("INVALID_CREDENTIALS");

        // Reset break-glass clears enrollment and writes audit log
        var breakGlass = await auth.ResetTwoFactorBreakGlassAsync("ahmed@platform.local", "dev-break-glass-secret-key-2026", "127.0.0.1", "req-bg");
        breakGlass.IsSuccess.Should().BeTrue();

        var adminEntity = await db.PlatformAdmins.FirstAsync();
        adminEntity.TotpEnrolled.Should().BeFalse();
        adminEntity.MustChangePassword.Should().BeTrue();

        var auditExists = await db.AuditLogs.AnyAsync(a => a.Action == "PlatformAdminBreakGlassReset2FA");
        auditExists.Should().BeTrue();
    }

    [Fact]
    public async Task Given_SuperAdmin_When_InspectingClinicAndMovingOwner_Then_WritesPatientAccessLogAndTransfersOwnership()
    {
        var clock = new IntegrationTestClock();
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context, clock);
        using var scope = provider.CreateScope();

        var adminAuth = scope.ServiceProvider.GetRequiredService<IPlatformAdminAuthService>();
        var platformSvc = scope.ServiceProvider.GetRequiredService<IPlatformAdminService>();
        var totp = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");

        var first = await adminAuth.LoginAsync("ahmed@platform.local", "AdminInitial#2026", null, null, null, null);
        await adminAuth.CompleteEnrollmentAsync(first.Value.AdminId, "SuperStrongPass#2026", totp.ComputeCurrentCode(first.Value.ManualSetupKey!, clock.UtcNow), null, null);

        string emailO1 = $"o1_{Guid.NewGuid():N}@clinic.local";
        string emailO2 = $"o2_{Guid.NewGuid():N}@clinic.local";
        var o1Res = await platformSvc.CreateOwnerAsync(first.Value.AdminId, "Owner One", emailO1, null, 3, null, null);
        var o2Res = await platformSvc.CreateOwnerAsync(first.Value.AdminId, "Owner Two", emailO2, null, 3, null, null);
        o1Res.IsSuccess.Should().BeTrue();
        o2Res.IsSuccess.Should().BeTrue();
        emailSender.SentMessages.Should().Contain(m => m.To == emailO1);

        var clinicId = GuidV7.NewGuid();
        var clinic = new Clinic(clinicId, "Nile Clinic", $"nile-{Guid.NewGuid():N}", clock.UtcNow, o1Res.Value.OwnerId);
        clinic.Activate();
        db.Clinics.Add(clinic);
        db.UserClinics.Add(new UserClinic(GuidV7.NewGuid(), o1Res.Value.UserId, clinicId, "Owner", clock.UtcNow));
        await db.SaveChangesAsync();

        // Inspect Clinic writes PatientAccessLog and audit log
        var inspect = await platformSvc.InspectClinicAsync(first.Value.AdminId, clinicId, "127.0.0.1", "req-inspect");
        inspect.IsSuccess.Should().BeTrue();
        inspect.Value.ReadOnlyMode.Should().BeTrue();

        var accessLog = await db.PatientAccessLogs.FirstOrDefaultAsync(l => l.ClinicId == clinicId);
        accessLog.Should().NotBeNull();
        accessLog!.Details.Should().Contain("ActorType:SuperAdmin");

        // Owner O1 can read the clinic access log
        var ownerAccessLogs = await platformSvc.GetClinicAccessLogsForOwnerAsync(o1Res.Value.UserId, clinicId);
        ownerAccessLogs.IsSuccess.Should().BeTrue();
        ownerAccessLogs.Value.Should().NotBeEmpty();

        // Move Clinic beyond 10-min step-up window requires step-up password
        clock.Advance(TimeSpan.FromMinutes(15));
        var moveWithoutStepUp = await platformSvc.MoveClinicOwnerAsync(first.Value.AdminId, clinicId, o2Res.Value.OwnerId, null, null, null);
        moveWithoutStepUp.IsSuccess.Should().BeFalse();
        moveWithoutStepUp.Error.Code.Should().Be("STEP_UP_REQUIRED");

        var moveWithStepUp = await platformSvc.MoveClinicOwnerAsync(
            first.Value.AdminId,
            clinicId,
            o2Res.Value.OwnerId,
            "SuperStrongPass#2026",
            "127.0.0.1",
            "req-move");
        moveWithStepUp.IsSuccess.Should().BeTrue();

        var oldMappingExists = await db.UserClinics.AnyAsync(uc => uc.ClinicId == clinicId && uc.UserId == o1Res.Value.UserId);
        var newMappingExists = await db.UserClinics.AnyAsync(uc => uc.ClinicId == clinicId && uc.UserId == o2Res.Value.UserId);
        oldMappingExists.Should().BeFalse();
        newMappingExists.Should().BeTrue();
    }
}
