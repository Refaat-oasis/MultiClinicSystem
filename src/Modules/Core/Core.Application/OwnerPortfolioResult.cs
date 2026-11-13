namespace Core.Application;

/// <summary>
/// Aggregate portfolio view across clinics owned by a single owner, executed under each clinic's own RLS context.
/// </summary>
public sealed record OwnerPortfolioResult(
    Guid OwnerId,
    int TotalClinics,
    int ActiveClinics,
    int TotalStaffAcrossClinics,
    int TotalDoctorsAcrossClinics,
    IReadOnlyList<OwnerPortfolioClinicItemResult> Clinics);

