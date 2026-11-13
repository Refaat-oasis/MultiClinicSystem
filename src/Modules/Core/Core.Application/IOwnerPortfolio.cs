using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Executes per-clinic aggregate queries under each owned clinic's tenant context (RLS active) to build an owner's multi-clinic portfolio.
/// </summary>
public interface IOwnerPortfolio
{
    Task<Result<OwnerPortfolioResult>> GetPortfolioAsync(
        Guid userId,
        IReadOnlyList<Guid>? filterClinicIds,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);
}
