using Core.Domain;

namespace Core.Application;

public sealed record UpsertPriceListItemInput(
    Guid? Id,
    string PriceCode,
    string NameAr,
    string NameEn,
    ChargeLineKind Kind,
    decimal UnitCostAmount,
    decimal DefaultPriceAmount,
    bool IsActive,
    Guid? OperationId);

