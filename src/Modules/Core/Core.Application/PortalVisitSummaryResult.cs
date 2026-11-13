namespace Core.Application;

/// <summary>
/// Patient-safe clinical visit summary for the patient portal (excludes doctor-private notes and hidden visits).
/// </summary>
public sealed record PortalVisitSummaryResult(
    Guid VisitId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string DoctorName,
    string SpecialtyCode,
    string? DiagnosisSummary,
    string? Advice,
    DateOnly? FollowUpDate,
    IReadOnlyList<VisitDiagnosisResult> Diagnoses,
    IReadOnlyList<VisitProcedureResult> Procedures,
    IReadOnlyList<PrescriptionResult> Prescriptions);

