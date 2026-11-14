namespace Core.Application;

/// <summary>
/// Read model for a specialty clinical form template.
/// </summary>
public sealed record ClinicalFormResult(
    Guid Id,
    string SpecialtyCode,
    string TitleAr,
    string TitleEn,
    string JsonSchema,
    int Version,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

