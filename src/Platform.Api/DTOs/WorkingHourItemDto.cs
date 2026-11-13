namespace Platform.Api.DTOs;

public sealed class WorkingHourItemDto
{
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "17:00";
    public bool IsClosed { get; set; }
}

