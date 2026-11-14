using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Append-only financial refund / reversal record. Never deletes or alters the original Payment row.
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class Refund : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? OriginalPaymentId { get; private set; }
    public Guid? ChargeId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string Reason { get; private set; } = string.Empty;
    public string? ReceiptNumber { get; private set; }
    public string IssuedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RefundedAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Refund()
    {
    }

    public Refund(
        Guid id,
        Guid clinicId,
        Guid patientId,
        Guid? originalPaymentId,
        Guid? chargeId,
        PaymentMethod method,
        Money amount,
        string reason,
        string? receiptNumber,
        string issuedByUserId,
        DateTimeOffset refundedAtUtc,
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

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Refund requires a reason.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        OriginalPaymentId = originalPaymentId;
        ChargeId = chargeId;
        Method = method;
        Amount = amount.Amount;
        Currency = amount.Currency;
        Reason = reason.Trim();
        ReceiptNumber = string.IsNullOrWhiteSpace(receiptNumber) ? null : receiptNumber.Trim().ToUpperInvariant();
        IssuedByUserId = string.IsNullOrWhiteSpace(issuedByUserId) ? "system" : issuedByUserId.Trim();
        RefundedAtUtc = refundedAtUtc.ToUniversalTime();
        OperationId = operationId;
    }

    public Money GetAmount() => new(Amount, Currency);
}

