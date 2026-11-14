using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Hand-written mapper from offline sync application results to API ViewModels.
/// </summary>
public static class SyncViewModelMapper
{
    public static ClinicSyncSettingViewModel ToViewModel(ClinicSyncSettingResult r) => new(
        r.ClinicId,
        r.OfflineSessionMaxHours,
        r.ReceiptBlockSize,
        r.ReceiptBlockLowWaterMark,
        r.ClockSkewWarningSeconds,
        r.UpdatedAtUtc);

    public static SyncPushBatchViewModel ToViewModel(SyncPushBatchResult r) => new(
        r.DeviceId,
        r.LatestCursor,
        r.ServerTimestampUtc,
        r.AppliedCount,
        r.DuplicateCount,
        r.ConflictCount,
        r.RejectedCount,
        r.Outcomes.Select(o => new SyncPushOperationOutcomeViewModel(
            o.OperationId,
            o.ActionType,
            o.EntityType,
            o.EntityId,
            o.Status,
            o.CursorSequence,
            o.ErrorCode,
            o.ErrorMessage,
            o.ConflictId,
            o.ClockSkewSeconds,
            o.IsLargeClockSkew,
            o.AssignedReceiptNumbers)).ToList());

    public static SyncPullResponseViewModel ToViewModel(SyncPullResponseResult r) => new(
        r.Cursor,
        r.NextCursor,
        r.HasMore,
        r.ServerTimestampUtc,
        r.Changes.Select(c => new SyncPullItemViewModel(
            c.CursorSequence,
            c.OperationId,
            c.DeviceId,
            c.ActionType,
            c.EntityType,
            c.EntityId,
            c.PayloadJson,
            c.ServerReceivedAtUtc)).ToList(),
        r.OpenConflictsCount);

    public static SyncConflictViewModel ToViewModel(SyncConflictResult r) => new(
        r.Id,
        r.ClinicId,
        r.OperationId,
        r.DeviceId,
        r.EntityType,
        r.EntityId,
        r.ConflictClass,
        r.ConflictCode,
        r.Reason,
        r.ClientPayloadJson,
        r.ServerSnapshotJson,
        r.Status,
        r.ResolvedByUserId,
        r.ResolvedAtUtc,
        r.ResolutionNote,
        r.CreatedAtUtc);

    public static AllocatedNumberBlockViewModel ToViewModel(AllocatedNumberBlockResult r) => new(
        r.Id,
        r.ClinicId,
        r.DeviceId,
        r.Kind,
        r.FromNumber,
        r.ToNumber,
        r.NextNumber,
        r.RemainingCount,
        r.Status,
        r.IssuedAtUtc);

    public static SyncDeviceStateViewModel ToViewModel(SyncDeviceStateResult r) => new(
        r.Id,
        r.ClinicId,
        r.DeviceId,
        r.UserId,
        r.LastSyncCursor,
        r.LastPushAtUtc,
        r.LastPullAtUtc,
        r.LastOnlineAuthAtUtc,
        r.MaxClockSkewSecondsObserved,
        r.PendingOperationsReported,
        r.IsApproved,
        r.IsRevoked);

    public static ClinicOfflineSyncInspectViewModel ToViewModel(ClinicOfflineSyncInspectResult r) => new(
        r.ClinicId,
        ToViewModel(r.Settings),
        r.Devices.Select(ToViewModel).ToList(),
        r.Conflicts.Select(ToViewModel).ToList(),
        r.NumberBlocks.Select(ToViewModel).ToList());
}

