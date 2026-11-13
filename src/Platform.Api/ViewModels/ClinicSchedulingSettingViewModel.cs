namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for clinic scheduling and queue settings.
/// </summary>
public sealed record ClinicSchedulingSettingViewModel(
    int VipJumpLimit,
    int LateGraceMinutes,
    int MaxQueuePushes,
    int DefaultSlotStepMinutes,
    int MinBookingLeadMinutes,
    string RescheduleOptionsJson);

