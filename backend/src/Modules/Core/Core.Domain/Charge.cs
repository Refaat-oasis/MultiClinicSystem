using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Patient visit or manual billing charge header storing pricing breakdown, discounts, markup, and payment allocation totals.
/// ConflictClass: ServerValidated.
/// </summary>
public sealed class Charge : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? VisitId { get; private set; }
    public Guid? AppointmentId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public ChargeStatus Status { get; private set; }
    public decimal StandardSessionPriceAmount { get; private set; }
    public bool IsFreeSession { get; private set; }
    public string? FreeSessionReason { get; private set; }
    public decimal? CustomSessionPriceAmount { get; private set; }
    public string? CustomPriceReason { get; private set; }
    public decimal BaseSessionPriceAmount { get; private set; }
    public decimal ToolsTotalCostAmount { get; private set; }
    public decimal ToolsTotalChargedAmount { get; private set; }
    public decimal ToolsMarkupAmount { get; private set; }
    public decimal ExtraChargesAmount { get; private set; }
    public string? ExtraChargeReason { get; private set; }
    public decimal GrossSubtotalAmount { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public decimal? DiscountFixedAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? DiscountReason { get; private set; }
    public decimal UnroundedNetTotalAmount { get; private set; }
    public decimal NetTotalAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string RoundingRuleApplied { get; private set; } = "NearestPound";
    public decimal PaidAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public string? Notes { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public bool IsVoided { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public string? VoidedBy { get; private set; }
    public string? VoidReason { get; private set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Charge()
    {
    }

    public Charge(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? visitId,
        Guid? appointmentId,
        Guid? doctorId,
        ChargeStatus status,
        decimal standardSessionPriceAmount,
        bool isFreeSession,
        string? freeSessionReason,
        decimal? customSessionPriceAmount,
        string? customPriceReason,
        decimal? discountPercent,
        decimal? discountFixedAmount,
        string? discountReason,
        decimal? extraChargeAmount,
        string? extraChargeReason,
        ChargeCalculationResult calculation,
        string currency,
        string roundingRuleApplied,
        string? notes,
        string createdByUserId,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        if (isFreeSession && string.IsNullOrWhiteSpace(freeSessionReason))
        {
            throw new InvalidOperationException("Free session requires a reason.");
        }

        if (!isFreeSession && customSessionPriceAmount.HasValue && string.IsNullOrWhiteSpace(customPriceReason))
        {
            throw new InvalidOperationException("Custom session price requires a reason.");
        }

        if (calculation.DiscountAmount > 0m && string.IsNullOrWhiteSpace(discountReason))
        {
            throw new InvalidOperationException("Discount requires a reason.");
        }

        if ((extraChargeAmount ?? 0m) > 0m && string.IsNullOrWhiteSpace(extraChargeReason))
        {
            throw new InvalidOperationException("Extra charge requires a reason.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        VisitId = visitId;
        AppointmentId = appointmentId;
        DoctorId = doctorId;
        Status = status;
        StandardSessionPriceAmount = Math.Round(standardSessionPriceAmount, 2, MidpointRounding.AwayFromZero);
        IsFreeSession = isFreeSession;
        FreeSessionReason = string.IsNullOrWhiteSpace(freeSessionReason) ? null : freeSessionReason.Trim();
        CustomSessionPriceAmount = customSessionPriceAmount.HasValue
            ? Math.Round(customSessionPriceAmount.Value, 2, MidpointRounding.AwayFromZero)
            : null;
        CustomPriceReason = string.IsNullOrWhiteSpace(customPriceReason) ? null : customPriceReason.Trim();
        BaseSessionPriceAmount = calculation.BaseSessionPrice;
        ToolsTotalCostAmount = calculation.ToolsTotalCost;
        ToolsTotalChargedAmount = calculation.ToolsTotalCharged;
        ToolsMarkupAmount = calculation.ToolsMarkupIncome;
        ExtraChargesAmount = calculation.ExtraChargesTotal;
        ExtraChargeReason = string.IsNullOrWhiteSpace(extraChargeReason) ? null : extraChargeReason.Trim();
        GrossSubtotalAmount = calculation.GrossSubtotal;
        DiscountPercent = discountPercent;
        DiscountFixedAmount = discountFixedAmount;
        DiscountAmount = calculation.DiscountAmount;
        DiscountReason = string.IsNullOrWhiteSpace(discountReason) ? null : discountReason.Trim();
        UnroundedNetTotalAmount = calculation.UnroundedNetTotal;
        NetTotalAmount = calculation.RoundedNetTotal;
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        RoundingRuleApplied = string.IsNullOrWhiteSpace(roundingRuleApplied) ? "NearestPound" : roundingRuleApplied.Trim();
        PaidAmount = 0m;
        RefundedAmount = 0m;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = string.IsNullOrWhiteSpace(createdByUserId) ? "system" : createdByUserId.Trim();
        OperationId = operationId;
        CreatedAtUtc = nowUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Money GetNetTotal() => new(NetTotalAmount, Currency);

    public decimal GetRemainingDueAmount()
    {
        if (Status == ChargeStatus.Voided)
        {
            return 0m;
        }

        decimal effectivePaid = Math.Max(0m, PaidAmount - RefundedAmount);
        return Math.Max(0m, Math.Round(NetTotalAmount - effectivePaid, 2, MidpointRounding.AwayFromZero));
    }

    public void Post(DateTimeOffset nowUtc)
    {
        if (Status == ChargeStatus.Voided)
        {
            throw new InvalidOperationException("Cannot post a voided charge.");
        }

        Status = ChargeStatus.Posted;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void ApplyAllocation(decimal amount, DateTimeOffset nowUtc)
    {
        if (Status != ChargeStatus.Posted)
        {
            throw new InvalidOperationException("Payments can only be allocated to posted charges.");
        }

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Allocation amount must be positive.");
        }

        PaidAmount = Math.Round(PaidAmount + amount, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void RecordRefund(decimal amount, DateTimeOffset nowUtc)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        }

        RefundedAmount = Math.Round(RefundedAmount + amount, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Void(string actorId, string reason, DateTimeOffset nowUtc)
    {
        if (Status == ChargeStatus.Voided)
        {
            throw new InvalidOperationException("Charge is already voided.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Voiding a charge requires a reason.");
        }

        Status = ChargeStatus.Voided;
        IsVoided = true;
        VoidedBy = string.IsNullOrWhiteSpace(actorId) ? "system" : actorId.Trim();
        VoidReason = reason.Trim();
        VoidedAtUtc = nowUtc.ToUniversalTime();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}
