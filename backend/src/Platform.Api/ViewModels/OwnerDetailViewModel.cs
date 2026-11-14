namespace Platform.Api.ViewModels;

/// <summary>
/// Detailed owner profile view model including child clinics.
/// </summary>
public sealed record OwnerDetailViewModel(
    Guid OwnerId,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Status,
    bool EmailConfirmed,
    int? MaxClinics,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ClinicSummaryViewModel> Clinics);

