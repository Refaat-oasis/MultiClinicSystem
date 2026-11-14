namespace Platform.Api.DTOs;

/// <summary>
/// Request item representing a single prescribed medication line.
/// </summary>
public sealed class PrescriptionItemDto
{
    public string DrugName { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? Instructions { get; set; }
}

