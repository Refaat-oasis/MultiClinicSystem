namespace Core.Application;

public sealed record ActivatePatientPortalInput(
    string Email,
    string VerificationCode,
    string Password);

