namespace Platform.Api.ViewModels;

public sealed record VisitDiagnosisViewModel(
    Guid Id,
    Guid VisitId,
    string Icd10Code,
    string TitleAr,
    string TitleEn,
    bool IsPrimary,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

