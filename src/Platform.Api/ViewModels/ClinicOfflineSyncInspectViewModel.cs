namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for Super Admin read-only offline sync inspection.
/// </summary>
public sealed record ClinicOfflineSyncInspectViewModel(
    Guid ClinicId,
    ClinicSyncSettingViewModel Settings,
    IReadOnlyList<SyncDeviceStateViewModel> Devices,
    IReadOnlyList<SyncConflictViewModel> Conflicts,
    IReadOnlyList<AllocatedNumberBlockViewModel> NumberBlocks);

