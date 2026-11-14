namespace Core.Application;

public sealed record UpdateClinicPortalSettingInput(
    bool ShowPricesByDefault,
    bool ShowDoctorNames,
    bool DefaultVisitVisibility,
    string? TermsTextAr,
    string? TermsTextEn,
    string? WelcomeMessageAr,
    string? WelcomeMessageEn);

