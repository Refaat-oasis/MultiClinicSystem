namespace Core.Application;

/// <summary>
/// Full general clinic settings response along with registered settings sections.
/// </summary>
public sealed record ClinicSettingsResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string Street,
    string City,
    string Governorate,
    string MainPhone,
    IReadOnlyList<string> ExtraPhones,
    string? ContactEmail,
    string TimeZone,
    string Currency,
    string RoundingRule,
    bool HasFinancialEntries,
    Guid? LogoAssetId,
    IReadOnlyList<ClinicSpecialtyItemResult> Specialties,
    IReadOnlyList<SettingsSectionResult> Sections);

