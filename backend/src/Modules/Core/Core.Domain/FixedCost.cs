using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Recurring or scheduled clinic fixed operating cost (rent, utilities, maintenance, etc.).
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class FixedCost : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public FixedCostCategory Category { get; private set; }
    public string TitleAr { get; private set; } = string.Empty;
    public string TitleEn { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public FixedCostRecurrence Recurrence { get; private set; }
    public PaymentMethod PaidFromMethod { get; private set; }
    public DateOnly EffectiveFromDate { get; private set; }
    public DateOnly? EffectiveToDate { get; private set; }
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private FixedCost()
    {
    }

    public FixedCost(
        Guid id,
        Guid clinicId,
        FixedCostCategory category,
        string titleAr,
        string titleEn,
        Money amount,
        FixedCostRecurrence recurrence,
        PaymentMethod paidFromMethod,
        DateOnly effectiveFromDate,
        DateOnly? effectiveToDate,
        string? notes,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(titleAr) && string.IsNullOrWhiteSpace(titleEn))
        {
            throw new ArgumentException("Fixed cost title is required.", nameof(titleEn));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Fixed cost amount must be positive.");
        }

        if (effectiveToDate.HasValue && effectiveToDate.Value < effectiveFromDate)
        {
            throw new ArgumentException("EffectiveToDate cannot precede EffectiveFromDate.", nameof(effectiveToDate));
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        Category = category;
        TitleAr = string.IsNullOrWhiteSpace(titleAr) ? titleEn.Trim() : titleAr.Trim();
        TitleEn = string.IsNullOrWhiteSpace(titleEn) ? titleAr.Trim() : titleEn.Trim();
        Amount = amount.Amount;
        Currency = amount.Currency;
        Recurrence = recurrence;
        PaidFromMethod = paidFromMethod;
        EffectiveFromDate = effectiveFromDate;
        EffectiveToDate = effectiveToDate;
        IsActive = true;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        OperationId = operationId;
    }

    public void Update(
        FixedCostCategory category,
        string titleAr,
        string titleEn,
        Money amount,
        FixedCostRecurrence recurrence,
        PaymentMethod paidFromMethod,
        DateOnly effectiveFromDate,
        DateOnly? effectiveToDate,
        bool isActive,
        string? notes)
    {
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Fixed cost amount must be positive.");
        }

        if (effectiveToDate.HasValue && effectiveToDate.Value < effectiveFromDate)
        {
            throw new ArgumentException("EffectiveToDate cannot precede EffectiveFromDate.", nameof(effectiveToDate));
        }

        Category = category;
        TitleAr = string.IsNullOrWhiteSpace(titleAr) ? titleEn.Trim() : titleAr.Trim();
        TitleEn = string.IsNullOrWhiteSpace(titleEn) ? titleAr.Trim() : titleEn.Trim();
        Amount = amount.Amount;
        Currency = amount.Currency;
        Recurrence = recurrence;
        PaidFromMethod = paidFromMethod;
        EffectiveFromDate = effectiveFromDate;
        EffectiveToDate = effectiveToDate;
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}

