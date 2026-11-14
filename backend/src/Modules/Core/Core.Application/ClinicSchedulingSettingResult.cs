namespace Core.Application;

/// <summary>
/// Owner-configured scheduling and waiting-queue policies for a clinic.
/// </summary>
public sealed record ClinicSchedulingSettingResult(
    int VipJumpLimit,
    int LateGraceMinutes,
    int MaxQueuePushes,
    int DefaultSlotStepMinutes,
    int MinBookingLeadMinutes,
    string RescheduleOptionsJson);

