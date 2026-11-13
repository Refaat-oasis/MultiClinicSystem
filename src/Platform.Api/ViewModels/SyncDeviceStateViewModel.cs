namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for registered device sync status.
/// </summary>
public sealed record SyncDeviceStateViewModel(
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
