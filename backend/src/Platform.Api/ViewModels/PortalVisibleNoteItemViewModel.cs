namespace Platform.Api.ViewModels;

public sealed record PortalVisibleNoteItemViewModel(
    Guid NoteId,
    string NoteText,
    DateTimeOffset CreatedAtUtc);

