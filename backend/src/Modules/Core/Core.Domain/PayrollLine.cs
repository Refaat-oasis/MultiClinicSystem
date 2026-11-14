using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Individual employee pay breakdown line belonging to a PayrollRun.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class PayrollLine : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string EmployeeNameSnapshot { get; private set; } = string.Empty;
    public Guid? DoctorId { get; private set; }
    public decimal BaseSalaryAmount { get; private set; }
    public decimal BonusTotalAmount { get; private set; }
    public decimal DeductionTotalAmount { get; private set; }
    public decimal AdvanceTotalAmount { get; private set; }
    public decimal NetPayAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PayrollLine()
    {
    }

    public PayrollLine(
        Guid id,
        Guid clinicId,
        Guid payrollRunId,
        Guid employeeId,
        string employeeNameSnapshot,
        Guid? doctorId,
        decimal baseSalaryAmount,
        decimal bonusTotalAmount,
        decimal deductionTotalAmount,
        decimal advanceTotalAmount,
        decimal netPayAmount,
        string currency,
        Guid? operationId)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PayrollRunId = payrollRunId;
        EmployeeId = employeeId;
        EmployeeNameSnapshot = employeeNameSnapshot.Trim();
        DoctorId = doctorId;
        BaseSalaryAmount = Math.Round(baseSalaryAmount, 2, MidpointRounding.AwayFromZero);
        BonusTotalAmount = Math.Round(bonusTotalAmount, 2, MidpointRounding.AwayFromZero);
        DeductionTotalAmount = Math.Round(deductionTotalAmount, 2, MidpointRounding.AwayFromZero);
        AdvanceTotalAmount = Math.Round(advanceTotalAmount, 2, MidpointRounding.AwayFromZero);
        NetPayAmount = Math.Round(netPayAmount, 2, MidpointRounding.AwayFromZero);
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        OperationId = operationId;
    }
}

