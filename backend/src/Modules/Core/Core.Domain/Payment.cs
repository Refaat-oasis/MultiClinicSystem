using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Strictly append-only payment record with number-block issued receipt number. Never deleted or modified after posting.
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class Payment : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public bool IsDeposit { get; private set; }
    public Guid? PaymentGroupId { get; private set; }
    public string? Notes { get; private set; }
    public string CollectedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset PaidAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Payment()
    {
    }

    public Payment(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? deviceId,
        string receiptNumber,
        PaymentMethod method,
        Money amount,
        bool isDeposit,
        Guid? paymentGroupId,
        string? notes,
        string collectedByUserId,
        DateTimeOffset paidAtUtc,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new ArgumentException("ReceiptNumber is required and must come from a number block.", nameof(receiptNumber));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        DeviceId = deviceId;
        ReceiptNumber = receiptNumber.Trim().ToUpperInvariant();
        Method = method;
        Amount = amount.Amount;
        Currency = amount.Currency;
        IsDeposit = isDeposit;
        PaymentGroupId = paymentGroupId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CollectedByUserId = string.IsNullOrWhiteSpace(collectedByUserId) ? "system" : collectedByUserId.Trim();
        PaidAtUtc = paidAtUtc.ToUniversalTime();
        OperationId = operationId;
    }

    public Money GetAmount() => new(Amount, Currency);
}

