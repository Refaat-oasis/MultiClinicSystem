using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Upfront deposit or scheduled installment item belonging to a treatment plan.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class PlanPaymentScheduleItem : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid TreatmentPlanId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public bool IsUpfront { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public PlanInstallmentStatus Status { get; private set; }
    public Guid? LinkedPaymentId { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PlanPaymentScheduleItem()
    {
    }

    public PlanPaymentScheduleItem(
        Guid id,
        Guid clinicId,
        Guid treatmentPlanId,
        int installmentNumber,
        bool isUpfront,
        DateOnly dueDate,
        decimal amount,
        string currency,
        DateOnly currentClinicDate,
        DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        Id = id;
        ClinicId = clinicId;
        TreatmentPlanId = treatmentPlanId;
        InstallmentNumber = installmentNumber;
        IsUpfront = isUpfront;
        DueDate = dueDate;
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        PaidAmount = 0m;
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        Status = dueDate < currentClinicDate ? PlanInstallmentStatus.Overdue : PlanInstallmentStatus.Pending;
        UpdatedAtUtc = nowUtc;
    }

    public decimal GetRemainingAmount() => Math.Max(0m, Math.Round(Amount - PaidAmount, 2, MidpointRounding.AwayFromZero));

    public void ApplyPayment(decimal amount, Guid paymentId, DateOnly currentClinicDate, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        PaidAmount = Math.Min(Amount, Math.Round(PaidAmount + amount, 2, MidpointRounding.AwayFromZero));
        LinkedPaymentId = paymentId;
        PaidAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        RefreshStatus(currentClinicDate);
    }

    public void ReversePayment(decimal amount, DateOnly currentClinicDate, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        PaidAmount = Math.Max(0m, Math.Round(PaidAmount - amount, 2, MidpointRounding.AwayFromZero));
        UpdatedAtUtc = nowUtc;
        Status = PaidAmount == 0m
            ? PlanInstallmentStatus.Refunded
            : currentClinicDate > DueDate
                ? PlanInstallmentStatus.Overdue
                : PlanInstallmentStatus.PartiallyPaid;
    }

    public void RefreshStatus(DateOnly currentClinicDate)
    {
        if (PaidAmount >= Amount)
        {
            Status = PlanInstallmentStatus.Paid;
            return;
        }
        if (PaidAmount > 0m)
        {
            Status = PlanInstallmentStatus.PartiallyPaid;
            return;
        }
        Status = currentClinicDate > DueDate ? PlanInstallmentStatus.Overdue : PlanInstallmentStatus.Pending;
    }
}
