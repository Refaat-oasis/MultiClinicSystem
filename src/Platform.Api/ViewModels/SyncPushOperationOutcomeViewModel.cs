using Core.Domain;

namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for an individual synced operation outcome.
/// </summary>
public sealed record SyncPushOperationOutcomeViewModel(
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
    IReadOnlyList<string>? AssignedReceiptNumbers);
