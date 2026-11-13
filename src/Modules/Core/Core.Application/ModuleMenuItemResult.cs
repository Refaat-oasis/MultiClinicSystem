namespace Core.Application;

/// <summary>
/// Navigation menu item contributed by a module manifest.
/// </summary>
public sealed record ModuleMenuItemResult(
    string ItemKey,
    string ModuleKey,
    string TitleAr,
    string TitleEn,
    string Route,
    string RequiredPermission,
    int Order);
