namespace Core.Application;

public sealed record IncomeVsExpensesReportResult(
    Guid ClinicId,
    DateOnly FromDate,
    DateOnly ToDate,
    string Currency,
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

