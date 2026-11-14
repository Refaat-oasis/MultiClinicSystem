using Core.Application;

namespace Platform.Api.ViewModels;

public static class TreatmentPlanViewModelMapper
{
    public static PlanSessionViewModel ToViewModel(PlanSessionResult s) => new(
        s.Id, s.SequenceNumber, s.Title, s.IntervalDaysFromPrevious, s.WindowMinusDays, s.WindowPlusDays,
        s.TargetDate, s.EarliestAllowedDate, s.LatestAllowedDate, s.ActualOrSlippedDate,
        s.SessionPriceAmount, s.MaterialsCostAmount, s.LinkedAppointmentId, s.LinkedVisitId, s.LinkedChargeId,
        s.Status.ToString(), s.Notes);

    public static PlanInstallmentViewModel ToViewModel(PlanInstallmentResult i) => new(
        i.Id, i.InstallmentNumber, i.IsUpfront, i.DueDate, i.Amount, i.PaidAmount, i.RemainingAmount,
        i.Currency, i.Status.ToString(), i.LinkedPaymentId, i.PaidAtUtc);

    public static TreatmentPlanViewModel ToViewModel(TreatmentPlanResult p) => new(
        p.Id, p.ClinicId, p.PatientId, p.DoctorId, p.Name, p.Mode.ToString(),
        p.FixedTotalAmount, p.IncludedMaterialsCostAmount, p.UpfrontAmount, p.PaidAmount, p.RefundedAmount,
        p.TotalBilledOrContractedAmount, p.RemainingBalanceAmount, p.Currency, p.Status.ToString(),
        p.TotalSessionsCount, p.CompletedSessionsCount, p.RemainingSessionsCount, p.CompletionPercentage,
        p.ShowCostToPatient, p.Notes, p.CreatedAtUtc,
        p.Sessions.Select(ToViewModel).ToList(),
        p.Installments.Select(ToViewModel).ToList());

    public static PlanSuggestedSlotViewModel ToViewModel(PlanSuggestedSlotResult s) => new(
        s.SessionId,
        s.SequenceNumber,
        s.TargetDate,
        s.EarliestAllowedDate,
        s.LatestAllowedDate,
        s.SuggestedCandidateDates,
        s.BookedAppointment != null ? SchedulingViewModelMapper.ToViewModel(s.BookedAppointment) : null);

    public static ClinicTreatmentPlansInspectViewModel ToViewModel(ClinicTreatmentPlansInspectResult r) => new(
        r.ClinicId,
        r.TotalPlansCount,
        r.ActivePlansCount,
        r.CompletedPlansCount,
        r.DelayedSessionsCount,
        r.RecentPlans.Select(ToViewModel).ToList());
}

