namespace Platform.Api.ViewModels;

public sealed record DashboardWidgetDefinitionViewModel(
    string WidgetKey,
    string ModuleKey,
    string RequiredPermission,
    string AllowedScope,
    string TitleAr,
    string TitleEn);
