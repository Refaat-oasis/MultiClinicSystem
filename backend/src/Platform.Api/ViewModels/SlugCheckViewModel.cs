namespace Platform.Api.ViewModels;

public sealed record SlugCheckViewModel(
    string Slug,
    bool IsValid,
    bool IsAvailable,
    string? ErrorCode,
    string? Reason,
    string SuggestedAlternative,
    string PreviewUrl);

