namespace Platform.Api.ViewModels;

public sealed record ClinicPortalSettingViewModel(
    Guid ClinicId,
    bool ShowPricesByDefault,
    bool ShowDoctorNames,
    bool DefaultVisitVisibility,
    string TermsTextAr,
    string TermsTextEn,
    string WelcomeMessageAr,
    string WelcomeMessageEn,
    DateTimeOffset UpdatedAtUtc);

