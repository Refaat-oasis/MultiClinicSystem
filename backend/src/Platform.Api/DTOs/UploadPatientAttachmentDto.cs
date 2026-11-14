namespace Platform.Api.DTOs;

public sealed record UploadPatientAttachmentDto(
    string Category,
    string FileName,
    string ContentType,
    string ContentBase64,
    string? Notes);

