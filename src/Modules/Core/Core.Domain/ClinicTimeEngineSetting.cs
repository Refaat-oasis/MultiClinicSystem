using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-clinic calibration policy for the average-time learning engine.
/// ConflictClass: LastWriterWins.
/// </summary>
public sealed class ClinicTimeEngineSetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public int MinSampleSize { get; private set; } = 5;
    public decimal TrimFraction { get; private set; } = 0.10m;
    public decimal MaxStepChangePercent { get; private set; } = 20m;
    public Guid? OperationId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicTimeEngineSetting()
    {
    }

    public ClinicTimeEngineSetting(Guid id, Guid clinicId, DateTimeOffset nowUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(int minSampleSize, decimal trimFraction, decimal maxStepChangePercent, Guid? operationId, DateTimeOffset nowUtc)
    {
        if (minSampleSize < 2 || minSampleSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minSampleSize), "MinSampleSize must be between 2 and 100.");
        }

        if (trimFraction < 0m || trimFraction > 0.40m)
        {
            throw new ArgumentOutOfRangeException(nameof(trimFraction), "TrimFraction must be between 0.00 and 0.40.");
        }

        if (maxStepChangePercent < 1m || maxStepChangePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStepChangePercent), "MaxStepChangePercent must be between 1 and 100.");
        }

        MinSampleSize = minSampleSize;
        TrimFraction = trimFraction;
        MaxStepChangePercent = maxStepChangePercent;
        OperationId = operationId;
        UpdatedAtUtc = nowUtc;
    }

    public void Void(string reason, DateTimeOffset voidedAtUtc)
    {
        IsVoided = true;
        VoidedAtUtc = voidedAtUtc;
        VoidReason = reason;
        UpdatedAtUtc = voidedAtUtc;
    }
}
