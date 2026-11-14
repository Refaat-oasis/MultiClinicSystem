namespace Core.Application;

/// <summary>
/// Single offline operation submitted in a `/sync/push` batch.
/// </summary>
public sealed record SyncPushOperationInput(
    Guid OperationId,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string PayloadJson,
    DateTimeOffset ClientTimestampUtc,
    long? ClientBaseCursor = null);

