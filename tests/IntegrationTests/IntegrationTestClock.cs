using BuildingBlocks.Domain;

namespace IntegrationTests;

/// <summary>
/// Controllable UTC clock for integration tests.
/// </summary>
public sealed class IntegrationTestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 6, 15, 10, 15, 0, TimeSpan.Zero);

    public void Advance(TimeSpan delta)
    {
        UtcNow = UtcNow.Add(delta);
    }
}

