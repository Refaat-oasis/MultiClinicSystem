namespace Core.Application;

/// <summary>
/// Read model for an ICD-10 diagnosis recorded on a visit.
/// </summary>
public sealed record VisitDiagnosisResult(
    Guid Id,
    Guid VisitId,
    string Icd10Code,
    string TitleAr,
    string TitleEn,
    bool IsPrimary,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

