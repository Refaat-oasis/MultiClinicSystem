namespace Core.Application;

/// <summary>
/// Read model for a clinical or doctor-private patient note.
/// </summary>
public sealed record PatientNoteItemResult(
    Guid Id,
    Guid PatientId,
    Guid? DoctorUserId,
    string NoteText,
    bool IsPrivate,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

