using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Default settings section registry exposing General clinic settings in Phase 3
/// and extensible module-gated sections in subsequent phases.
/// </summary>
public sealed class SettingsSectionRegistry : ISettingsSectionRegistry
{
    private static readonly IReadOnlyList<SettingsSectionResult> CoreSections =
    [
        new(
            SectionKey: "general",
            TitleAr: "البيانات العامة للعيادة",
            TitleEn: "General Clinic Information",
            RequiredPermission: "clinic.settings.edit",
            ModuleKey: null)
    ];

    public IReadOnlyList<SettingsSectionResult> GetAvailableSections(IReadOnlyCollection<string> enabledModules)
    {
        return CoreSections
            .Where(s => s.ModuleKey == null || enabledModules.Contains(s.ModuleKey, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}

