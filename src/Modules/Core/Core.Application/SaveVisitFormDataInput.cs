namespace Core.Application;

/// <summary>
/// Input model to submit answers against a specialty clinical form template on a visit.
/// </summary>
public sealed record SaveVisitFormDataInput(
    Guid ClinicalFormId,
    string AnswersJson);

