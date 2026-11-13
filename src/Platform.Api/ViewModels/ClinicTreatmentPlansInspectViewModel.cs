namespace Platform.Api.ViewModels;

public sealed record ClinicTreatmentPlansInspectViewModel(
    Guid ClinicId,
    int TotalPlansCount,
    int ActivePlansCount,
    int CompletedPlansCount,
    int DelayedSessionsCount,
    IReadOnlyList<TreatmentPlanViewModel> RecentPlans);

