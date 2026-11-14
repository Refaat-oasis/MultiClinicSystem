namespace Platform.Api.ViewModels;

public sealed record PublicClinicResolutionViewModel(
    Guid ClinicId,
    string Name,
    string CurrentSlug,
    string RequestedSlug,
    bool IsRedirect,
    string RedirectUrl,
    Guid? LogoAssetId);

