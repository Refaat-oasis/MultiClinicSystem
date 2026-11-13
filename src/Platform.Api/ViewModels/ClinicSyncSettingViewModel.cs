namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for clinic offline sync configuration.
/// </summary>
public sealed record ClinicSyncSettingViewModel(
    Guid ClinicId,
    int OfflineSessionMaxHours,
    int ReceiptBlockSize,
    int ReceiptBlockLowWaterMark,
    int ClockSkewWarningSeconds,
    DateTimeOffset UpdatedAtUtc);

