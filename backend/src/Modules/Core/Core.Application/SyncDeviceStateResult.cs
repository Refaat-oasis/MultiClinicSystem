namespace Core.Application;

/// <summary>
/// Telemetry snapshot for a registered offline sync device.
/// </summary>
public sealed record SyncDeviceStateResult(
    Guid Id,
    Guid ClinicId,
    string DeviceId,
    Guid UserId,
    long LastSyncCursor,
    DateTimeOffset? LastPushAtUtc,
    DateTimeOffset? LastPullAtUtc,
    DateTimeOffset LastOnlineAuthAtUtc,
    long MaxClockSkewSecondsObserved,
    int PendingOperationsReported,
    bool IsApproved,
    bool IsRevoked);
