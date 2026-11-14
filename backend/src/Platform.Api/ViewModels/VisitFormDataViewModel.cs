namespace Platform.Api.ViewModels;

public sealed record VisitFormDataViewModel(
    Guid Id,
    Guid VisitId,
    Guid ClinicalFormId,
    string SpecialtyCode,
    int FormVersion,
    string AnswersJson,
    DateTimeOffset SavedAtUtc);

