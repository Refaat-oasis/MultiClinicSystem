namespace Core.Application;

/// <summary>
/// Detailed owner profile along with assigned clinics.
/// </summary>
public sealed record OwnerDetailResult(
    Guid OwnerId,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Status,
    bool EmailConfirmed,
    int? MaxClinics,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ClinicSummaryResult> Clinics);

