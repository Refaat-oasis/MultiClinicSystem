using Core.Domain;

namespace Core.Application;

public sealed record ChargeLineInput(
    ChargeLineKind Kind,
    Guid? PriceListItemId,
    Guid? InventoryItemId,
    string Description,
    decimal Quantity,
    decimal UnitCostAmount,
    decimal UnitChargedPriceAmount);

