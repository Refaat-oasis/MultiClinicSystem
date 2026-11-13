using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Strictly append-only financial ledger entry. Patient balance is always derived as SUM(SignedAmount).
/// ConflictClass: AppendOnly.
/// </summary>
public sealed class LedgerEntry : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public LedgerEntryType EntryType { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string ReferenceKind { get; private set; } = string.Empty;
    public decimal SignedAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public PaymentMethod? Method { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private LedgerEntry()
    {
    }

    public LedgerEntry(
        Guid id,
        Guid clinicId,
        Guid patientId,
        LedgerEntryType entryType,
        Guid referenceId,
        string referenceKind,
        decimal signedAmount,
        string currency,
        PaymentMethod? method,
        string description,
        string actorUserId,
        DateTimeOffset occurredAtUtc,
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

        if (referenceId == Guid.Empty)
        {
            throw new ArgumentException("ReferenceId is required.", nameof(referenceId));
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        EntryType = entryType;
        ReferenceId = referenceId;
        ReferenceKind = string.IsNullOrWhiteSpace(referenceKind) ? entryType.ToString() : referenceKind.Trim();
        SignedAmount = Math.Round(signedAmount, 2, MidpointRounding.AwayFromZero);
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        Method = method;
        Description = string.IsNullOrWhiteSpace(description) ? entryType.ToString() : description.Trim();
        ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim();
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        OperationId = operationId;
    }
}

