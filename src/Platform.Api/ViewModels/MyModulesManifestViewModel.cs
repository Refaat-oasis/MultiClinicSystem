namespace Platform.Api.ViewModels;

public sealed record MyModulesManifestViewModel(
    Guid ClinicId,
    IReadOnlyList<ClinicModuleStateViewModel> Modules,
    IReadOnlyList<ModuleMenuItemViewModel> MenuItems,
    IReadOnlyList<SettingsSectionViewModel> SettingsSections,
    IReadOnlyList<string> EffectivePermissions);
