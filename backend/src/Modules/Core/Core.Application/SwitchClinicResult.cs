namespace Core.Application;

/// <summary>
/// Result returned after switching the active clinic context and minting an updated JWT/refresh pair.
/// </summary>
public sealed record SwitchClinicResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    string OnboardingStatus,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);

