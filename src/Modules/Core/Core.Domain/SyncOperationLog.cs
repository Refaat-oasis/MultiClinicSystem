using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Append-only audit log of every sync push operation received by the server, with clock-skew telemetry and monotonic cursor.
/// </summary>
public sealed class SyncOperationLog : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string DeviceId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OperationId { get; private set; }
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public string ActionType { get; private set; }
    public ConflictClass ConflictClass { get; private set; }
    public long CursorSequence { get; private set; }
    public DateTimeOffset ClientTimestampUtc { get; private set; }
    public DateTimeOffset ServerReceivedAtUtc { get; private set; }
    public long ClockSkewSeconds { get; private set; }
    public bool IsLargeClockSkew { get; private set; }
    public SyncOperationStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? PayloadJson { get; private set; }
    public bool IsVoided { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SyncOperationLog()
    {
        DeviceId = string.Empty;
        EntityType = string.Empty;
        ActionType = string.Empty;
    }

    public SyncOperationLog(
        Guid id,
        Guid clinicId,
        string deviceId,
        Guid userId,
        Guid operationId,
        string entityType,
        Guid entityId,
        string actionType,
        ConflictClass conflictClass,
        long cursorSequence,
        DateTimeOffset clientTimestampUtc,
        DateTimeOffset serverReceivedAtUtc,
        long clockSkewSeconds,
        bool isLargeClockSkew,
        SyncOperationStatus status,
        string? errorCode,
        string? errorMessage,
        string? payloadJson)
    {
        Id = id;
        ClinicId = clinicId;
        DeviceId = deviceId.Trim();
        UserId = userId;
        OperationId = operationId;
        EntityType = entityType.Trim();
        EntityId = entityId;
        ActionType = actionType.Trim();
        ConflictClass = conflictClass;
        CursorSequence = cursorSequence;
        ClientTimestampUtc = clientTimestampUtc;
        ServerReceivedAtUtc = serverReceivedAtUtc;
        ClockSkewSeconds = clockSkewSeconds;
        IsLargeClockSkew = isLargeClockSkew;
        Status = status;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        PayloadJson = payloadJson;
    }
}

