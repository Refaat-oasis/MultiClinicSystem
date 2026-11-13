using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record ChargeLineViewModel(
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

