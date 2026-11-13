using Core.Domain;

namespace Core.Application;

public sealed record RecordPayrollAdjustmentInput(
    Guid EmployeeId,
    int PeriodYear,
    int PeriodMonth,
    PayrollAdjustmentType AdjustmentType,
    decimal Amount,
    string Reason,
    DateOnly OccurredOnDate,
    Guid? OperationId);

