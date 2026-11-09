using BuildingBlocks.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class GuidV7Tests
{
    [Fact]
    public void Given_GuidV7_When_Generated_Then_VersionIsSeven()
    {
        // Act
        var guid = GuidV7.NewGuid();

        // Assert
        guid.Should().NotBeEmpty();
        guid.Version.Should().Be(7);
    }

    [Fact]
    public void Given_GuidV7Instances_When_GeneratedSequentially_Then_MaintainsMonotonicOrdering()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        const int count = 50;
        var guids = new List<Guid>(count);

        // Act - generate with sequential timestamps
        for (int i = 0; i < count; i++)
        {
            guids.Add(GuidV7.NewGuid(baseTime.AddMilliseconds(i)));
        }

        // Assert - verify big-endian ordering
        for (int i = 0; i < count - 1; i++)
        {
            GuidV7.Compare(guids[i], guids[i + 1]).Should().BeNegative();
        }
    }

    [Fact]
    public void Given_ExplicitTimestamp_When_GeneratingGuidV7_Then_ExtractedTimestampMatchesWithinMillisecond()
    {
        // Arrange
        var expectedTimestamp = DateTimeOffset.UtcNow;

        // Act
        var guid = GuidV7.NewGuid(expectedTimestamp);
        var extractedTimestamp = GuidV7.GetTimestamp(guid);

        // Assert
        extractedTimestamp.ToUnixTimeMilliseconds()
            .Should().Be(expectedTimestamp.ToUnixTimeMilliseconds());
    }
}

