namespace Platform.Api.ViewModels;

public sealed record PortalVisitSummaryViewModel(
    Guid VisitId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string DoctorName,
    string SpecialtyCode,
    string? DiagnosisSummary,
    string? Advice,
    DateOnly? FollowUpDate,
    IReadOnlyList<VisitDiagnosisViewModel> Diagnoses,
    IReadOnlyList<VisitProcedureViewModel> Procedures,
    IReadOnlyList<PrescriptionViewModel> Prescriptions);

