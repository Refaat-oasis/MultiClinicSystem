namespace Core.Application;

public sealed record InvitePatientToPortalInput(
    string? Email,
    bool IsGuardianAccount,
    string? GuardianName,
    bool ConsentAccepted,
    string? TermsVersion,
    bool IssuePrintedPinIfNoEmail);

