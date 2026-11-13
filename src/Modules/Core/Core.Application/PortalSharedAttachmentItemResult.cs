namespace Core.Application;

public sealed record PortalSharedAttachmentItemResult(
    Guid AttachmentId,
    string Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

