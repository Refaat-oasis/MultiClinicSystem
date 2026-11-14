namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to log a clinical procedure performed during a visit.
/// </summary>
public sealed class AddVisitProcedureDto
{
    public string ProcedureCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ToothOrSite { get; set; }
    public string? Notes { get; set; }
}

