using Core.Domain;

namespace Platform.Api.DTOs;

public sealed class RecordPayrollAdjustmentDto
{
    public Guid EmployeeId { get; init; }
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public PayrollAdjustmentType AdjustmentType { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateOnly OccurredOnDate { get; init; }
    public Guid? OperationId { get; init; }
}

