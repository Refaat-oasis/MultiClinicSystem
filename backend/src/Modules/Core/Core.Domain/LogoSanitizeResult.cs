namespace Core.Domain;

/// <summary>
/// Output from image sanitization containing cleaned full and thumbnail image payloads.
/// </summary>
public sealed record LogoSanitizeResult(
    string ContentType,
    byte[] FullBytes,
    byte[] ThumbBytes);

