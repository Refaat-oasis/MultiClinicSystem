namespace Core.Application;

/// <summary>
/// Registry of modular clinic settings sections filtered by enabled modules.
/// </summary>
public interface ISettingsSectionRegistry
{
    IReadOnlyList<SettingsSectionResult> GetAvailableSections(IReadOnlyCollection<string> enabledModules);
}

