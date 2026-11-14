using BuildingBlocks.Domain;

namespace UnitTests;

/// <summary>
/// Test double for deterministic control of time across unit tests.
/// </summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }

    public TestClock(DateTimeOffset initialTime)
    {
        UtcNow = initialTime;
    }

    public TestClock() : this(DateTimeOffset.UtcNow)
    {
    }

    public void Advance(TimeSpan duration)
    {
        UtcNow = UtcNow.Add(duration);
    }
}

