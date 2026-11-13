namespace Platform.Api.ViewModels;

public sealed record PortalTreatmentPlanItemViewModel(
    Guid PlanId,
    string Name,
    string Mode,
    string Status,
    int TotalSessionsCount,
    int CompletedSessionsCount,
    decimal CompletionPercentage,
    bool ShowCostToPatient,
    decimal? TotalCostToPatient,
    decimal? PaidAmountToPatient,
    decimal? RemainingBalanceToPatient,
    string? Currency,
    IReadOnlyList<PortalPlanSessionItemViewModel> Sessions);

