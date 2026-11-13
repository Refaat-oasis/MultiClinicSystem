namespace Platform.Api.DTOs;

public sealed class CreateClinicDayOffDto
{
    public Guid? DoctorId { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string ReasonAr { get; set; } = string.Empty;
    public string ReasonEn { get; set; } = string.Empty;
}
