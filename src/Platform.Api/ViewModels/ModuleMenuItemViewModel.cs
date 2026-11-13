namespace Platform.Api.ViewModels;

public sealed record ModuleMenuItemViewModel(
    string ItemKey,
    string ModuleKey,
    string TitleAr,
    string TitleEn,
    string Route,
    string RequiredPermission,
    int Order);
