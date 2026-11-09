using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Given_FreshRefreshToken_When_WithinExpiry_Then_IsActiveReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sample_hash",
            "device-1",
            now.AddDays(7),
            Guid.NewGuid(),
            now);

        token.IsActive(now).Should().BeTrue();
    }

    [Fact]
    public void Given_ExpiredRefreshToken_When_Checked_Then_IsActiveReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sample_hash",
            "device-1",
            now.AddMinutes(-1),
            Guid.NewGuid(),
            now.AddDays(-7));

        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void Given_RevokedRefreshToken_When_Checked_Then_IsActiveReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sample_hash",
            "device-1",
            now.AddDays(7),
            Guid.NewGuid(),
            now);

        token.Revoke(now);

        token.IsRevoked.Should().BeTrue();
        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void Given_ReplacedRefreshToken_When_Checked_Then_IsActiveReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sample_hash",
            "device-1",
            now.AddDays(7),
            Guid.NewGuid(),
            now);

        var nextTokenId = Guid.NewGuid();
        token.Replace(nextTokenId, now);

        token.ReplacedByTokenId.Should().Be(nextTokenId);
        token.IsRevoked.Should().BeTrue();
        token.IsActive(now).Should().BeFalse();
    }
}
