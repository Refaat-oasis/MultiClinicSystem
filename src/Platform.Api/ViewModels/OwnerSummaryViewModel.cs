namespace Platform.Api.ViewModels;

/// <summary>
/// Owner summary presentation model for owner directory listings.
/// </summary>
public sealed record OwnerSummaryViewModel(
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
    IReadOnlyList<ClinicSummaryViewModel> Clinics);

