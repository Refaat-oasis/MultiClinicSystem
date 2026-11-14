namespace Core.Application;

/// <summary>
/// Available appointment slot computed by IAvailabilityQueries.
/// </summary>
public sealed record FreeSlotItemResult(
    Guid DoctorId,
    string DoctorNameAr,
    string DoctorNameEn,
    Guid? RoomId,
    string? RoomCode,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DurationMinutes);

