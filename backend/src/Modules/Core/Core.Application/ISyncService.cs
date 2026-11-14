using BuildingBlocks.Domain;
using Core.Domain;

namespace Core.Application;

/// <summary>
/// Application service managing Phase 15 offline sync push/pull, number block pre-allocation,
/// conflict inbox resolution, clock-skew telemetry, and Super Admin read-only inspection.
/// </summary>
public interface ISyncService
{
    Task<Result<ClinicSyncSettingResult>> GetSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicSyncSettingResult>> UpdateSettingsAsync(
        Guid userId,
        Guid clinicId,
        UpdateClinicSyncSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<AllocatedNumberBlockResult>> AllocateNumberBlockAsync(
        Guid userId,
        Guid clinicId,
        AllocateNumberBlockInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AllocatedNumberBlockResult>>> ListNumberBlocksAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<SyncPushBatchResult>> PushAsync(
        Guid userId,
        Guid clinicId,
        SyncPushBatchInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<SyncPullResponseResult>> PullAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        long cursor,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SyncConflictResult>>> ListConflictsAsync(
        Guid userId,
        Guid clinicId,
        SyncConflictStatus? status,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<SyncConflictResult>> ResolveConflictAsync(
        Guid userId,
        Guid clinicId,
        Guid conflictId,
        ResolveSyncConflictInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SyncDeviceStateResult>>> ListDevicesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicOfflineSyncInspectResult>> InspectClinicOfflineSyncAsync(
        Guid adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}

