namespace Platform.Api.ViewModels;

public sealed record ClinicClinicalInspectViewModel(
    Guid ClinicId,
    IReadOnlyList<VisitSessionViewModel> Visits,
    IReadOnlyList<ClinicalFormViewModel> Forms);

