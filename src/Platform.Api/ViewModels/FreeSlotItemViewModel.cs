namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a free bookable time slot.
/// </summary>
public sealed record FreeSlotItemViewModel(
    Guid DoctorId,
    string DoctorNameAr,
    string DoctorNameEn,
    Guid? RoomId,
    string? RoomCode,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DurationMinutes);

