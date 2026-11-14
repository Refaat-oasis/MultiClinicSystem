namespace Core.Domain;

/// <summary>
/// Snapshot item used by QueueReorderCalculator to compute FIFO, VIP jump, and push-back orderings.
/// </summary>
public sealed record QueueReorderEntryInput(
    Guid Id,
    AppointmentPriority Priority,
    int OrderIndex,
    DateTimeOffset CreatedAtUtc);

