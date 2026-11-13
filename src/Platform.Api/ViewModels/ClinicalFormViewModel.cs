namespace Platform.Api.ViewModels;

public sealed record ClinicalFormViewModel(
    Guid Id,
    string SpecialtyCode,
    string TitleAr,
    string TitleEn,
    string JsonSchema,
    int Version,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

