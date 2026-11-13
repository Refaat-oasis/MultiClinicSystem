using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Catalog and dependency validation service for platform modules, permissions, menus, and dashboard widget definitions.
/// </summary>
public interface IModuleCatalogService
{
    IReadOnlyList<ModuleDefinitionResult> GetSwitchableModules();
    IReadOnlyList<IModuleManifest> GetAllManifests();
    IReadOnlyList<PermissionDefinitionResult> GetPermissionCatalog();
    IReadOnlyList<DashboardWidgetDefinitionResult> GetAllWidgetDefinitions();
    IReadOnlyList<ModuleMenuItemResult> GetMenuItemsForModules(IEnumerable<string> enabledModules, IReadOnlySet<string>? effectivePermissions = null);
    IReadOnlySet<string> GetActivePermissionsForModules(IEnumerable<string> enabledModules);
    IReadOnlyList<string> GetDirectDependents(string moduleKey);
    bool IsValidSwitchableModule(string moduleKey);
    Result ValidateModuleChanges(
        IReadOnlySet<string> currentlyEnabledModules,
        IReadOnlyList<ModuleToggleInput> requestedChanges);
}
