namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to issue an allergy-checked prescription for a visit.
/// </summary>
public sealed class CreatePrescriptionDto
{
    public string? Notes { get; set; }
    public bool AcknowledgeAllergyWarning { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = [];
    public Guid? OperationId { get; set; }
}

