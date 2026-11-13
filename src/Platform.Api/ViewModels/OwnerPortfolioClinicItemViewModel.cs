namespace Platform.Api.ViewModels;

public sealed record OwnerPortfolioClinicItemViewModel(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    string OnboardingStatus,
    int StaffCount,
    int RoomsCount,
    int DoctorsCount,
    int EnabledModulesCount);
