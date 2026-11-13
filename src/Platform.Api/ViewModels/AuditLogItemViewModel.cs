namespace Platform.Api.ViewModels;

/// <summary>
/// Audit log entry view model for platform audit trail viewer.
/// </summary>
public sealed record AuditLogItemViewModel(
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

