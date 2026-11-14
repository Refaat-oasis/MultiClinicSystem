namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to add a patient to the freed-slot waiting list.
/// </summary>
public sealed class AddToWaitingListDto
{
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? CategoryId { get; set; }
    public string PreferredDate { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? OperationId { get; set; }
}

