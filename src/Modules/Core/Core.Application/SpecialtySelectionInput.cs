namespace Core.Application;

/// <summary>
/// Specialty selection item submitted during onboarding step 3.
/// </summary>
public sealed record SpecialtySelectionInput(
    string CatalogCode,
    string? CustomNameAr,
    string? CustomNameEn);

