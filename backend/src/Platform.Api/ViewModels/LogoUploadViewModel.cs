namespace Platform.Api.ViewModels;

public sealed record LogoUploadViewModel(
    Guid AssetId,
    string ContentType,
    long SizeBytes,
    string FullStorageKey,
    string ThumbStorageKey);

