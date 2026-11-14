namespace Core.Domain;

/// <summary>
/// Existing occupied interval for a doctor and optional room.
/// </summary>
public sealed record AvailabilityBusyInterval(
    Guid DoctorId,
    Guid? RoomId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc);

