namespace Platform.Api.ViewModels;

public sealed record SettingsSectionViewModel(
    string SectionKey,
    string TitleAr,
    string TitleEn,
    string RequiredPermission,
    string? ModuleKey);

