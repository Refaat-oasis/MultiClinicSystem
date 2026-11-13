namespace Core.Application;

/// <summary>
/// Definition metadata for a switchable module and its prerequisite module keys.
/// </summary>
public sealed record ModuleDefinitionResult(
    string ModuleKey,
    string TitleAr,
    string TitleEn,
    string DescriptionAr,
    string DescriptionEn,
    IReadOnlyList<string> Prerequisites);
