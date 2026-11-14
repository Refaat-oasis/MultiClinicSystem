using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class NumberBlockTests
{
    [Fact]
    public void Given_InvalidBoundsOrKind_When_ConstructingNumberBlock_Then_ThrowsArgumentException()
    {
        var actInvalidKind = () => new NumberBlock(Guid.NewGuid(), Guid.NewGuid(), "dev-1", "InvalidKind", 1, 500, DateTimeOffset.UtcNow);
        var actInvalidFrom = () => new NumberBlock(Guid.NewGuid(), Guid.NewGuid(), "dev-1", "Receipt", 0, 500, DateTimeOffset.UtcNow);
        var actReversedBounds = () => new NumberBlock(Guid.NewGuid(), Guid.NewGuid(), "dev-1", "Receipt", 500, 100, DateTimeOffset.UtcNow);

        actInvalidKind.Should().Throw<ArgumentException>();
        actInvalidFrom.Should().Throw<ArgumentException>();
        actReversedBounds.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Given_ActiveNumberBlock_When_ConsumingSequentiallyUntilExhausted_Then_AllocatesAndStopsAtBoundary()
    {
        var block = new NumberBlock(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "tablet-reception-1",
            "Receipt",
            fromNumber: 1,
            toNumber: 3,
            createdAtUtc: DateTimeOffset.UtcNow);

        var r1 = block.ConsumeNext();
        var r2 = block.ConsumeNext();
        var r3 = block.ConsumeNext();
        var r4 = block.ConsumeNext();

        r1.IsSuccess.Should().BeTrue();
        r1.Value.Should().Be(1);
        r2.IsSuccess.Should().BeTrue();
        r2.Value.Should().Be(2);
        r3.IsSuccess.Should().BeTrue();
        r3.Value.Should().Be(3);
        block.Status.Should().Be("Exhausted");

        r4.IsFailure.Should().BeTrue();
        r4.Error.Code.Should().Be("NUMBER_BLOCK_EXHAUSTED");
    }
}

