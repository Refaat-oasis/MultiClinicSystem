namespace Core.Application;

public sealed record ClinicTreatmentPlansInspectResult(
    Guid ClinicId,
    int TotalPlansCount,
    int ActivePlansCount,
    int CompletedPlansCount,
    int DelayedSessionsCount,
    IReadOnlyList<TreatmentPlanResult> RecentPlans);

