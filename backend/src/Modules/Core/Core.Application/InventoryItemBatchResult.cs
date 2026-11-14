namespace Core.Application;

/// <summary>
/// Read projection for a lot/batch with expiry and remaining stock ordered for FEFO allocation.
/// </summary>
public sealed record InventoryItemBatchResult(
    Guid Id,
    Guid ClinicId,
    Guid ItemId,
    string ItemCode,
    string ItemNameAr,
    string ItemNameEn,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    decimal UnitCostAmount,
    string Currency,
    bool IsExpired,
    bool IsNearExpiry,
    Guid? PurchaseInvoiceId,
    DateTimeOffset CreatedAtUtc);

