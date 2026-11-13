namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a single incremental pull change.
/// </summary>
public sealed record SyncPullItemViewModel(
    long CursorSequence,
    Guid OperationId,
    string DeviceId,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string PayloadJson,
    DateTimeOffset ServerReceivedAtUtc);

