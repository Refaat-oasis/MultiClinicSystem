namespace Platform.Api.ViewModels;

public sealed record OwnerPortfolioViewModel(
    Guid OwnerId,
    int TotalClinics,
    int ActiveClinics,
    int TotalStaffAcrossClinics,
    int TotalDoctorsAcrossClinics,
    IReadOnlyList<OwnerPortfolioClinicItemViewModel> Clinics);
