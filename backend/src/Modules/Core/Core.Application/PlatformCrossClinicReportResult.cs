namespace Core.Application;

public sealed record PlatformCrossClinicReportResult(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalClinics,
    int TotalVisits,
    decimal TotalNetIncome,
    decimal TotalExpenses,
    decimal TotalNetProfit,
    IReadOnlyList<PlatformClinicReportRowResult> Clinics);

