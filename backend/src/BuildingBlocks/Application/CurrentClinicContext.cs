namespace BuildingBlocks.Application;

/// <summary>
/// Scoped ambient context holding the current clinic tenant and authorization state.
/// </summary>
public sealed class CurrentClinicContext : ICurrentClinic
{
    public Guid? ClinicId { get; private set; }
    public bool HasTenant => ClinicId.HasValue && ClinicId.Value != Guid.Empty;
    public bool IsPlatformAdmin { get; private set; }

    public void SetTenant(Guid clinicId)
    {
        ClinicId = clinicId;
        IsPlatformAdmin = false;
    }

    public void SetPlatformAdmin()
    {
        IsPlatformAdmin = true;
    }

    public void Clear()
    {
        ClinicId = null;
        IsPlatformAdmin = false;
    }
}

