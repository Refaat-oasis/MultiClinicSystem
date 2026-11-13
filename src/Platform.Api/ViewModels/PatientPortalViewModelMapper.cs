using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Hand-written mapper between Patient Portal application DTOs and API ViewModels.
/// </summary>
public static class PatientPortalViewModelMapper
{
    public static PublicClinicLandingViewModel ToViewModel(PublicClinicLandingResult r) => new(
        r.ClinicId,
        r.Name,
        r.CurrentSlug,
        r.RequestedSlug,
        r.IsRedirect,
        r.RedirectUrl,
        r.LogoAssetId,
        r.Address,
        r.MainPhone,
        r.WelcomeMessageAr,
        r.WelcomeMessageEn,
        r.TermsTextAr,
        r.TermsTextEn,
        r.AvailableSections);

    public static PatientPortalSessionViewModel ToViewModel(PatientPortalSessionResult s) => new(
        s.AccessToken,
        s.ExpiresAtUtc,
        s.PatientAccountId,
        s.ClinicId,
        s.PatientId,
        s.FullName,
        s.ClinicSlug,
        s.Email);

    public static PortalPatientSummaryViewModel ToViewModel(PortalPatientSummaryResult p) => new(
        p.PatientId,
        p.FileNumber,
        p.FullName,
        p.AgeYears,
        p.DateOfBirth,
        p.Gender,
        p.BloodType,
        p.Email,
        p.GuardianName);

    public static PortalVisibleNoteItemViewModel ToViewModel(PortalVisibleNoteItemResult n) => new(
        n.NoteId,
        n.NoteText,
        n.CreatedAtUtc);

    public static PortalSharedAttachmentItemViewModel ToViewModel(PortalSharedAttachmentItemResult a) => new(
        a.AttachmentId,
        a.Category,
        a.FileName,
        a.ContentType,
        a.SizeBytes,
        a.Notes,
        a.CreatedAtUtc);

    public static PortalMedicalBackgroundViewModel ToViewModel(PortalMedicalBackgroundResult m) => new(
        m.Allergies.Select(PatientViewModelMapper.MapAllergy).ToList(),
        m.ChronicConditions,
        m.CurrentMedications,
        m.PastSurgeries,
        m.IsMedicalHistoryReviewed,
        m.MedicalHistorySummary,
        m.VisibleNotes.Select(ToViewModel).ToList(),
        m.SharedAttachments.Select(ToViewModel).ToList());

    public static PortalUpcomingAppointmentItemViewModel ToViewModel(PortalUpcomingAppointmentItemResult a) => new(
        a.AppointmentId,
        a.StartAtUtc,
        a.EndAtUtc,
        a.DoctorName,
        a.SpecialtyCode,
        a.CategoryNameAr,
        a.CategoryNameEn,
        a.Status);

    public static PortalPlanSessionItemViewModel ToViewModel(PortalPlanSessionItemResult s) => new(
        s.SessionId,
        s.SequenceNumber,
        s.Title,
        s.TargetDate,
        s.EarliestAllowedDate,
        s.LatestAllowedDate,
        s.Status.ToString(),
        s.LinkedAppointmentId,
        s.SessionPriceAmount);

    public static PortalTreatmentPlanItemViewModel ToViewModel(PortalTreatmentPlanItemResult p) => new(
        p.PlanId,
        p.Name,
        p.Mode.ToString(),
        p.Status.ToString(),
        p.TotalSessionsCount,
        p.CompletedSessionsCount,
        p.CompletionPercentage,
        p.ShowCostToPatient,
        p.TotalCostToPatient,
        p.PaidAmountToPatient,
        p.RemainingBalanceToPatient,
        p.Currency,
        p.Sessions.Select(ToViewModel).ToList());

    public static PortalVisitDetailViewModel ToViewModel(PortalVisitDetailResult v) => new(
        ClinicalViewModelMapper.ToViewModel(v.Visit),
        v.SharedAttachments.Select(ToViewModel).ToList());

    public static PortalHomeOverviewViewModel ToViewModel(PortalHomeOverviewResult o) => new(
        o.ClinicId,
        o.ClinicName,
        o.ClinicSlug,
        o.ClinicLogoAssetId,
        o.WelcomeMessageAr,
        o.WelcomeMessageEn,
        o.AvailableSections,
        ToViewModel(o.Summary),
        o.MedicalBackground != null ? ToViewModel(o.MedicalBackground) : null,
        o.UpcomingAppointments?.Select(ToViewModel).ToList(),
        o.Visits?.Select(ClinicalViewModelMapper.ToViewModel).ToList(),
        o.TreatmentPlans?.Select(ToViewModel).ToList());

    public static PortalSignedUrlViewModel ToViewModel(PortalSignedUrlResult u) => new(
        u.Url,
        u.ExpiresAtUtc);

    public static PatientPortalAdminStatusViewModel ToViewModel(PatientPortalAdminStatusResult s) => new(
        s.PatientId,
        s.HasAccount,
        s.Status?.ToString(),
        s.Email,
        s.IsEmailConfirmed,
        s.IsGuardianAccount,
        s.GuardianName,
        s.ConsentAcceptedAtUtc,
        s.ConsentTermsVersion,
        s.LastLoginAtUtc,
        s.GeneratedPrintedPin,
        s.PrintedPinExpiresAtUtc);

    public static ClinicPortalSettingViewModel ToViewModel(ClinicPortalSettingResult s) => new(
        s.ClinicId,
        s.ShowPricesByDefault,
        s.ShowDoctorNames,
        s.DefaultVisitVisibility,
        s.TermsTextAr,
        s.TermsTextEn,
        s.WelcomeMessageAr,
        s.WelcomeMessageEn,
        s.UpdatedAtUtc);

    public static PortalAccessLogItemViewModel ToViewModel(PortalAccessLogItemResult l) => new(
        l.Id,
        l.TimestampUtc,
        l.ActorId,
        l.ActorType,
        l.Reason,
        l.Details);

    public static ClinicPortalInspectViewModel ToViewModel(ClinicPortalInspectResult r) => new(
        r.ClinicId,
        r.TotalAccountsCount,
        r.ActiveAccountsCount,
        r.InvitedAccountsCount,
        r.DisabledAccountsCount,
        r.PatientViewsLast30DaysCount,
        ToViewModel(r.Settings));
}

