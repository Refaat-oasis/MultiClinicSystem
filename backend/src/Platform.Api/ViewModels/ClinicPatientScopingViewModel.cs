namespace Platform.Api.ViewModels;

public sealed record ClinicPatientScopingViewModel(
    bool ScopePatientsByDoctor,
    bool ScopePatientsBySpecialty);

