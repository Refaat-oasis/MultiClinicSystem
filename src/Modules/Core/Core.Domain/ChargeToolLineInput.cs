namespace Core.Domain;

/// <summary>
/// Pure input item representing a tool or billable line with cost and patient price for markup computation.
/// </summary>
public sealed record ChargeToolLineInput(
    ChargeLineKind Kind,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    decimal UnitChargedPrice,
    Guid? InventoryItemId = null);

