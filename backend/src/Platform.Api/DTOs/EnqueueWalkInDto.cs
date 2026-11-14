namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to add a walk-in patient to a doctor's live queue.
/// </summary>
public sealed class EnqueueWalkInDto
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string Priority { get; set; } = "Normal";
    public Guid? OperationId { get; set; }
}

