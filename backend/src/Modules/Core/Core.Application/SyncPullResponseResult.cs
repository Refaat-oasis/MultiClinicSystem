namespace Core.Application;

/// <summary>
/// Response model for `/sync/pull?cursor=...`.
/// </summary>
public sealed record SyncPullResponseResult(
    long Cursor,
    long NextCursor,
    bool HasMore,
    DateTimeOffset ServerTimestampUtc,
    IReadOnlyList<SyncPullItemResult> Changes,
    int OpenConflictsCount);

