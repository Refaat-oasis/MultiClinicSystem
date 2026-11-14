namespace Core.Application;

/// <summary>
/// Line item in a pack-based supplier purchase invoice allowing pack size and pack price overrides per invoice.
/// </summary>
public sealed record PurchaseInvoiceLineInput(
    Guid ItemId,
    int? PackSize,
    int PackCount,
    decimal PackPriceAmount,
    string BatchNumber,
    DateOnly? ExpiryDate);

