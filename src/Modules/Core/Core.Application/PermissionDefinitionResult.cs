namespace Core.Application;

/// <summary>
/// Definition entry in the code-backed permission catalog.
/// </summary>
public sealed record PermissionDefinitionResult(
    string Key,
    string ModuleKey,
    string Group,
    string TitleAr,
    string TitleEn,
    bool IsReserved);
