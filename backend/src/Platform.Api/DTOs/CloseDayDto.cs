namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to close a clinic or doctor day/range and list affected appointments.
/// </summary>
public sealed class CloseDayDto
{
    public Guid? DoctorId { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string ReasonAr { get; set; } = string.Empty;
    public string ReasonEn { get; set; } = string.Empty;
}

