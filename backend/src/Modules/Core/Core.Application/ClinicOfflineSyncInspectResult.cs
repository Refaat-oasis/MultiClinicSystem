namespace Core.Application;

/// <summary>
/// Super Admin read-only inspection snapshot for a clinic's offline sync module.
/// </summary>
public sealed record ClinicOfflineSyncInspectResult(
    Guid ClinicId,
    ClinicSyncSettingResult Settings,
    IReadOnlyList<SyncDeviceStateResult> Devices,
    IReadOnlyList<SyncConflictResult> Conflicts,
    IReadOnlyList<AllocatedNumberBlockResult> NumberBlocks);

