namespace Core.Application;

/// <summary>
/// Catalog entry returned when searching ICD-10 diagnosis codes.
/// </summary>
public sealed record Icd10CatalogEntryResult(
    string Code,
    string TitleAr,
    string TitleEn,
    string SpecialtyCategory);

