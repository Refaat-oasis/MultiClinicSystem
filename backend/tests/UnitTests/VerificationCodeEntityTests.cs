using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class VerificationCodeEntityTests
{
    [Fact]
    public void Given_FreshCode_When_Created_Then_AttemptsIsZeroAndNotDead()
    {
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Staff",
            "Invitation",
            "dummy_hash",
            now.AddHours(72),
            now);

        code.Attempts.Should().Be(0);
        code.IsDead().Should().BeFalse();
        code.IsExpired(now).Should().BeFalse();
    }

    [Fact]
    public void Given_CodeWithFailedAttempts_When_ReachesMaxAttempts_Then_IsDeadReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Staff",
            "Invitation",
            "dummy_hash",
            now.AddHours(72),
            now,
            maxAttempts: 5);

        for (int i = 0; i < 4; i++)
        {
            code.RecordFailedAttempt();
            code.IsDead().Should().BeFalse();
        }

        code.RecordFailedAttempt();
        code.Attempts.Should().Be(5);
        code.IsDead().Should().BeTrue();
    }

    [Fact]
    public void Given_Code_When_MarkedUsed_Then_IsDeadReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Staff",
            "Invitation",
            "dummy_hash",
            now.AddHours(72),
            now);

        code.MarkUsed(now);

        code.UsedAtUtc.Should().Be(now);
        code.IsDead().Should().BeTrue();
    }

    [Fact]
    public void Given_ExpiredCode_When_CheckedAgainstCurrentTime_Then_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Staff",
            "PasswordReset",
            "dummy_hash",
            now.AddMinutes(-5),
            now.AddMinutes(-20));

        code.IsExpired(now).Should().BeTrue();
    }
}
