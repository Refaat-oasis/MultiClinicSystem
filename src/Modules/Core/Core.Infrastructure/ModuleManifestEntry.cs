using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Concrete manifest descriptor declaring a module's prerequisites, permissions, menu items, settings sections, and widgets.
/// </summary>
public sealed record ModuleManifestEntry(
    string ModuleKey,
    string TitleAr,
    string TitleEn,
    string DescriptionAr,
    string DescriptionEn,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<PermissionDefinitionResult> Permissions,
    IReadOnlyList<ModuleMenuItemResult> MenuItems,
    IReadOnlyList<SettingsSectionResult> SettingsSections,
    IReadOnlyList<DashboardWidgetDefinitionResult> DashboardWidgets) : IModuleManifest;

