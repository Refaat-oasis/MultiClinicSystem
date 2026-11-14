namespace BuildingBlocks.Domain;

/// <summary>
/// Designates an entity as belonging to a specific clinic tenant.
/// Enforced by both EF Core Global Query Filters and SQL Server Row-Level Security (RLS).
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Gets the unique identifier of the clinic tenant owning this record.
    /// </summary>
    Guid ClinicId { get; }
}

