using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Executes operations under Platform Admin bypass authority and logs access.
/// </summary>
public sealed class PlatformAccessService : IPlatformAccess
{
    private readonly CurrentClinicContext _context;

    public PlatformAccessService(CurrentClinicContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TResult> ExecuteAsPlatformAdminAsync<TResult>(
        Func<Task<TResult>> operation,
        string reason,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var originalClinicId = _context.ClinicId;
        var wasPlatformAdmin = _context.IsPlatformAdmin;

        try
        {
            _context.SetPlatformAdmin();
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            if (wasPlatformAdmin)
            {
                _context.SetPlatformAdmin();
            }
            else if (originalClinicId.HasValue)
            {
                _context.SetTenant(originalClinicId.Value);
            }
            else
            {
                _context.Clear();
            }
        }
    }
}

