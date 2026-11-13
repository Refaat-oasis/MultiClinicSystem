namespace Platform.Api.DTOs;

public sealed record UploadLogoDto(
    string FileName,
    string ContentType,
    string ContentBase64);

