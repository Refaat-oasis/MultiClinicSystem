namespace Platform.Api.ViewModels;

public sealed record PlatformClinicReportRowViewModel(
    Guid ClinicId,
    string ClinicName,
    string Slug,
    string Currency,
    int VisitsCount,
    decimal NetIncome,
    decimal OperatingExpenses,
    decimal SupplierPayments,
    decimal FixedCostsTotal,
    decimal PayrollTotal,
    decimal TotalExpenses,
    decimal NetProfit);

