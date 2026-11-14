using Core.Domain;

namespace Platform.Api.DTOs;

/// <summary>
/// Request DTO for receiving a supplier purchase invoice into clinic stock.
/// </summary>
public sealed class ReceivePurchaseInvoiceDto
{
    public Guid? Id { get; set; }
    public Guid? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<PurchaseInvoiceLineDto> Lines { get; set; } = Array.Empty<PurchaseInvoiceLineDto>();
    public Guid? OperationId { get; set; }
}

