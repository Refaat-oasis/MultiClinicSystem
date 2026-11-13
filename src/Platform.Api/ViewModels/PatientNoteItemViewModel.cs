namespace Platform.Api.ViewModels;

public sealed record PatientNoteItemViewModel(
    Guid Id,
    Guid PatientId,
    Guid? DoctorUserId,
    string NoteText,
    bool IsPrivate,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

