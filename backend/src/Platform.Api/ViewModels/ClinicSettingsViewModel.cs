namespace Platform.Api.ViewModels;

public sealed record ClinicSettingsViewModel(
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
    IReadOnlyList<ClinicSpecialtyItemViewModel> Specialties,
    IReadOnlyList<SettingsSectionViewModel> Sections);

