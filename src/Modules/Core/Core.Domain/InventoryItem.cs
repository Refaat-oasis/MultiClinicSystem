using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Inventory stock item tracked per clinic with pack definition, weighted unit cost, markup, and low-stock threshold.
/// Conflict class: ServerValidated.
/// </summary>
public sealed class InventoryItem : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string NormalizedSearchName { get; private set; } = string.Empty;
    public string UnitName { get; private set; } = string.Empty;
    public int PackSize { get; private set; }
    public decimal UnitCostAmount { get; private set; }
    public decimal DefaultMarkupPercent { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public decimal LowStockThreshold { get; private set; }
    public decimal QuantityOnHand { get; private set; }
    public string? SpecialtyCode { get; private set; }
    public string? StorageLocation { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private InventoryItem()
    {
    }

    public InventoryItem(
        Guid id,
        Guid clinicId,
        string code,
        string nameAr,
        string nameEn,
        string unitName,
        int packSize,
        Money unitCost,
        decimal defaultMarkupPercent,
        decimal lowStockThreshold,
        string? specialtyCode,
        string? storageLocation,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            throw new ArgumentException("Item code and bilingual names are required.", nameof(code));
        }

        if (packSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(packSize), "PackSize must be positive.");
        }

        if (unitCost.Amount < 0m || defaultMarkupPercent < 0m || lowStockThreshold < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Costs, markup, and thresholds cannot be negative.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        Code = code.Trim().ToUpperInvariant();
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        NormalizedSearchName = $"{ArabicNormalizer.NormalizeText(NameAr)} {NameEn.ToUpperInvariant()}".Trim();
        UnitName = string.IsNullOrWhiteSpace(unitName) ? "unit" : unitName.Trim();
        PackSize = packSize;
        UnitCostAmount = unitCost.Amount;
        Currency = unitCost.Currency;
        DefaultMarkupPercent = decimal.Round(defaultMarkupPercent, 2, MidpointRounding.AwayFromZero);
        LowStockThreshold = decimal.Round(lowStockThreshold, 2, MidpointRounding.AwayFromZero);
        QuantityOnHand = 0m;
        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? null : specialtyCode.Trim().ToLowerInvariant();
        StorageLocation = string.IsNullOrWhiteSpace(storageLocation) ? null : storageLocation.Trim();
        IsActive = true;
        OperationId = operationId;
        CreatedAtUtc = nowUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void UpdateMetadata(
        string nameAr,
        string nameEn,
        string unitName,
        int packSize,
        Money unitCost,
        decimal defaultMarkupPercent,
        decimal lowStockThreshold,
        string? specialtyCode,
        string? storageLocation,
        bool isActive,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            throw new ArgumentException("Bilingual names are required.", nameof(nameAr));
        }

        if (packSize <= 0 || unitCost.Amount < 0m || defaultMarkupPercent < 0m || lowStockThreshold < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(packSize), "Invalid inventory item parameters.");
        }

        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        NormalizedSearchName = $"{ArabicNormalizer.NormalizeText(NameAr)} {NameEn.ToUpperInvariant()}".Trim();
        UnitName = string.IsNullOrWhiteSpace(unitName) ? UnitName : unitName.Trim();
        PackSize = packSize;
        UnitCostAmount = unitCost.Amount;
        Currency = unitCost.Currency;
        DefaultMarkupPercent = decimal.Round(defaultMarkupPercent, 2, MidpointRounding.AwayFromZero);
        LowStockThreshold = decimal.Round(lowStockThreshold, 2, MidpointRounding.AwayFromZero);
        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? null : specialtyCode.Trim().ToLowerInvariant();
        StorageLocation = string.IsNullOrWhiteSpace(storageLocation) ? null : storageLocation.Trim();
        IsActive = isActive;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void ReceivePurchaseUnits(decimal totalUnits, decimal purchaseUnitCost, int effectivePackSize, DateTimeOffset nowUtc)
    {
        if (totalUnits <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalUnits), "Purchase units must be positive.");
        }

        if (purchaseUnitCost < 0m || effectivePackSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(purchaseUnitCost), "Invalid unit cost or pack size.");
        }

        QuantityOnHand = decimal.Round(QuantityOnHand + totalUnits, 2, MidpointRounding.AwayFromZero);
        PackSize = effectivePackSize;
        UnitCostAmount = decimal.Round(purchaseUnitCost, 2, MidpointRounding.AwayFromZero);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public decimal AdjustQuantity(decimal delta, NegativeStockPolicy policy, DateTimeOffset nowUtc)
    {
        decimal next = decimal.Round(QuantityOnHand + delta, 2, MidpointRounding.AwayFromZero);
        if (next < 0m && policy == NegativeStockPolicy.Block)
        {
            throw new InvalidOperationException("INSUFFICIENT_STOCK: Resulting stock cannot be negative under current policy.");
        }

        QuantityOnHand = next;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
        return QuantityOnHand;
    }

    public Money GetPatientMarkedUpPrice()
    {
        decimal patientPrice = InventoryFefoAllocator.CalculateMarkedUpPatientPrice(UnitCostAmount, DefaultMarkupPercent);
        return new Money(patientPrice, Currency);
    }

    public bool IsAtOrBelowLowStockThreshold() => QuantityOnHand <= LowStockThreshold;

    public void Void(string actorUserId, string reason, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Void reason is required.", nameof(reason));
        }

        IsVoided = true;
        VoidedAtUtc = nowUtc.ToUniversalTime();
        VoidedBy = actorUserId;
        VoidReason = reason.Trim();
        UpdatedAtUtc = VoidedAtUtc.Value;
    }
}
