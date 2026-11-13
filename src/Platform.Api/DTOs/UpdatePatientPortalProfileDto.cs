namespace Platform.Api.DTOs;

public sealed record UpdatePatientPortalProfileDto(
    string? Email,
    string? BloodType,
    string? GuardianName,
    string? GuardianEmail);

