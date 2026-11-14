using Core.Domain;

namespace Core.Application;

/// <summary>
/// Application read model for a sync conflict queued in the clinic's conflict inbox.
/// </summary>
public sealed record SyncConflictResult(
    Guid Id,
    Guid ClinicId,
    Guid OperationId,
    string DeviceId,
    string EntityType,
    Guid EntityId,
    ConflictClass ConflictClass,
    string ConflictCode,
    string Reason,
    string ClientPayloadJson,
    string ServerSnapshotJson,
    SyncConflictStatus Status,
    string? ResolvedByUserId,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolutionNote,
    DateTimeOffset CreatedAtUtc);

