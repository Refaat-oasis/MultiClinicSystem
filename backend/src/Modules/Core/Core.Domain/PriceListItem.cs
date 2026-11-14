using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-clinic price list catalog item representing procedures, tools, extras, or lab fees.
/// </summary>
public sealed class PriceListItem : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string PriceCode { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public ChargeLineKind Kind { get; private set; }
    public decimal UnitCostAmount { get; private set; }
    public decimal DefaultPriceAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public bool IsActive { get; private set; } = true;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PriceListItem()
    {
    }

    public PriceListItem(
        Guid id,
        Guid clinicId,
        string priceCode,
        string nameAr,
        string nameEn,
        ChargeLineKind kind,
        Money unitCost,
        Money defaultPrice,
        Guid? operationId,
        DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(priceCode))
        {
            throw new ArgumentException("PriceCode is required.", nameof(priceCode));
        }

        if (string.IsNullOrWhiteSpace(nameAr) && string.IsNullOrWhiteSpace(nameEn))
        {
            throw new ArgumentException("At least one localized name is required.", nameof(nameAr));
        }

        if (unitCost.Amount < 0m || defaultPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPrice), "Price amounts cannot be negative.");
        }

        if (!string.Equals(unitCost.Currency, defaultPrice.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unit cost and default price must share the same currency.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PriceCode = priceCode.Trim().ToUpperInvariant();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? nameEn.Trim() : nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? nameAr.Trim() : nameEn.Trim();
        Kind = kind;
        UnitCostAmount = unitCost.Amount;
        DefaultPriceAmount = defaultPrice.Amount;
        Currency = defaultPrice.Currency;
        IsActive = true;
        OperationId = operationId;
        CreatedAtUtc = nowUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Money GetUnitCost() => new(UnitCostAmount, Currency);
    public Money GetDefaultPrice() => new(DefaultPriceAmount, Currency);

    public void Update(
        string nameAr,
        string nameEn,
        ChargeLineKind kind,
        Money unitCost,
        Money defaultPrice,
        bool isActive,
        DateTimeOffset nowUtc)
    {
        if (unitCost.Amount < 0m || defaultPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPrice), "Price amounts cannot be negative.");
        }

        NameAr = string.IsNullOrWhiteSpace(nameAr) ? NameAr : nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? NameEn : nameEn.Trim();
        Kind = kind;
        UnitCostAmount = unitCost.Amount;
        DefaultPriceAmount = defaultPrice.Amount;
        Currency = defaultPrice.Currency;
        IsActive = isActive;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}

