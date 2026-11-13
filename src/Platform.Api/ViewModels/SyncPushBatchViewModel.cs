namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for `/sync/push` batch results.
/// </summary>
public sealed record SyncPushBatchViewModel(
    string DeviceId,
    long LatestCursor,
    DateTimeOffset ServerTimestampUtc,
    int AppliedCount,
    int DuplicateCount,
    int ConflictCount,
    int RejectedCount,
    IReadOnlyList<SyncPushOperationOutcomeViewModel> Outcomes);

