using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record PriceListItemViewModel(
    Guid Id,
    Guid ClinicId,
    string PriceCode,
    string NameAr,
    string NameEn,
    ChargeLineKind Kind,
    decimal UnitCostAmount,
    decimal DefaultPriceAmount,
    string Currency,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc);

