using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-clinic offline sync configuration: offline auth expiration window, number block size, low-water mark, and skew threshold.
/// </summary>
public sealed class ClinicSyncSetting : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public int OfflineSessionMaxHours { get; private set; } = 24;
    public int ReceiptBlockSize { get; private set; } = 50;
    public int ReceiptBlockLowWaterMark { get; private set; } = 10;
    public int ClockSkewWarningSeconds { get; private set; } = 300;
    public bool IsVoided { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicSyncSetting()
    {
    }

    public ClinicSyncSetting(
        Guid id,
        Guid clinicId,
        int offlineSessionMaxHours,
        int receiptBlockSize,
        int receiptBlockLowWaterMark,
        int clockSkewWarningSeconds,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        ClinicId = clinicId;
        Update(offlineSessionMaxHours, receiptBlockSize, receiptBlockLowWaterMark, clockSkewWarningSeconds, updatedAtUtc);
    }

    public void Update(
        int offlineSessionMaxHours,
        int receiptBlockSize,
        int receiptBlockLowWaterMark,
        int clockSkewWarningSeconds,
        DateTimeOffset updatedAtUtc)
    {
        if (offlineSessionMaxHours is < 1 or > 720)
        {
            throw new ArgumentOutOfRangeException(nameof(offlineSessionMaxHours));
        }
        if (receiptBlockSize is < 10 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(receiptBlockSize));
        }
        if (receiptBlockLowWaterMark < 1 || receiptBlockLowWaterMark >= receiptBlockSize)
        {
            throw new ArgumentOutOfRangeException(nameof(receiptBlockLowWaterMark));
        }
        if (clockSkewWarningSeconds is < 30 or > 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(clockSkewWarningSeconds));
        }

        OfflineSessionMaxHours = offlineSessionMaxHours;
        ReceiptBlockSize = receiptBlockSize;
        ReceiptBlockLowWaterMark = receiptBlockLowWaterMark;
        ClockSkewWarningSeconds = clockSkewWarningSeconds;
        UpdatedAtUtc = updatedAtUtc;
    }
}

