namespace Core.Application;

public sealed record PortalBookableCategoryItemResult(
    Guid CategoryId,
    string SpecialtyCode,
    string NameAr,
    string NameEn,
    int DurationMinutes,
    decimal? PriceAmount,
    string? PriceCurrency);
