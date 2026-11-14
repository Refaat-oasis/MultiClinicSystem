namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to update owner-configured clinic scheduling and queue settings.
/// </summary>
public sealed class UpdateClinicSchedulingSettingsDto
{
    public int VipJumpLimit { get; set; } = 2;
    public int LateGraceMinutes { get; set; } = 10;
    public int MaxQueuePushes { get; set; } = 3;
    public int DefaultSlotStepMinutes { get; set; } = 15;
    public int MinBookingLeadMinutes { get; set; }
    public string? RescheduleOptionsJson { get; set; }
}

