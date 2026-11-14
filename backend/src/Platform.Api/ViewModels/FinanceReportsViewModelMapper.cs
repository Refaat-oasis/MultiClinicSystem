using Core.Application;

namespace Platform.Api.ViewModels;

public static class FinanceReportsViewModelMapper
{
    public static FixedCostViewModel ToViewModel(FixedCostResult r) => new(
        r.Id, r.ClinicId, r.Category, r.TitleAr, r.TitleEn, r.Amount, r.Currency,
        r.Recurrence, r.PaidFromMethod, r.EffectiveFromDate, r.EffectiveToDate, r.IsActive, r.Notes);

    public static FixedCostOccurrenceViewModel ToViewModel(FixedCostOccurrenceResult r) => new(
        r.FixedCostId, r.Category, r.TitleAr, r.TitleEn, r.OccurrenceDate, r.Amount, r.Currency, r.PaidFromMethod);

    public static EmployeeViewModel ToViewModel(EmployeeResult r) => new(
        r.Id, r.ClinicId, r.FullName, r.RoleTitle, r.DoctorId, r.Phone, r.BaseSalaryAmount,
        r.Currency, r.PaidFromMethod, r.HiredOnDate, r.IsActive);

    public static PayrollAdjustmentViewModel ToViewModel(PayrollAdjustmentResult r) => new(
        r.Id, r.ClinicId, r.EmployeeId, r.EmployeeName, r.PayrollRunId,
        r.PeriodYear, r.PeriodMonth, r.AdjustmentType, r.Amount, r.Currency, r.Reason, r.OccurredOnDate);

    public static PayrollRunViewModel ToViewModel(PayrollRunResult r) => new(
        r.Id,
        r.ClinicId,
        r.PeriodYear,
        r.PeriodMonth,
        r.Status,
        r.PaidFromMethod,
        r.TotalBaseSalary,
        r.TotalBonuses,
        r.TotalDeductions,
        r.TotalAdvances,
        r.TotalNetPay,
        r.Currency,
        r.CreatedAtUtc,
        r.FinalizedAtUtc,
        r.Notes,
        r.Lines.Select(l => new PayrollLineViewModel(
            l.Id, l.PayrollRunId, l.EmployeeId, l.EmployeeNameSnapshot, l.DoctorId,
            l.BaseSalaryAmount, l.BonusTotalAmount, l.DeductionTotalAmount, l.AdvanceTotalAmount,
            l.NetPayAmount, l.Currency)).ToList());

    public static IncomeVsExpensesReportViewModel ToViewModel(IncomeVsExpensesReportResult r) => new(
        r.ClinicId, r.FromDate, r.ToDate, r.Currency, r.GrossPayments, r.Refunds, r.NetIncome,
        r.OperatingExpenses, r.SupplierPayments, r.SupplierAccruals, r.FixedCostsTotal, r.PayrollTotal,
        r.TotalExpenses, r.NetProfit);

    public static DoctorFinancialReportRowViewModel ToViewModel(DoctorFinancialReportRowResult r) => new(
        r.DoctorId, r.DoctorName, r.SpecialtyCode, r.SessionsCount, r.CompletedSessionsCount,
        r.BilledTotal, r.CollectedTotal, r.RefundedTotal, r.ToolsCost, r.LabCost, r.PayrollCost, r.NetContribution);

    public static SessionCategoryReportRowViewModel ToViewModel(SessionCategoryReportRowResult r) => new(
        r.VisitDate, r.CategoryId, r.CategoryNameAr, r.CategoryNameEn, r.SessionsCount, r.CompletedSessionsCount, r.BilledTotal);

    public static MethodTotalReportRowViewModel ToViewModel(MethodTotalReportRowResult r) => new(
        r.Method, r.PaymentsIn, r.RefundsOut, r.ExpensesOut, r.SupplierPaymentsOut, r.NetFlow);

    public static ReportExportLogViewModel ToViewModel(ReportExportLogResult r) => new(
        r.Id, r.ReportKind, r.Format, r.FromDate, r.ToDate, r.RowCount, r.FileName, r.ExportedAtUtc);

    public static ScheduledReportConfigViewModel ToViewModel(ScheduledReportConfigResult r) => new(
        r.Id, r.ReportKind, r.Format, r.Frequency, r.IsEnabled, r.NextRunAtUtc, r.LastRunAtUtc);

    public static PlatformCrossClinicReportViewModel ToViewModel(PlatformCrossClinicReportResult r) => new(
        r.FromDate,
        r.ToDate,
        r.TotalClinics,
        r.TotalVisits,
        r.TotalNetIncome,
        r.TotalExpenses,
        r.TotalNetProfit,
        r.Clinics.Select(c => new PlatformClinicReportRowViewModel(
            c.ClinicId, c.ClinicName, c.Slug, c.Currency, c.VisitsCount, c.NetIncome,
            c.OperatingExpenses, c.SupplierPayments, c.FixedCostsTotal, c.PayrollTotal, c.TotalExpenses, c.NetProfit)).ToList());

    public static ImportValidationViewModel ToViewModel(ImportValidationResult r) => new(
        r.TargetKind,
        r.FileName,
        r.TotalRows,
        r.ValidRows,
        r.RejectedRows,
        r.CanCommit,
        r.Committed,
        r.Errors.Select(e => new ImportRowErrorViewModel(e.RowNumber, e.Column, e.ErrorCode, e.Message)).ToList(),
        r.PreviewSummaries);

    public static ImportBatchLogViewModel ToViewModel(ImportBatchLogResult r) => new(
        r.Id, r.TargetKind, r.FileName, r.TotalRows, r.AcceptedRows, r.RejectedRows, r.IsCommitted, r.ExecutedAtUtc);

    public static ClinicFinanceReportsInspectViewModel ToViewModel(ClinicFinanceReportsInspectResult r) => new(
        r.ClinicId,
        ToViewModel(r.IncomeVsExpenses),
        r.FixedCosts.Select(ToViewModel).ToList(),
        r.Employees.Select(ToViewModel).ToList(),
        r.PayrollRuns.Select(ToViewModel).ToList(),
        r.RecentExports.Select(ToViewModel).ToList(),
        r.RecentImports.Select(ToViewModel).ToList());
}

