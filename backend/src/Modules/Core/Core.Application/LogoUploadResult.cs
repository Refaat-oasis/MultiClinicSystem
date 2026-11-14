namespace Core.Application;

/// <summary>
/// Metadata returned after sanitizing and persisting a clinic logo image.
/// </summary>
public sealed record LogoUploadResult(
    Guid AssetId,
    string ContentType,
    long SizeBytes,
    string FullStorageKey,
    string ThumbStorageKey);

