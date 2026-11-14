namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for an inventory lot/batch with expiry indicators.
/// </summary>
public sealed record InventoryItemBatchViewModel(
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

