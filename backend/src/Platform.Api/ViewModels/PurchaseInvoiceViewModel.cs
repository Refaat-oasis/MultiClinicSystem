namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for a supplier purchase invoice with computed line details.
/// </summary>
public sealed record PurchaseInvoiceViewModel(
    Guid Id,
    Guid ClinicId,
    Guid? SupplierId,
    string SupplierName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string Currency,
    string? Notes,
    string RecordedByUserId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PurchaseInvoiceLineViewModel> Lines);

