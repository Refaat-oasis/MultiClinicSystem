namespace BuildingBlocks.Application;

/// <summary>
/// Provides access to the current request's clinic tenant context.
/// </summary>
public interface ICurrentClinic
{
    /// <summary>
    /// The unique identifier of the currently scoped clinic tenant, if present.
    /// </summary>
    Guid? ClinicId { get; }

    /// <summary>
    /// Indicates whether a valid clinic tenant is established for this request.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Indicates whether the active request is operating under platform super admin bypass.
    /// </summary>
    bool IsPlatformAdmin { get; }
}

