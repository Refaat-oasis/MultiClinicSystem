using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace UnitTests;

public sealed class HourlyTotpServiceTests
{
    private readonly HourlyTotpService _sut;

    public HourlyTotpServiceTests()
    {
        var protectorProvider = DataProtectionProvider.Create("ClinicPlatform.UnitTests");
        var verificationCodes = new VerificationCodes("unit-test-hmac-secret-key-32-chars-clinic-platform");
        _sut = new HourlyTotpService(protectorProvider, verificationCodes);
    }

    [Fact]
    public void Given_GeneratedBase32Secret_When_ProtectedAndUnprotected_Then_RoundTripsOriginalSecret()
    {
        string rawSecret = _sut.GenerateSecretBase32();
        string encrypted = _sut.ProtectSecret(rawSecret);
        string decrypted = _sut.UnprotectSecret(encrypted);

        encrypted.Should().NotBe(rawSecret);
        decrypted.Should().Be(rawSecret);
    }

    [Fact]
    public void Given_CurrentHourCode_When_ValidatedWithinSameHour_Then_SucceedsAndReturnsTimeStep()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 6, 15, 10, 12, 30, TimeSpan.Zero));
        string secret = _sut.GenerateSecretBase32();
        string code = _sut.ComputeCurrentCode(secret, clock.UtcNow);

        var result = _sut.ValidateCode(secret, code, clock.UtcNow, lastAcceptedTimeStep: null);

        result.IsValid.Should().BeTrue();
        result.AcceptedTimeStep.Should().Be(_sut.GetTimeStep(clock.UtcNow));
        result.FailureCode.Should().BeNull();
    }

    [Fact]
    public void Given_AlreadyAcceptedTimeStep_When_SameHourCodeReplayed_Then_ReturnsReplayError()
    {
        var clock = new DateTimeOffset(2026, 6, 15, 10, 20, 0, TimeSpan.Zero);
        string secret = _sut.GenerateSecretBase32();
        string code = _sut.ComputeCurrentCode(secret, clock);
        long currentStep = _sut.GetTimeStep(clock);

        var replayResult = _sut.ValidateCode(secret, code, clock.AddMinutes(10), lastAcceptedTimeStep: currentStep);

        replayResult.IsValid.Should().BeFalse();
        replayResult.FailureCode.Should().Be("CODE_REPLAYED");
    }

    [Fact]
    public void Given_PreviousHourCode_When_ValidatedWithinFirstFiveMinutesOfNewHour_Then_Succeeds()
    {
        var previousHour = new DateTimeOffset(2026, 6, 15, 10, 45, 0, TimeSpan.Zero);
        var withinGrace = new DateTimeOffset(2026, 6, 15, 11, 4, 59, TimeSpan.Zero);

        string secret = _sut.GenerateSecretBase32();
        string previousCode = _sut.ComputeCurrentCode(secret, previousHour);

        var result = _sut.ValidateCode(secret, previousCode, withinGrace, lastAcceptedTimeStep: null);

        result.IsValid.Should().BeTrue();
        result.AcceptedTimeStep.Should().Be(_sut.GetTimeStep(previousHour));
    }

    [Fact]
    public void Given_PreviousHourCode_When_ValidatedAfterFiveMinutesOfNewHour_Then_FailsWithInvalidCode()
    {
        var previousHour = new DateTimeOffset(2026, 6, 15, 10, 45, 0, TimeSpan.Zero);
        var outsideGrace = new DateTimeOffset(2026, 6, 15, 11, 5, 0, TimeSpan.Zero);

        string secret = _sut.GenerateSecretBase32();
        string previousCode = _sut.ComputeCurrentCode(secret, previousHour);

        var result = _sut.ValidateCode(secret, previousCode, outsideGrace, lastAcceptedTimeStep: null);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be("INVALID_TOTP_CODE");
    }

    [Fact]
    public void Given_TenRecoveryKeys_When_HashedWithAdminId_Then_AreDistinctAndHmacBound()
    {
        var adminId = GuidV7.NewGuid();
        var keys = _sut.GenerateRecoveryKeys(10);

        keys.Should().HaveCount(10);
        keys.Should().OnlyHaveUniqueItems();

        string hashA = _sut.HashRecoveryKey(keys[0], adminId);
        string hashDifferentAdmin = _sut.HashRecoveryKey(keys[0], GuidV7.NewGuid());
        hashA.Should().NotBe(keys[0]);
        hashA.Should().NotBe(hashDifferentAdmin);
    }

    [Fact]
    public void Given_PlatformAdmin_When_FiveFailedLoginsAndStepUpTimeout_Then_EnforcesLockoutAndStepUpWindow()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var admin = new PlatformAdmin(GuidV7.NewGuid(), "ahmed@platform.local", "Ahmed", "hash", clock.UtcNow);

        for (int i = 0; i < 4; i++)
        {
            admin.RecordFailedLogin(clock.UtcNow);
            admin.IsLockedOut(clock.UtcNow).Should().BeFalse();
        }

        admin.RecordFailedLogin(clock.UtcNow);
        admin.IsLockedOut(clock.UtcNow).Should().BeTrue();
        admin.IsLockedOut(clock.UtcNow.AddMinutes(14)).Should().BeTrue();
        admin.IsLockedOut(clock.UtcNow.AddMinutes(16)).Should().BeFalse();

        admin.RecordStepUp(clock.UtcNow);
        admin.HasValidStepUp(clock.UtcNow.AddMinutes(9), TimeSpan.FromMinutes(10)).Should().BeTrue();
        admin.HasValidStepUp(clock.UtcNow.AddMinutes(11), TimeSpan.FromMinutes(10)).Should().BeFalse();
    }
}
