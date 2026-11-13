using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Automated inventory threshold or expiry alert persisted once per condition window and dispatched via SignalR/owner alerts.
/// Conflict class: AppendOnly.
/// </summary>
public sealed class InventoryAlert : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? BatchId { get; private set; }
    public InventoryAlertKind Kind { get; private set; }
    public string MessageAr { get; private set; } = string.Empty;
    public string MessageEn { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private InventoryAlert()
    {
    }

    public InventoryAlert(
        Guid id,
        Guid clinicId,
        Guid itemId,
        Guid? batchId,
        InventoryAlertKind kind,
        string messageAr,
        string messageEn,
        DateTimeOffset createdAtUtc)
    {
        if (clinicId == Guid.Empty || itemId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId and ItemId are required.");
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        ItemId = itemId;
        BatchId = batchId;
        Kind = kind;
        MessageAr = messageAr.Trim();
        MessageEn = messageEn.Trim();
        IsResolved = false;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public void Resolve(DateTimeOffset nowUtc)
    {
        IsResolved = true;
        ResolvedAtUtc = nowUtc.ToUniversalTime();
    }
}

