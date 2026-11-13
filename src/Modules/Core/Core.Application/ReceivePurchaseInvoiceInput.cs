using Core.Domain;

namespace Core.Application;

/// <summary>
/// Input model for receiving a supplier purchase invoice with optional Finance payable/payment linkage.
/// </summary>
public sealed record ReceivePurchaseInvoiceInput(
    Guid? Id,
    Guid? SupplierId,
    string SupplierName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal PaidAmount,
    PaymentMethod? PaymentMethod,
    string? Notes,
    IReadOnlyList<PurchaseInvoiceLineInput> Lines,
    Guid? OperationId);

