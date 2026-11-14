namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for a pack-based supplier purchase invoice line.
/// </summary>
public sealed record PurchaseInvoiceLineViewModel(
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

