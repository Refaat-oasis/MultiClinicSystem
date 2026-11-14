namespace Core.Application;

public sealed record ClinicBillingInspectResult(
    Guid ClinicId,
    int PriceListCount,
    int ChargesCount,
    decimal TotalNetBilledAmount,
    decimal TotalCollectedAmount,
    decimal TotalRefundedAmount,
    decimal TotalExpensesAmount,
    int ClosingsCount,
    int SuppliersCount,
    IReadOnlyList<ChargeResult> RecentCharges,
    IReadOnlyList<DailyClosingResult> RecentClosings);

