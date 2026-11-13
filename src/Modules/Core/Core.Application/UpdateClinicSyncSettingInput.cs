namespace Core.Application;

/// <summary>
/// Input model to update per-clinic offline sync settings.
/// </summary>
public sealed record UpdateClinicSyncSettingInput(
    int OfflineSessionMaxHours,
    int ReceiptBlockSize,
    int ReceiptBlockLowWaterMark,
    int ClockSkewWarningSeconds);

