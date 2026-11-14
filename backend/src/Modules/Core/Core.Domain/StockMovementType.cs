namespace Core.Domain;

/// <summary>
/// Classification of append-only inventory stock ledger movements.
/// </summary>
public enum StockMovementType
{
    PurchaseReceived = 0,
    SessionUsed = 1,
    ManualAdjustment = 2
}

