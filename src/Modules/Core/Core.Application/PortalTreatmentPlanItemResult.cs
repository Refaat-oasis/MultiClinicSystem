using Core.Domain;

namespace Core.Application;

/// <summary>
/// Patient-portal-safe treatment plan DTO: never exposes internal material costs,
/// and omits patient-facing cost figures when ShowCostToPatient is false.
/// </summary>
public sealed record PortalTreatmentPlanItemResult(
    Guid PlanId,
    string Name,
    TreatmentPlanMode Mode,
    TreatmentPlanStatus Status,
    int TotalSessionsCount,
    int CompletedSessionsCount,
    decimal CompletionPercentage,
    bool ShowCostToPatient,
    decimal? TotalCostToPatient,
    decimal? PaidAmountToPatient,
    decimal? RemainingBalanceToPatient,
    string? Currency,
    IReadOnlyList<PortalPlanSessionItemResult> Sessions);

