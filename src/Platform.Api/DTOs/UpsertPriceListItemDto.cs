using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record UpsertPriceListItemDto(
    Guid? Id,
    string PriceCode,
    string NameAr,
    string NameEn,
    ChargeLineKind Kind,
    decimal UnitCostAmount,
    decimal DefaultPriceAmount,
    bool IsActive,
    Guid? OperationId);

