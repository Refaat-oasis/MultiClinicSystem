using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Monthly clinic payroll run aggregating employee base salaries, bonuses, deductions, and advances.
/// Strictly no doctor commission. ConflictClass: ServerValidated.
/// </summary>
public sealed class PayrollRun : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public PayrollRunStatus Status { get; private set; }
    public PaymentMethod PaidFromMethod { get; private set; }
    public decimal TotalBaseSalary { get; private set; }
    public decimal TotalBonuses { get; private set; }
    public decimal TotalDeductions { get; private set; }
    public decimal TotalAdvances { get; private set; }
    public decimal TotalNetPay { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string CreatedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? FinalizedAtUtc { get; private set; }
    public string? FinalizedByUserId { get; private set; }
    public string? Notes { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PayrollRun()
    {
    }

    public PayrollRun(
        Guid id,
        Guid clinicId,
        int periodYear,
        int periodMonth,
        PaymentMethod paidFromMethod,
        string currency,
        string createdByUserId,
        DateTimeOffset createdAtUtc,
        string? notes,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (periodMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(periodMonth), "PeriodMonth must be between 1 and 12.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        Status = PayrollRunStatus.Draft;
        PaidFromMethod = paidFromMethod;
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        CreatedByUserId = string.IsNullOrWhiteSpace(createdByUserId) ? "system" : createdByUserId.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        OperationId = operationId;
    }

    public void FinalizeRun(
        decimal totalBaseSalary,
        decimal totalBonuses,
        decimal totalDeductions,
        decimal totalAdvances,
        decimal totalNetPay,
        string finalizedByUserId,
        DateTimeOffset finalizedAtUtc)
    {
        TotalBaseSalary = Math.Round(totalBaseSalary, 2, MidpointRounding.AwayFromZero);
        TotalBonuses = Math.Round(totalBonuses, 2, MidpointRounding.AwayFromZero);
        TotalDeductions = Math.Round(totalDeductions, 2, MidpointRounding.AwayFromZero);
        TotalAdvances = Math.Round(totalAdvances, 2, MidpointRounding.AwayFromZero);
        TotalNetPay = Math.Round(totalNetPay, 2, MidpointRounding.AwayFromZero);
        Status = PayrollRunStatus.Finalized;
        FinalizedByUserId = string.IsNullOrWhiteSpace(finalizedByUserId) ? "system" : finalizedByUserId.Trim();
        FinalizedAtUtc = finalizedAtUtc.ToUniversalTime();
    }
}

