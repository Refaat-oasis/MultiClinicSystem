namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for updating an owner's maximum clinic quota.
/// </summary>
public sealed class UpdateOwnerQuotaDto
{
    public int? MaxClinics { get; set; }

    public string? StepUpPassword { get; set; }
}

