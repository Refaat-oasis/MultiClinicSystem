namespace Core.Application;

/// <summary>
/// Contract implemented by each platform module to declare its identity, prerequisites, permissions, menu items, settings sections, and widgets.
/// </summary>
public interface IModuleManifest
{
    string ModuleKey { get; }
    string TitleAr { get; }
    string TitleEn { get; }
    string DescriptionAr { get; }
    string DescriptionEn { get; }
    IReadOnlyList<string> Prerequisites { get; }
    IReadOnlyList<PermissionDefinitionResult> Permissions { get; }
    IReadOnlyList<ModuleMenuItemResult> MenuItems { get; }
    IReadOnlyList<SettingsSectionResult> SettingsSections { get; }
    IReadOnlyList<DashboardWidgetDefinitionResult> DashboardWidgets { get; }
}
