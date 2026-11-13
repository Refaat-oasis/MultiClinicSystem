namespace Core.Application;

/// <summary>
/// Live slug validation and availability check response with suggestion and public link preview.
/// </summary>
public sealed record SlugCheckResult(
    string Slug,
    bool IsValid,
    bool IsAvailable,
    string? ErrorCode,
    string? Reason,
    string SuggestedAlternative,
    string PreviewUrl);

