namespace Platform.Api.DTOs;

/// <summary>
/// Request DTO for one line on a pack-based supplier purchase invoice.
/// </summary>
public sealed class PurchaseInvoiceLineDto
{
    public Guid ItemId { get; set; }
    public int? PackSize { get; set; }
    public int PackCount { get; set; } = 1;
    public decimal PackPriceAmount { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
}

