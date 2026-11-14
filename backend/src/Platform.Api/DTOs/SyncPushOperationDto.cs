namespace Platform.Api.DTOs;

/// <summary>
/// HTTP request item representing a single offline operation in a push batch.
/// </summary>
public sealed record SyncPushOperationDto(
    Guid OperationId,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string PayloadJson,
    DateTimeOffset ClientTimestampUtc,
    long? ClientBaseCursor = null);

