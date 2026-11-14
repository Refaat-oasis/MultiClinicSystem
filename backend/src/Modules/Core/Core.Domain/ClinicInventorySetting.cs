using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-clinic inventory settings governing negative stock behavior and near-expiry warning window.
/// Conflict class: LastWriterWins.
/// </summary>
public sealed class ClinicInventorySetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public NegativeStockPolicy NegativeStockPolicy { get; private set; }
    public int NearExpiryWarningDays { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicInventorySetting()
    {
    }

    public ClinicInventorySetting(Guid id, Guid clinicId, DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        NegativeStockPolicy = NegativeStockPolicy.Block;
        NearExpiryWarningDays = 30;
        CreatedAtUtc = nowUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Update(NegativeStockPolicy policy, int nearExpiryWarningDays, DateTimeOffset nowUtc)
    {
        if (nearExpiryWarningDays < 1 || nearExpiryWarningDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(nearExpiryWarningDays), "Warning days must be between 1 and 365.");
        }

        NegativeStockPolicy = policy;
        NearExpiryWarningDays = nearExpiryWarningDays;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}

