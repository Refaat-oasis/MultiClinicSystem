using Core.Domain;

namespace Core.Application;

/// <summary>
/// Per-operation outcome returned inside a `/sync/push` response.
/// </summary>
public sealed record SyncPushOperationOutcomeResult(
    Guid OperationId,
    string ActionType,
    string EntityType,
    Guid EntityId,
    SyncOperationStatus Status,
    long? CursorSequence,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? ConflictId,
    long ClockSkewSeconds,
    bool IsLargeClockSkew,
    IReadOnlyList<string>? AssignedReceiptNumbers = null);
