using Core.Domain;

namespace Core.Application;

public sealed record PatientPortalAdminStatusResult(
    Guid PatientId,
    bool HasAccount,
    PatientAccountStatus? Status,
    string? Email,
    bool IsEmailConfirmed,
    bool IsGuardianAccount,
    string? GuardianName,
    DateTimeOffset? ConsentAcceptedAtUtc,
    string? ConsentTermsVersion,
    DateTimeOffset? LastLoginAtUtc,
    string? GeneratedPrintedPin,
    DateTimeOffset? PrintedPinExpiresAtUtc,
    int NoShowCount = 0,
    bool IsOnlineBookingBlocked = false,
    string? OnlineBookingBlockReason = null);

