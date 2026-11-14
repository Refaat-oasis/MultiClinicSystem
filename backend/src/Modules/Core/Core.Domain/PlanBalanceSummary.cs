namespace Core.Domain;

public sealed record PlanBalanceSummary(
    decimal TotalBilledOrContractedAmount,
    decimal NetPaidAmount,
    decimal RemainingBalanceAmount);

