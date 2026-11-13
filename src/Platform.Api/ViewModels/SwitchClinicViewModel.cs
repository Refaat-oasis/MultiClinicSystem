namespace Platform.Api.ViewModels;

public sealed record SwitchClinicViewModel(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    string OnboardingStatus,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);

