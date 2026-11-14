namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to update clinic-level average-time engine settings.
/// </summary>
public sealed class UpdateClinicTimeEngineSettingDto
{
    public int MinSampleSize { get; set; }
    public decimal TrimFraction { get; set; }
    public decimal MaxStepChangePercent { get; set; }
    public Guid? OperationId { get; set; }
}

