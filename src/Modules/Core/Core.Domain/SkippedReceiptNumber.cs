using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Records any receipt number skipped or voided from an offline/device receipt block so every number is accounted for.
/// </summary>
public sealed class SkippedReceiptNumber : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public Guid? NumberBlockId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SkippedReceiptNumber()
    {
    }

    public SkippedReceiptNumber(
        Guid id,
        Guid clinicId,
        Guid? deviceId,
        Guid? numberBlockId,
        string receiptNumber,
        string reason,
        string recordedByUserId,
        DateTimeOffset recordedAtUtc,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new ArgumentException("ReceiptNumber is required.", nameof(receiptNumber));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Reason is required for a skipped receipt number.");
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        DeviceId = deviceId;
        NumberBlockId = numberBlockId;
        ReceiptNumber = receiptNumber.Trim().ToUpperInvariant();
        Reason = reason.Trim();
        RecordedByUserId = string.IsNullOrWhiteSpace(recordedByUserId) ? "system" : recordedByUserId.Trim();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        OperationId = operationId;
    }
}

