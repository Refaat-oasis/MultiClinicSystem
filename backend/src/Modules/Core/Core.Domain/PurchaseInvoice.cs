using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Pack-based supplier purchase invoice header linked to optional supplier ledger settlement.
/// Conflict class: AppendOnly.
/// </summary>
public sealed class PurchaseInvoice : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string SupplierName { get; private set; } = string.Empty;
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateOnly InvoiceDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainingAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string? Notes { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public Guid? OperationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PurchaseInvoice()
    {
    }

    public PurchaseInvoice(
        Guid id,
        Guid clinicId,
        Guid? supplierId,
        string supplierName,
        string invoiceNumber,
        DateOnly invoiceDate,
        Money totalAmount,
        Money paidAmount,
        string? notes,
        string recordedByUserId,
        Guid? operationId,
        DateTimeOffset createdAtUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber) || string.IsNullOrWhiteSpace(supplierName))
        {
            throw new ArgumentException("InvoiceNumber and SupplierName are required.");
        }

        if (totalAmount.Amount <= 0m || paidAmount.Amount < 0m || paidAmount.Amount > totalAmount.Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(paidAmount), "Paid amount must be between 0 and total invoice amount.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        SupplierId = supplierId;
        SupplierName = supplierName.Trim();
        InvoiceNumber = invoiceNumber.Trim();
        InvoiceDate = invoiceDate;
        TotalAmount = totalAmount.Amount;
        PaidAmount = paidAmount.Amount;
        RemainingAmount = decimal.Round(totalAmount.Amount - paidAmount.Amount, 2, MidpointRounding.AwayFromZero);
        Currency = totalAmount.Currency;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        RecordedByUserId = string.IsNullOrWhiteSpace(recordedByUserId) ? "system" : recordedByUserId.Trim();
        OperationId = operationId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}

