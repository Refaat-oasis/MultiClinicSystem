namespace Platform.Api.DTOs;

/// <summary>
/// HTTP request payload for updating clinic offline sync settings.
/// </summary>
public sealed record UpdateClinicSyncSettingDto(
    int OfflineSessionMaxHours,
    int ReceiptBlockSize,
    int ReceiptBlockLowWaterMark,
    int ClockSkewWarningSeconds);

