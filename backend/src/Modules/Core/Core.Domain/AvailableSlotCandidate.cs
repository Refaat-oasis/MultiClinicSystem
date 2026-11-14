namespace Core.Domain;

/// <summary>
/// Computed free appointment slot candidate returned by AvailabilityCalculator.
/// </summary>
public sealed record AvailableSlotCandidate(
    Guid DoctorId,
    Guid? RoomId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc);

