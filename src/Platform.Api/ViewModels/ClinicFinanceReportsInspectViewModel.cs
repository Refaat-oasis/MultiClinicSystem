namespace Platform.Api.ViewModels;

public sealed record ClinicFinanceReportsInspectViewModel(
    Guid ClinicId,
    IncomeVsExpensesReportViewModel IncomeVsExpenses,
    IReadOnlyList<FixedCostViewModel> FixedCosts,
    IReadOnlyList<EmployeeViewModel> Employees,
    IReadOnlyList<PayrollRunViewModel> PayrollRuns,
    IReadOnlyList<ReportExportLogViewModel> RecentExports,
    IReadOnlyList<ImportBatchLogViewModel> RecentImports);

