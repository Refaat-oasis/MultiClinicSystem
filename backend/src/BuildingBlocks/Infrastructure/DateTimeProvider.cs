using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Production system implementation of IClock returning real UTC time.
/// </summary>
public sealed class DateTimeProvider : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

