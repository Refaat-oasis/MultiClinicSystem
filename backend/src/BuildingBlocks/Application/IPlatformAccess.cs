namespace BuildingBlocks.Application;

/// <summary>
/// Controls guarded Super Admin platform bypass access.
/// Every bypass execution writes an immutable audit record to PatientAccessLog.
/// </summary>
public interface IPlatformAccess
{
    /// <summary>
    /// Executes the specified operation under Platform Admin bypass authority.
    /// </summary>
    Task<TResult> ExecuteAsPlatformAdminAsync<TResult>(
        Func<Task<TResult>> operation,
        string reason,
        string actorId,
        CancellationToken cancellationToken = default);
}

