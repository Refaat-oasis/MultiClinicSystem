namespace BuildingBlocks.Domain;

/// <summary>
/// Abstraction for system time to ensure deterministic time testing in UTC.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}

