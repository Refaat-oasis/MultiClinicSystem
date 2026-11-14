namespace Core.Domain;

/// <summary>
/// Audit trail entity logging business events, logins, security updates, and data modifications.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? ClinicId { get; private set; }
    public string Action { get; private set; }
    public string EntityName { get; private set; }
    public string? EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? RequestId { get; private set; }

    private AuditLog()
    {
        Action = string.Empty;
        EntityName = string.Empty;
    }

    public AuditLog(
        Guid id,
        Guid? userId,
        Guid? clinicId,
        string action,
        string entityName,
        string? entityId,
        string? oldValues,
        string? newValues,
        DateTimeOffset timestampUtc,
        string? ipAddress,
        string? requestId)
    {
        Id = id;
        UserId = userId;
        ClinicId = clinicId;
        Action = action ?? string.Empty;
        EntityName = entityName ?? string.Empty;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        TimestampUtc = timestampUtc;
        IpAddress = ipAddress;
        RequestId = requestId;
    }
}
