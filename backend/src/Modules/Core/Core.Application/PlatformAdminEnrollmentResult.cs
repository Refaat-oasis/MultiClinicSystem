namespace Core.Application;

/// <summary>
/// Application result of completing forced Super Admin password change & hourly TOTP enrollment.
/// </summary>
public sealed record PlatformAdminEnrollmentResult(
    Guid AdminId,
    string Email,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> RecoveryKeys);

