namespace Platform.Api.ViewModels;

public sealed record PatientPortalAdminStatusViewModel(
    Guid PatientId,
    bool HasAccount,
    string? Status,
    string? Email,
    bool IsEmailConfirmed,
    bool IsGuardianAccount,
    string? GuardianName,
    DateTimeOffset? ConsentAcceptedAtUtc,
    string? ConsentTermsVersion,
    DateTimeOffset? LastLoginAtUtc,
    string? GeneratedPrintedPin,
    DateTimeOffset? PrintedPinExpiresAtUtc);

