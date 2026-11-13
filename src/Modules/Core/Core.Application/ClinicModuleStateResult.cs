namespace Core.Application;

/// <summary>
/// Per-clinic activation state for a switchable module, including unmet prerequisites and active dependents.
/// </summary>
public sealed record ClinicModuleStateResult(
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

