namespace Platform.Api.ViewModels;

/// <summary>
/// ViewModel returned once when hourly TOTP enrollment completes, displaying single-use recovery keys.
/// </summary>
public sealed record TotpEnrollmentCompletedViewModel(
    Guid AdminId,
    string Email,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> RecoveryKeys);

