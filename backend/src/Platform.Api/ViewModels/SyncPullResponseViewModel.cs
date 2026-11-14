namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for `/sync/pull`.
/// </summary>
public sealed record SyncPullResponseViewModel(
    long Cursor,
    long NextCursor,
    bool HasMore,
    DateTimeOffset ServerTimestampUtc,
    IReadOnlyList<SyncPullItemViewModel> Changes,
    int OpenConflictsCount);

