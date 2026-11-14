using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;

namespace Core.Infrastructure;

/// <summary>
/// Database persistence implementation of business and security audit tracking.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly CoreDbContext _dbContext;
    private readonly IClock _clock;

    public AuditService(CoreDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string? oldValues,
        string? newValues,
        Guid? userId = null,
        Guid? clinicId = null,
        string? ipAddress = null,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog(
            id: GuidV7.NewGuid(),
            userId: userId,
            clinicId: clinicId,
            action: action,
            entityName: entityName,
            entityId: entityId,
            oldValues: oldValues,
            newValues: newValues,
            timestampUtc: _clock.UtcNow,
            ipAddress: ipAddress,
            requestId: requestId);

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
