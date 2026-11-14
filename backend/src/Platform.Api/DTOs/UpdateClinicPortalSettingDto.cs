namespace Platform.Api.DTOs;

public sealed record UpdateClinicPortalSettingDto(
    bool ShowPricesByDefault,
    bool ShowDoctorNames,
    bool DefaultVisitVisibility,
    string? TermsTextAr,
    string? TermsTextEn,
    string? WelcomeMessageAr,
    string? WelcomeMessageEn);

