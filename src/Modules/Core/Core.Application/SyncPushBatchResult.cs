namespace Core.Application;

/// <summary>
/// Result of processing a `/sync/push` batch.
/// </summary>
public sealed record SyncPushBatchResult(
    string DeviceId,
    long LatestCursor,
    DateTimeOffset ServerTimestampUtc,
    int AppliedCount,
    int DuplicateCount,
    int ConflictCount,
    int RejectedCount,
    IReadOnlyList<SyncPushOperationOutcomeResult> Outcomes);

