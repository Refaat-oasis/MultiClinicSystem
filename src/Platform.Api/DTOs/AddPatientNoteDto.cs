namespace Platform.Api.DTOs;

public sealed record AddPatientNoteDto(
    string NoteText,
    bool IsPrivate);

