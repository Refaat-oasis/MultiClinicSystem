namespace Core.Application;

/// <summary>
/// Incremental change item returned by `/sync/pull`.
/// </summary>
public sealed record SyncPullItemResult(
    long CursorSequence,
    Guid OperationId,
    string DeviceId,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string PayloadJson,
    DateTimeOffset ServerReceivedAtUtc);

