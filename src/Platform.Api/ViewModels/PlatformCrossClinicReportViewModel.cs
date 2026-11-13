namespace Platform.Api.ViewModels;

public sealed record PlatformCrossClinicReportViewModel(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalClinics,
    int TotalVisits,
    decimal TotalNetIncome,
    decimal TotalExpenses,
    decimal TotalNetProfit,
    IReadOnlyList<PlatformClinicReportRowViewModel> Clinics);

