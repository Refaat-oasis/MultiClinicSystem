namespace Core.Application;

public sealed record PublicClinicLandingResult(
    Guid ClinicId,
    string Name,
    string CurrentSlug,
    string RequestedSlug,
    bool IsRedirect,
    string RedirectUrl,
    Guid? LogoAssetId,
    string? Address,
    string? MainPhone,
    string WelcomeMessageAr,
    string WelcomeMessageEn,
    string TermsTextAr,
    string TermsTextEn,
    IReadOnlyList<string> AvailableSections);

