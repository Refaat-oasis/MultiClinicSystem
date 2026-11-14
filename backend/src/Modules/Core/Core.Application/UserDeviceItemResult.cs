namespace Core.Application;

/// <summary>
/// Active or revoked user device item returned in user profile.
/// </summary>
public sealed record UserDeviceItemResult(
    Guid Id,
    string DeviceId,
    string DeviceName,
    bool IsApproved,
    bool IsRevoked,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc);

