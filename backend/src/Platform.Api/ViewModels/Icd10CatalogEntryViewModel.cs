namespace Platform.Api.ViewModels;

public sealed record Icd10CatalogEntryViewModel(
    string Icd10Code,
    string TitleAr,
    string TitleEn,
    string SpecialtyCategory);
