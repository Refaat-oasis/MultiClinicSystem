using Core.Domain;

namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for an open or resolved sync conflict in the conflict inbox.
/// </summary>
public sealed record SyncConflictViewModel(
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

