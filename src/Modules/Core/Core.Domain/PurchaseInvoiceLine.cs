using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Line item inside a pack-based PurchaseInvoice computing total units and per-unit cost from box price and pack size.
/// Conflict class: AppendOnly.
/// </summary>
public sealed class PurchaseInvoiceLine : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PurchaseInvoiceId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid BatchId { get; private set; }
    public int PackSize { get; private set; }
    public int PackCount { get; private set; }
    public decimal TotalUnits { get; private set; }
    public decimal PackPriceAmount { get; private set; }
    public decimal LineTotalAmount { get; private set; }
    public decimal UnitCostAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string BatchNumber { get; private set; } = string.Empty;
    public DateOnly? ExpiryDate { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PurchaseInvoiceLine()
    {
    }

    public PurchaseInvoiceLine(
        Guid id,
        Guid clinicId,
        Guid purchaseInvoiceId,
        Guid itemId,
        Guid batchId,
        int packSize,
        int packCount,
        Money packPrice,
        string batchNumber,
        DateOnly? expiryDate)
    {
        if (clinicId == Guid.Empty || purchaseInvoiceId == Guid.Empty || itemId == Guid.Empty || batchId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId, PurchaseInvoiceId, ItemId, and BatchId are required.");
        }

        if (packSize <= 0 || packCount <= 0 || packPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(packSize), "PackSize, PackCount must be positive and PackPrice non-negative.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        PurchaseInvoiceId = purchaseInvoiceId;
        ItemId = itemId;
        BatchId = batchId;
        PackSize = packSize;
        PackCount = packCount;
        TotalUnits = checked(packSize * packCount);
        PackPriceAmount = packPrice.Amount;
        LineTotalAmount = decimal.Round(packPrice.Amount * packCount, 2, MidpointRounding.AwayFromZero);
        UnitCostAmount = InventoryFefoAllocator.CalculatePackUnitCost(packPrice.Amount, packSize, packCount);
        Currency = packPrice.Currency;
        BatchNumber = batchNumber.Trim();
        ExpiryDate = expiryDate;
    }
}

