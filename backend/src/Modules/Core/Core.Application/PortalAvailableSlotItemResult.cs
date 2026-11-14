namespace Core.Application;

public sealed record PortalAvailableSlotItemResult(
    Guid DoctorId,
    string? DoctorName,
    Guid? RoomId,
    string LocalDate,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    int DurationMinutes);
