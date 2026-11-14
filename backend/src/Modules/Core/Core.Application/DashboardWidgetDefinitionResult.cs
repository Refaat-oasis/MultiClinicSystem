namespace Core.Application;

/// <summary>
/// Metadata declaration for a dashboard widget registered by a module manifest.
/// </summary>
public sealed record DashboardWidgetDefinitionResult(
    string WidgetKey,
    string ModuleKey,
    string RequiredPermission,
    string AllowedScope,
    string TitleAr,
    string TitleEn);

