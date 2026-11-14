namespace Platform.Api.ViewModels;

public sealed record MyClinicCardViewModel(
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

