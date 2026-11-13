namespace Core.Application;

/// <summary>
/// Public clinic resolution result supporting canonical redirects from historical slugs.
/// </summary>
public sealed record PublicClinicResolutionResult(
    Guid ClinicId,
    string Name,
    string CurrentSlug,
    string RequestedSlug,
    bool IsRedirect,
    string RedirectUrl,
    Guid? LogoAssetId);

