using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Allocation linking a posted Payment to a specific Charge (supports partial and multi-charge settlement).
/// </summary>
public sealed class PaymentAllocation : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid ChargeId { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PaymentAllocation()
    {
    }

    public PaymentAllocation(
        Guid id,
        Guid clinicId,
        Guid paymentId,
        Guid chargeId,
        Money allocatedAmount,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId is required.", nameof(paymentId));
        }

        if (chargeId == Guid.Empty)
        {
            throw new ArgumentException("ChargeId is required.", nameof(chargeId));
        }

        if (allocatedAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedAmount), "Allocated amount must be positive.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PaymentId = paymentId;
        ChargeId = chargeId;
        AllocatedAmount = allocatedAmount.Amount;
        Currency = allocatedAmount.Currency;
        CreatedAtUtc = nowUtc.ToUniversalTime();
    }
}

