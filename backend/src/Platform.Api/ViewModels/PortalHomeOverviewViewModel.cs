namespace Platform.Api.ViewModels;

public sealed record PortalHomeOverviewViewModel(
    Guid ClinicId,
    string ClinicName,
    string ClinicSlug,
    Guid? ClinicLogoAssetId,
    string WelcomeMessageAr,
    string WelcomeMessageEn,
    IReadOnlyList<string> AvailableSections,
    PortalPatientSummaryViewModel Summary,
    PortalMedicalBackgroundViewModel? MedicalBackground,
    IReadOnlyList<PortalUpcomingAppointmentItemViewModel>? UpcomingAppointments,
    IReadOnlyList<PortalVisitSummaryViewModel>? Visits,
    IReadOnlyList<PortalTreatmentPlanItemViewModel>? TreatmentPlans);

