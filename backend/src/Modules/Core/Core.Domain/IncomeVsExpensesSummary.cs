namespace Core.Domain;

/// <summary>
/// Pure period reconciliation comparing collected ledger income against clinic expense buckets.
/// </summary>
public sealed record IncomeVsExpensesSummary(
    decimal GrossPayments,
    decimal Refunds,
    decimal NetIncome,
    decimal OperatingExpenses,
    decimal SupplierPayments,
    decimal SupplierAccruals,
    decimal FixedCostsTotal,
    decimal PayrollTotal,
    decimal TotalExpenses,
    decimal NetProfit);

