namespace Core.Application;

public sealed record ClinicFinanceReportsInspectResult(
    Guid ClinicId,
    IncomeVsExpensesReportResult IncomeVsExpenses,
    IReadOnlyList<FixedCostResult> FixedCosts,
    IReadOnlyList<EmployeeResult> Employees,
    IReadOnlyList<PayrollRunResult> PayrollRuns,
    IReadOnlyList<ReportExportLogResult> RecentExports,
    IReadOnlyList<ImportBatchLogResult> RecentImports);

