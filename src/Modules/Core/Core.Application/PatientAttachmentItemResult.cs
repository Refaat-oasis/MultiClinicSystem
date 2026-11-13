namespace Core.Application;

/// <summary>
/// Metadata read model for a stored patient attachment.
/// </summary>
public sealed record PatientAttachmentItemResult(
    Guid Id,
    Guid PatientId,
    string Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    bool HasThumbnail,
    string? Notes,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAtUtc);

