namespace Platform.Api.DTOs;

public sealed record ActivatePatientPortalDto(
    string Email,
    string VerificationCode,
    string Password);

