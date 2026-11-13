namespace Core.Application;

public sealed record ClinicPortalSettingResult(
    Guid ClinicId,
    bool ShowPricesByDefault,
    bool ShowDoctorNames,
    bool DefaultVisitVisibility,
    string TermsTextAr,
    string TermsTextEn,
    string WelcomeMessageAr,
    string WelcomeMessageEn,
    DateTimeOffset UpdatedAtUtc);

