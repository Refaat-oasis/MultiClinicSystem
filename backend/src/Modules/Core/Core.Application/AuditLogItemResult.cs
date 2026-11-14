namespace Core.Application;

/// <summary>
/// Audit log entry representation for Super Admin audit viewer.
/// </summary>
public sealed record AuditLogItemResult(
    Guid Id,
    Guid? UserId,
    Guid? ClinicId,
    string Action,
    string EntityName,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    DateTimeOffset TimestampUtc,
    string? IpAddress,
    string? RequestId);

