namespace Core.Application;

/// <summary>
/// Represents a dynamically registered clinic settings section.
/// </summary>
public sealed record SettingsSectionResult(
    string SectionKey,
    string TitleAr,
    string TitleEn,
    string RequiredPermission,
    string? ModuleKey);

