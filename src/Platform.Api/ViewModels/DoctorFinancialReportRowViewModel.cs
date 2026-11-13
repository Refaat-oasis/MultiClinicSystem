namespace Platform.Api.ViewModels;

public sealed record DoctorFinancialReportRowViewModel(
    Guid DoctorId,
    string DoctorName,
    string SpecialtyCode,
    int SessionsCount,
    int CompletedSessionsCount,
    decimal BilledTotal,
    decimal CollectedTotal,
    decimal RefundedTotal,
    decimal ToolsCost,
    decimal LabCost,
    decimal PayrollCost,
    decimal NetContribution);

