namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to reschedule an appointment via preset or custom time interval.
/// </summary>
public sealed class RescheduleAppointmentDto
{
    public string? PresetKey { get; set; }
    public DateTimeOffset? CustomStartUtc { get; set; }
    public DateTimeOffset? CustomEndUtc { get; set; }
    public Guid? NewDoctorId { get; set; }
    public Guid? NewRoomId { get; set; }
    public Guid? OperationId { get; set; }
}

