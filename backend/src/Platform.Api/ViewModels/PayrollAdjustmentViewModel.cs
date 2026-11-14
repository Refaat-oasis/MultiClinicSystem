using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record PayrollAdjustmentViewModel(
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

