namespace Platform.Api.ViewModels;

public sealed record OnboardingStateViewModel(
    Guid ClinicId,
    string Name,
    string Slug,
    string OnboardingStatus,
    bool WelcomeSeen,
    IReadOnlyList<string> CompletedSteps,
    string? Street,
    string? City,
    string? Governorate,
    string? MainPhone,
    IReadOnlyList<string> ExtraPhones,
    string? ContactEmail,
    Guid? LogoAssetId,
    IReadOnlyList<ClinicSpecialtyItemViewModel> Specialties);

