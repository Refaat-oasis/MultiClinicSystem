namespace Platform.Api.DTOs;

public sealed record PatientPortalResetPasswordDto(
    string Email,
    string VerificationCode,
    string NewPassword);

