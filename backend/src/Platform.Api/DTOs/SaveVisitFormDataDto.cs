namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to validate and persist structured specialty form answers on a visit.
/// </summary>
public sealed class SaveVisitFormDataDto
{
    public Guid ClinicalFormId { get; set; }
    public string AnswersJson { get; set; } = "{}";
}

