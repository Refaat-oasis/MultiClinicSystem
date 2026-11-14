using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Append-only payment recorded against a Supplier balance (reduces clinic cash/bank total on daily closing).
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class SupplierPayment : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid SupplierId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string? ReferenceNote { get; private set; }
    public string PaidByUserId { get; private set; } = string.Empty;
    public DateTimeOffset PaidAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SupplierPayment()
    {
    }

    public SupplierPayment(
        Guid id,
        Guid clinicId,
        Guid supplierId,
        PaymentMethod method,
        Money amount,
        string? referenceNote,
        string paidByUserId,
        DateTimeOffset paidAtUtc,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("SupplierId is required.", nameof(supplierId));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Supplier payment amount must be positive.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        SupplierId = supplierId;
        Method = method;
        Amount = amount.Amount;
        Currency = amount.Currency;
        ReferenceNote = string.IsNullOrWhiteSpace(referenceNote) ? null : referenceNote.Trim();
        PaidByUserId = string.IsNullOrWhiteSpace(paidByUserId) ? "system" : paidByUserId.Trim();
        PaidAtUtc = paidAtUtc.ToUniversalTime();
        OperationId = operationId;
    }
}

