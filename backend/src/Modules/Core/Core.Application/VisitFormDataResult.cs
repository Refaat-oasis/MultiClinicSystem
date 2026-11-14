namespace Core.Application;

/// <summary>
/// Read model for saved specialty form answers linked to a visit.
/// </summary>
public sealed record VisitFormDataResult(
    Guid Id,
    Guid VisitId,
    Guid ClinicalFormId,
    string SpecialtyCode,
    int FormVersion,
    string AnswersJson,
    DateTimeOffset SavedAtUtc);

