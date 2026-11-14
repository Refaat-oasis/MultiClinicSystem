namespace Core.Application;

/// <summary>
/// Service contract for recording business and security audit events.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string? oldValues,
        string? newValues,
        Guid? userId = null,
        Guid? clinicId = null,
        string? ipAddress = null,
        string? requestId = null,
        CancellationToken cancellationToken = default);
}
