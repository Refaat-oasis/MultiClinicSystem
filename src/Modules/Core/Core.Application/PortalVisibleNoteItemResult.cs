namespace Core.Application;

public sealed record PortalVisibleNoteItemResult(
    Guid NoteId,
    string NoteText,
    DateTimeOffset CreatedAtUtc);

