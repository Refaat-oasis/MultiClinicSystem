using Core.Domain;

namespace Core.Application;

public sealed record ChargeLineResult(
    Guid Id,
    ChargeLineKind Kind,
    Guid? PriceListItemId,
    Guid? InventoryItemId,
    string Description,
    decimal Quantity,
    decimal UnitCostAmount,
    decimal UnitChargedPriceAmount,
    decimal LineCostAmount,
    decimal LineTotalAmount,
    decimal MarkupAmount,
    string Currency);

