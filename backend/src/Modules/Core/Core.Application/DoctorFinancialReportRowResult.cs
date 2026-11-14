namespace Core.Application;

public sealed record DoctorFinancialReportRowResult(
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

