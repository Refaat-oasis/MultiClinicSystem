namespace Core.Application;

/// <summary>
/// Read-only clinic membership summary shown on the user's profile page.
/// </summary>
public sealed record UserClinicMembershipResult(
    Guid ClinicId,
    string ClinicName,
    string Slug,
    string Role,
    string Status,
    string OnboardingStatus);

