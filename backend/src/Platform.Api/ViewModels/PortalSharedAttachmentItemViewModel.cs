namespace Platform.Api.ViewModels;

public sealed record PortalSharedAttachmentItemViewModel(
    Guid AttachmentId,
    string Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

