namespace Core.Application;

/// <summary>
/// Summary of a clinic owner on the platform.
/// </summary>
public sealed record OwnerSummaryResult(
    Guid OwnerId,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Status,
    bool EmailConfirmed,
    int? MaxClinics,
    int ClinicsCount,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ClinicSummaryResult> Clinics);

