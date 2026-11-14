namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to enable or disable the adaptive time engine for a specific doctor.
/// </summary>
public sealed class SetDoctorTimeEngineDto
{
    public bool IsEngineEnabled { get; set; }
    public Guid? OperationId { get; set; }
}

