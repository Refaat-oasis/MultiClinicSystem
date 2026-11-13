using Core.Domain;

namespace Core.Application;

public sealed record PayrollAdjustmentResult(
    Guid Id,
    Guid ClinicId,
    Guid EmployeeId,
    string EmployeeName,
    Guid? PayrollRunId,
    int PeriodYear,
    int PeriodMonth,
    PayrollAdjustmentType AdjustmentType,
    decimal Amount,
    string Currency,
    string Reason,
    DateOnly OccurredOnDate);

