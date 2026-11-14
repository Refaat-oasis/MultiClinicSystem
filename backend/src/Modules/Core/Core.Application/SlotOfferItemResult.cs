namespace Core.Application;

/// <summary>
/// Read model for a freed-slot offer generated on cancellation.
/// </summary>
public sealed record SlotOfferItemResult(
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

