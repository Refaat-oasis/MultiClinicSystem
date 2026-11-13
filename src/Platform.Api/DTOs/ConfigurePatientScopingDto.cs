namespace Platform.Api.DTOs;

public sealed record ConfigurePatientScopingDto(
    bool ScopePatientsByDoctor,
    bool ScopePatientsBySpecialty);

