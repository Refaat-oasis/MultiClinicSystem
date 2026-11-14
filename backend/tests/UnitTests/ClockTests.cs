using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class ClockTests
{
    [Fact]
    public void Given_TestClock_When_Advanced_Then_UtcNowReflectsElapsedDuration()
    {
        // Arrange
        var startTime = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(startTime);

        // Act
        clock.Advance(TimeSpan.FromMinutes(15));

        // Assert
        clock.UtcNow.Should().Be(startTime.AddMinutes(15));
    }

    [Fact]
    public void Given_DateTimeProvider_When_UtcNowCalled_Then_ReturnsValidUtcTimestamp()
    {
        // Arrange
        IClock clock = new DateTimeProvider();
        var before = DateTimeOffset.UtcNow;

        // Act
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        // Assert
        now.Offset.Should().Be(TimeSpan.Zero);
        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}

