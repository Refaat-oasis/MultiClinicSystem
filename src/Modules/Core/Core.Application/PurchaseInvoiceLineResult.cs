namespace Core.Application;

/// <summary>
/// Read projection for an individual line item on a supplier purchase invoice.
/// </summary>
public sealed record PurchaseInvoiceLineResult(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemNameAr,
    string ItemNameEn,
    Guid BatchId,
    int PackSize,
    int PackCount,
    decimal TotalUnits,
    decimal PackPriceAmount,
    decimal LineTotalAmount,
    decimal UnitCostAmount,
    string Currency,
    string BatchNumber,
    DateOnly? ExpiryDate);

