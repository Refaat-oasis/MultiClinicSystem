using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Specialty-linked appointment category with default duration, price (Money), and online-booking flag.
/// </summary>
public sealed class AppointmentCategory : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string SpecialtyCode { get; private set; }
    public string NameAr { get; private set; }
    public string NameEn { get; private set; }
    public int DefaultDurationMinutes { get; private set; }
    public decimal DefaultPriceAmount { get; private set; }
    public string DefaultPriceCurrency { get; private set; }
    public bool BookableOnline { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private AppointmentCategory()
    {
        SpecialtyCode = string.Empty;
        NameAr = string.Empty;
        NameEn = string.Empty;
        DefaultPriceCurrency = Money.DefaultCurrency;
    }

    public AppointmentCategory(
        Guid id,
        Guid clinicId,
        string specialtyCode,
        string nameAr,
        string nameEn,
        int defaultDurationMinutes,
        Money defaultPrice,
        bool bookableOnline,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Category ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(nameAr) && string.IsNullOrWhiteSpace(nameEn))
        {
            throw new ArgumentException("At least one category name is required.");
        }

        if (defaultDurationMinutes < 5 || defaultDurationMinutes > 480)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDurationMinutes), "Duration must be between 5 and 480 minutes.");
        }

        if (defaultPrice.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPrice), "Price cannot be negative.");
        }

        Id = id;
        ClinicId = clinicId;
        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? "general" : specialtyCode.Trim().ToLowerInvariant();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? nameEn.Trim() : nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? nameAr.Trim() : nameEn.Trim();
        DefaultDurationMinutes = defaultDurationMinutes;
        DefaultPriceAmount = defaultPrice.Amount;
        DefaultPriceCurrency = defaultPrice.Currency;
        BookableOnline = bookableOnline;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Money GetDefaultPrice() => new(DefaultPriceAmount, DefaultPriceCurrency);

    public void Update(
        string specialtyCode,
        string nameAr,
        string nameEn,
        int defaultDurationMinutes,
        Money defaultPrice,
        bool bookableOnline,
        bool isActive,
        DateTimeOffset now)
    {
        if (defaultDurationMinutes < 5 || defaultDurationMinutes > 480)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDurationMinutes), "Duration must be between 5 and 480 minutes.");
        }

        if (defaultPrice.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPrice), "Price cannot be negative.");
        }

        SpecialtyCode = string.IsNullOrWhiteSpace(specialtyCode) ? SpecialtyCode : specialtyCode.Trim().ToLowerInvariant();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? NameAr : nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? NameEn : nameEn.Trim();
        DefaultDurationMinutes = defaultDurationMinutes;
        DefaultPriceAmount = defaultPrice.Amount;
        DefaultPriceCurrency = defaultPrice.Currency;
        BookableOnline = bookableOnline;
        IsActive = isActive;
        UpdatedAtUtc = now;
    }

    public void Void(DateTimeOffset voidedAtUtc, string voidedBy, string? reason = null)
    {
        IsVoided = true;
        IsActive = false;
        VoidedAtUtc = voidedAtUtc;
        VoidedBy = voidedBy;
        VoidReason = reason;
    }
}
