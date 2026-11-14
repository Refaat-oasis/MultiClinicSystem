namespace Platform.Api.ViewModels;

/// <summary>
/// ViewModel returned upon Platform Super Admin login.
/// </summary>
public sealed record PlatformAdminLoginViewModel(
    Guid AdminId,
    string Email,
    string FullName,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    bool RequiresEnrollment,
    bool MustChangePassword,
    string? ManualSetupKey,
    string? OtpAuthUri);

