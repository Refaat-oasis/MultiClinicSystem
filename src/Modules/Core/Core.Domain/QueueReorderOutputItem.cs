namespace Core.Domain;

/// <summary>
/// Calculated order output for a waiting queue entry.
/// </summary>
public sealed record QueueReorderOutputItem(
    Guid Id,
    int NewOrderIndex);

