namespace Platform.Api.DTOs;

public sealed class SaveWorkingHoursDto
{
    public Guid? DoctorId { get; set; }
    public List<WorkingHourItemDto> Items { get; set; } = new();
}
