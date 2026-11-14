namespace Core.Application;

public sealed record PatientPortalResetPasswordInput(
    string Email,
    string VerificationCode,
    string NewPassword);

