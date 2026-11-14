namespace Core.Application;

/// <summary>
/// Clinic card summary returned for an owner's clinic switcher and My Clinics view.
/// </summary>
public sealed record MyClinicCardResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    string OnboardingStatus,
    string TimeZone,
    string Currency,
    string RoundingRule,
    Guid? LogoAssetId,
    IReadOnlyList<string> ModulesEnabled,
    IReadOnlyList<string> CompletedSteps,
    bool WelcomeSeen,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAtUtc = null);

