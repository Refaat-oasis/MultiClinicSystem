namespace Platform.Api.ViewModels;

public sealed record PatientAttachmentItemViewModel(
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

