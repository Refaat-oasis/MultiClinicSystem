using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class Phase15SyncDomainTests
{
    [Theory]
    [InlineData("CloseDay", true)]
    [InlineData("ReopenDay", true)]
    [InlineData("Refund", true)]
    [InlineData("PriceListUpsert", true)]
    [InlineData("RolesAndClaims", true)]
    [InlineData("AddClinic", true)]
    [InlineData("Onboarding", true)]
    [InlineData("ModuleChange", true)]
    [InlineData("AdminAction", true)]
    [InlineData("PasswordOrEmailChange", true)]
    [InlineData("PortalEndpoint", true)]
    [InlineData("CrossClinicReport", true)]
    [InlineData("RegisterPatient", false)]
    [InlineData("BookAppointment", false)]
    [InlineData("CompleteVisit", false)]
    [InlineData("RecordPayment", false)]
    [InlineData("CreateCharge", false)]
    [InlineData("ClinicSettingsUpdate", false)]
    public void Given_ActionType_When_CheckingOnlineOnlyGuard_Then_EnforcesOnlineOnlyBoundaries(string actionType, bool expectedOnlineOnly)
    {
        SyncCalculator.IsOnlineOnlyAction(actionType).Should().Be(expectedOnlineOnly);
    }

    [Theory]
    [InlineData("Visit", ConflictClass.AppendOnly)]
    [InlineData("ClinicalNote", ConflictClass.AppendOnly)]
    [InlineData("Charge", ConflictClass.AppendOnly)]
    [InlineData("Payment", ConflictClass.AppendOnly)]
    [InlineData("Appointment", ConflictClass.ServerValidated)]
    [InlineData("QueueEntry", ConflictClass.ServerValidated)]
    [InlineData("Patient", ConflictClass.ServerValidated)]
    [InlineData("ClinicSettings", ConflictClass.LastWriterWins)]
    public void Given_EntityType_When_ClassifyingConflictPolicy_Then_ReturnsExpectedClass(string entityType, ConflictClass expected)
    {
        SyncCalculator.ClassifyConflictClass(entityType).Should().Be(expected);
    }

    [Fact]
    public void Given_ClientAndServerTimestamps_When_EvaluatingClockSkew_Then_FlagsLargeSkewAboveThreshold()
    {
        DateTimeOffset serverNow = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

        var okSkew = SyncCalculator.EvaluateClockSkew(serverNow.AddMinutes(-2), serverNow, thresholdSeconds: 300);
        okSkew.AbsoluteSkewSeconds.Should().Be(120);
        okSkew.IsLargeSkew.Should().BeFalse();

        var largeSkew = SyncCalculator.EvaluateClockSkew(serverNow.AddMinutes(-15), serverNow, thresholdSeconds: 300);
        largeSkew.AbsoluteSkewSeconds.Should().Be(900);
        largeSkew.IsLargeSkew.Should().BeTrue();
    }

    [Fact]
    public void Given_RetryAttemptsAndOfflineSession_When_ComputingBackoffAndExpiry_Then_CapsBackoffAndEnforcesSessionWindow()
    {
        SyncCalculator.ComputeBackoffDelayMs(0).Should().Be(500);
        SyncCalculator.ComputeBackoffDelayMs(1).Should().Be(1000);
        SyncCalculator.ComputeBackoffDelayMs(10).Should().Be(30000);

        DateTimeOffset loginAt = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
        SyncCalculator.IsOfflineSessionValid(loginAt, loginAt.AddHours(23), maxHours: 24).Should().BeTrue();
        SyncCalculator.IsOfflineSessionValid(loginAt, loginAt.AddHours(25), maxHours: 24).Should().BeFalse();

        var conflict = new SyncConflict(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "dev-1",
            Guid.NewGuid(),
            "Appointment",
            Guid.NewGuid(),
            ConflictClass.ServerValidated,
            "SLOT_TAKEN",
            "Portal booked slot while device was offline",
            "{}",
            "{}",
            loginAt);
        conflict.Status.Should().Be(SyncConflictStatus.Open);
        conflict.Resolve(SyncConflictStatus.ResolvedKeepServer, "Kept portal slot", Guid.NewGuid(), loginAt.AddHours(2));
        conflict.Status.Should().Be(SyncConflictStatus.ResolvedKeepServer);
        conflict.ResolvedAtUtc.Should().Be(loginAt.AddHours(2));
    }
}

