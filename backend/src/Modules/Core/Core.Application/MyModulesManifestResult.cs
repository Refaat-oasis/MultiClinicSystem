namespace Core.Application;

/// <summary>
/// Combined dynamic manifest returned to a clinic user containing module states, visible menu items, settings sections, and effective permissions.
/// </summary>
public sealed record MyModulesManifestResult(
    Guid ClinicId,
    IReadOnlyList<ClinicModuleStateResult> Modules,
    IReadOnlyList<ModuleMenuItemResult> MenuItems,
    IReadOnlyList<SettingsSectionResult> SettingsSections,
    IReadOnlyList<string> EffectivePermissions);

