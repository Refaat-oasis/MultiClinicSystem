namespace Core.Application;

/// <summary>
/// Clinic-level patient visibility scoping configuration.
/// </summary>
public sealed record ClinicPatientScopingResult(
    bool ScopePatientsByDoctor,
    bool ScopePatientsBySpecialty);

