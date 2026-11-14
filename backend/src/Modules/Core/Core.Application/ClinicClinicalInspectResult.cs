namespace Core.Application;

/// <summary>
/// Super Admin read-only inspection snapshot for a clinic's clinical visits and forms.
/// </summary>
public sealed record ClinicClinicalInspectResult(
    Guid ClinicId,
    IReadOnlyList<VisitSessionResult> Visits,
    IReadOnlyList<ClinicalFormResult> Forms);

