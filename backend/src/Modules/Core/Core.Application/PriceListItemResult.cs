using Core.Domain;

namespace Core.Application;

public sealed record PriceListItemResult(
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

