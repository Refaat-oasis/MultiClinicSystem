namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a freed-slot offer to a waiting-list patient.
/// </summary>
public sealed record SlotOfferItemViewModel(
    Guid Id,
    Guid ClinicId,
    Guid WaitingListEntryId,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    Guid? RoomId,
    Guid CategoryId,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    string Status,
    DateTimeOffset OfferedAtUtc);

