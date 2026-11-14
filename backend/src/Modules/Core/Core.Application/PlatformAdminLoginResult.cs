namespace Core.Application;

/// <summary>
/// Application result of a Super Admin login attempt.
/// </summary>
public sealed record PlatformAdminLoginResult(
    Guid AdminId,
    string Email,
    string FullName,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    bool RequiresEnrollment,
    bool MustChangePassword,
    string? ManualSetupKey,
    string? OtpAuthUri);

