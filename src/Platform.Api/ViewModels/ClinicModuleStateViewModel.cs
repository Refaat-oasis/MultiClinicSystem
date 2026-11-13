namespace Platform.Api.ViewModels;

public sealed record ClinicModuleStateViewModel(
    string ModuleKey,
    string TitleAr,
    string TitleEn,
    string DescriptionAr,
    string DescriptionEn,
    bool Enabled,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> MissingPrerequisites,
    IReadOnlyList<string> EnabledDependents,
    DateTimeOffset? ChangedAtUtc,
    string? Note);
