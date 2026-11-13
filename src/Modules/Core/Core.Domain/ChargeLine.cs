using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Line item belonging to a Charge (session, inventory/manual tool with cost and markup, extra, or lab fee).
/// </summary>
public sealed class ChargeLine : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid ChargeId { get; private set; }
    public ChargeLineKind Kind { get; private set; }
    public Guid? PriceListItemId { get; private set; }
    public Guid? InventoryItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitCostAmount { get; private set; }
    public decimal UnitChargedPriceAmount { get; private set; }
    public decimal LineCostAmount { get; private set; }
    public decimal LineTotalAmount { get; private set; }
    public decimal MarkupAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ChargeLine()
    {
    }

    public ChargeLine(
        Guid id,
        Guid clinicId,
        Guid chargeId,
        ChargeLineKind kind,
        Guid? priceListItemId,
        Guid? inventoryItemId,
        string description,
        decimal quantity,
        decimal unitCostAmount,
        decimal unitChargedPriceAmount,
        string currency,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (chargeId == Guid.Empty)
        {
            throw new ArgumentException("ChargeId is required.", nameof(chargeId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (unitCostAmount < 0m || unitChargedPriceAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitChargedPriceAmount), "Amounts cannot be negative.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        ChargeId = chargeId;
        Kind = kind;
        PriceListItemId = priceListItemId;
        InventoryItemId = inventoryItemId;
        Description = description.Trim();
        Quantity = Math.Round(quantity, 2, MidpointRounding.AwayFromZero);
        UnitCostAmount = Math.Round(unitCostAmount, 2, MidpointRounding.AwayFromZero);
        UnitChargedPriceAmount = Math.Round(unitChargedPriceAmount, 2, MidpointRounding.AwayFromZero);
        LineCostAmount = Math.Round(UnitCostAmount * Quantity, 2, MidpointRounding.AwayFromZero);
        LineTotalAmount = Math.Round(UnitChargedPriceAmount * Quantity, 2, MidpointRounding.AwayFromZero);
        MarkupAmount = BillingCalculator.ComputeMarkup(UnitCostAmount, UnitChargedPriceAmount, Quantity);
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        CreatedAtUtc = nowUtc.ToUniversalTime();
    }
}

