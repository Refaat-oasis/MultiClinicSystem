namespace Core.Application;

/// <summary>
/// Per-clinic offline sync configuration.
/// </summary>
public sealed record ClinicSyncSettingResult(
    Guid ClinicId,
    int OfflineSessionMaxHours,
    int ReceiptBlockSize,
    int ReceiptBlockLowWaterMark,
    int ClockSkewWarningSeconds,
    DateTimeOffset UpdatedAtUtc);

