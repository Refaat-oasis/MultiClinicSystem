namespace Core.Application;

/// <summary>
/// Read projection for a supplier purchase invoice header and its pack-derived batch lines.
/// </summary>
public sealed record PurchaseInvoiceResult(
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
    IReadOnlyList<PurchaseInvoiceLineResult> Lines);

