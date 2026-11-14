using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record ChargeLineDto(
    ChargeLineKind Kind,
    Guid? PriceListItemId,
    Guid? InventoryItemId,
    string Description,
    decimal Quantity,
    decimal UnitCostAmount,
    decimal UnitChargedPriceAmount);

