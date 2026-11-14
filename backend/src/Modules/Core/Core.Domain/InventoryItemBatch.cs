using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Lot/batch instance of an inventory item carrying expiry date, purchase unit cost, and remaining quantity for FEFO deduction.
/// Conflict class: ServerValidated.
/// </summary>
public sealed class InventoryItemBatch : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid ItemId { get; private set; }
    public string BatchNumber { get; private set; } = string.Empty;
    public DateOnly? ExpiryDate { get; private set; }
    public decimal QuantityReceived { get; private set; }
    public decimal QuantityRemaining { get; private set; }
    public decimal UnitCostAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public Guid? PurchaseInvoiceId { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private InventoryItemBatch()
    {
    }

    public InventoryItemBatch(
        Guid id,
        Guid clinicId,
        Guid itemId,
        string batchNumber,
        DateOnly? expiryDate,
        decimal quantityReceived,
        Money unitCost,
        Guid? purchaseInvoiceId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty || itemId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId and ItemId are required.");
        }

        if (string.IsNullOrWhiteSpace(batchNumber))
        {
            throw new ArgumentException("Batch number is required.", nameof(batchNumber));
        }

        if (quantityReceived <= 0m || unitCost.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityReceived), "Batch quantity must be positive and unit cost non-negative.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        ItemId = itemId;
        BatchNumber = batchNumber.Trim();
        ExpiryDate = expiryDate;
        QuantityReceived = decimal.Round(quantityReceived, 2, MidpointRounding.AwayFromZero);
        QuantityRemaining = QuantityReceived;
        UnitCostAmount = unitCost.Amount;
        Currency = unitCost.Currency;
        PurchaseInvoiceId = purchaseInvoiceId;
        CreatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Deduct(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Deduction quantity must be positive.");
        }

        if (quantity > QuantityRemaining)
        {
            throw new InvalidOperationException("Cannot deduct more than remaining quantity in batch.");
        }

        QuantityRemaining = decimal.Round(QuantityRemaining - quantity, 2, MidpointRounding.AwayFromZero);
    }

    public void AdjustRemaining(decimal delta)
    {
        decimal next = decimal.Round(QuantityRemaining + delta, 2, MidpointRounding.AwayFromZero);
        if (next < 0m)
        {
            throw new InvalidOperationException("Batch remaining quantity cannot become negative.");
        }

        QuantityRemaining = next;
    }

    public bool IsExpired(DateOnly todayLocal)
    {
        return ExpiryDate.HasValue && QuantityRemaining > 0m && ExpiryDate.Value < todayLocal;
    }

    public bool IsNearExpiry(DateOnly todayLocal, int warningDays)
    {
        if (!ExpiryDate.HasValue || QuantityRemaining <= 0m)
        {
            return false;
        }

        int daysLeft = ExpiryDate.Value.DayNumber - todayLocal.DayNumber;
        return daysLeft >= 0 && daysLeft <= warningDays;
    }

    public void Void(string actorUserId, string reason, DateTimeOffset nowUtc)
    {
        IsVoided = true;
        VoidedAtUtc = nowUtc.ToUniversalTime();
        VoidedBy = actorUserId;
        VoidReason = reason.Trim();
    }
}
