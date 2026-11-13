namespace Platform.Api.ViewModels;

public sealed record PortalBookableCategoryItemViewModel(
    Guid CategoryId,
    string SpecialtyCode,
    string NameAr,
    string NameEn,
    int DurationMinutes,
    decimal? PriceAmount,
    string? PriceCurrency);
