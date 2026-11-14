namespace Platform.Api.ViewModels;

public sealed record ClinicDashboardViewModel(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    string OnboardingStatus,
    IReadOnlyList<string> ModulesEnabled,
    bool AwaitingModuleSetup,
    string StatusMessageAr,
    string StatusMessageEn,
    IReadOnlyList<string> OwnerChecklist,
    int SpecialtiesCount);

