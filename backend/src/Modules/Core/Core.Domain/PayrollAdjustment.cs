using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Employee payroll adjustment (bonus, deduction, or advance) for a specific salary month.
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class PayrollAdjustment : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? PayrollRunId { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public PayrollAdjustmentType AdjustmentType { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string Reason { get; private set; } = string.Empty;
    public DateOnly OccurredOnDate { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PayrollAdjustment()
    {
    }

    public PayrollAdjustment(
        Guid id,
        Guid clinicId,
        Guid employeeId,
        int periodYear,
        int periodMonth,
        PayrollAdjustmentType adjustmentType,
        Money amount,
        string reason,
        DateOnly occurredOnDate,
        string recordedByUserId,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("EmployeeId is required.", nameof(employeeId));
        }

        if (periodMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(periodMonth), "PeriodMonth must be between 1 and 12.");
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Adjustment amount must be positive.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        EmployeeId = employeeId;
        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        AdjustmentType = adjustmentType;
        Amount = amount.Amount;
        Currency = amount.Currency;
        Reason = string.IsNullOrWhiteSpace(reason) ? adjustmentType.ToString() : reason.Trim();
        OccurredOnDate = occurredOnDate;
        RecordedByUserId = string.IsNullOrWhiteSpace(recordedByUserId) ? "system" : recordedByUserId.Trim();
        OperationId = operationId;
    }

    public void AttachToPayrollRun(Guid payrollRunId) => PayrollRunId = payrollRunId;
}

