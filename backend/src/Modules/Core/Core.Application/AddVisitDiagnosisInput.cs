namespace Core.Application;

/// <summary>
/// Input model to record an ICD-10 diagnosis for a visit.
/// </summary>
public sealed record AddVisitDiagnosisInput(
    string Icd10Code,
    string TitleAr,
    string TitleEn,
    bool IsPrimary,
    string? Notes);

