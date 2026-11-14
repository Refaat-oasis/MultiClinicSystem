using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Settings section registry exposing core General settings plus module-contributed sections for enabled modules.
/// </summary>
public sealed class SettingsSectionRegistry : ISettingsSectionRegistry
{
    private readonly IModuleCatalogService _moduleCatalog;

    public SettingsSectionRegistry(IModuleCatalogService moduleCatalog)
    {
        _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
    }

    public IReadOnlyList<SettingsSectionResult> GetAvailableSections(IReadOnlyCollection<string> enabledModules)
    {
        var enabledSet = new HashSet<string>(enabledModules ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase) { "core" };

        return _moduleCatalog
            .GetAllManifests()
            .Where(m => enabledSet.Contains(m.ModuleKey))
            .SelectMany(m => m.SettingsSections)
            .ToList();
    }
}
