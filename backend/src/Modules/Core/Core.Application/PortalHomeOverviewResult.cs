namespace Core.Application;

public sealed record PortalHomeOverviewResult(
    Guid ClinicId,
    string ClinicName,
    string ClinicSlug,
    Guid? ClinicLogoAssetId,
    string WelcomeMessageAr,
    string WelcomeMessageEn,
    IReadOnlyList<string> AvailableSections,
    PortalPatientSummaryResult Summary,
    PortalMedicalBackgroundResult? MedicalBackground,
    IReadOnlyList<PortalUpcomingAppointmentItemResult>? UpcomingAppointments,
    IReadOnlyList<PortalVisitSummaryResult>? Visits,
    IReadOnlyList<PortalTreatmentPlanItemResult>? TreatmentPlans);

