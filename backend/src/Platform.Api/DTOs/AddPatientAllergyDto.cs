namespace Platform.Api.DTOs;

public sealed record AddPatientAllergyDto(
    string Allergen,
    string Severity,
    string? Reaction);

