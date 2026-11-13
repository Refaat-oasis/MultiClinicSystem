namespace Core.Application;

/// <summary>
/// Clinic dashboard summary showing enabled modules or waiting-for-features checklist.
/// </summary>
public sealed record ClinicDashboardResult(
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

