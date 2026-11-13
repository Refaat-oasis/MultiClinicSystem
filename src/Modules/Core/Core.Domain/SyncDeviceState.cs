using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Tracks registered offline-capable clinic device sync cursors, clock skew, and offline auth windows.
/// </summary>
public sealed class SyncDeviceState : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string DeviceId { get; private set; }
    public Guid UserId { get; private set; }
    public long LastSyncCursor { get; private set; }
    public DateTimeOffset? LastPushAtUtc { get; private set; }
    public DateTimeOffset? LastPullAtUtc { get; private set; }
    public DateTimeOffset LastOnlineAuthAtUtc { get; private set; }
    public long MaxClockSkewSecondsObserved { get; private set; }
    public int PendingOperationsReported { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SyncDeviceState()
    {
        DeviceId = string.Empty;
    }

    public SyncDeviceState(
        Guid id,
        Guid clinicId,
        string deviceId,
        Guid userId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId cannot be empty.", nameof(clinicId));
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("DeviceId cannot be empty.", nameof(deviceId));
        }

        Id = id;
        ClinicId = clinicId;
        DeviceId = deviceId.Trim();
        UserId = userId;
        LastSyncCursor = 0;
        LastOnlineAuthAtUtc = nowUtc;
        CreatedAtUtc = nowUtc;
    }

    public void RecordPush(long latestCursor, long skewSeconds, int pendingReported, DateTimeOffset nowUtc)
    {
        if (latestCursor > LastSyncCursor)
        {
            LastSyncCursor = latestCursor;
        }
        if (skewSeconds > MaxClockSkewSecondsObserved)
        {
            MaxClockSkewSecondsObserved = skewSeconds;
        }
        PendingOperationsReported = Math.Max(0, pendingReported);
        LastPushAtUtc = nowUtc;
    }

    public void RecordPull(long cursor, DateTimeOffset nowUtc)
    {
        if (cursor > LastSyncCursor)
        {
            LastSyncCursor = cursor;
        }
        LastPullAtUtc = nowUtc;
    }

    public void RefreshOnlineAuth(DateTimeOffset nowUtc)
    {
        LastOnlineAuthAtUtc = nowUtc;
    }
}

