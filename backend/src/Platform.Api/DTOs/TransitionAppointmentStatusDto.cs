namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to transition an appointment to a valid next status.
/// </summary>
public sealed class TransitionAppointmentStatusDto
{
    public string NextStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

