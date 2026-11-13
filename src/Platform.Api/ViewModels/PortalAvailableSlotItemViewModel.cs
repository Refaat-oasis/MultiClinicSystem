namespace Platform.Api.ViewModels;

public sealed record PortalAvailableSlotItemViewModel(
    Guid DoctorId,
    string? DoctorName,
    Guid? RoomId,
    string LocalDate,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    int DurationMinutes);
