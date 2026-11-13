namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to add an ICD-10 diagnosis to a clinical visit.
/// </summary>
public sealed class AddVisitDiagnosisDto
{
    public string Icd10Code { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = true;
    public string? Notes { get; set; }
}

