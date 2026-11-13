namespace Platform.Api.ViewModels;

public sealed record PermissionDefinitionViewModel(
    string Key,
    string ModuleKey,
    string Group,
    string TitleAr,
    string TitleEn,
    bool IsReserved);
