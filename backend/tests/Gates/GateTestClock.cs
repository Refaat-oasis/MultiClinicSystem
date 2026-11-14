using BuildingBlocks.Domain;

namespace Gates;

/// <summary>
/// Controllable UTC clock for deterministic Phase 2 gate verification across hourly TOTP windows.
/// </summary>
public sealed class GateTestClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 6, 15, 10, 15, 0, TimeSpan.Zero);

    public void SetTime(DateTimeOffset newTimeUtc)
    {
        UtcNow = newTimeUtc.ToUniversalTime();
    }

    public void Advance(TimeSpan delta)
    {
        UtcNow = UtcNow.Add(delta);
    }
}

