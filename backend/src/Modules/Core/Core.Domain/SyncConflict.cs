using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents an unresolved or resolved offline synchronization conflict placed in the clinic conflict inbox.
/// </summary>
public sealed class SyncConflict : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string DeviceId { get; private set; }
    public Guid OperationId { get; private set; }
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public ConflictClass ConflictClass { get; private set; }
    public string ConflictCode { get; private set; }
    public string Reason { get; private set; }
    public string ClientPayloadJson { get; private set; }
    public string ServerSnapshotJson { get; private set; }
    public SyncConflictStatus Status { get; private set; }
    public string? ResolutionNote { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsVoided { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SyncConflict()
    {
        DeviceId = string.Empty;
        EntityType = string.Empty;
        ConflictCode = string.Empty;
        Reason = string.Empty;
        ClientPayloadJson = "{}";
        ServerSnapshotJson = "{}";
    }

    public SyncConflict(
        Guid id,
        Guid clinicId,
        string deviceId,
        Guid operationId,
        string entityType,
        Guid entityId,
        ConflictClass conflictClass,
        string conflictCode,
        string reason,
        string clientPayloadJson,
        string serverSnapshotJson,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Conflict id cannot be empty.", nameof(id));
        }
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId cannot be empty.", nameof(clinicId));
        }

        Id = id;
        ClinicId = clinicId;
        DeviceId = deviceId.Trim();
        OperationId = operationId;
        EntityType = entityType.Trim();
        EntityId = entityId;
        ConflictClass = conflictClass;
        ConflictCode = conflictCode.Trim();
        Reason = reason.Trim();
        ClientPayloadJson = string.IsNullOrWhiteSpace(clientPayloadJson) ? "{}" : clientPayloadJson;
        ServerSnapshotJson = string.IsNullOrWhiteSpace(serverSnapshotJson) ? "{}" : serverSnapshotJson;
        Status = SyncConflictStatus.Open;
        CreatedAtUtc = createdAtUtc;
    }

    public void Resolve(SyncConflictStatus status, string? resolutionNote, Guid resolvedByUserId, DateTimeOffset nowUtc)
    {
        if (status == SyncConflictStatus.Open)
        {
            throw new ArgumentException("Resolution status cannot revert to Open.", nameof(status));
        }

        Status = status;
        ResolutionNote = resolutionNote?.Trim();
        ResolvedByUserId = resolvedByUserId;
        ResolvedAtUtc = nowUtc;
    }
}

