namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to define a clinic-scoped JSON Schema clinical form template for a specialty.
/// </summary>
public sealed class CreateSpecialtyFormDto
{
    public string SpecialtyCode { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string JsonSchema { get; set; } = string.Empty;
}

