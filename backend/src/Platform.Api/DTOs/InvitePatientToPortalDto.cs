namespace Platform.Api.DTOs;

public sealed record InvitePatientToPortalDto(
    string? Email,
    bool IsGuardianAccount,
    string? GuardianName,
    bool ConsentAccepted,
    string? TermsVersion,
    bool IssuePrintedPinIfNoEmail);

