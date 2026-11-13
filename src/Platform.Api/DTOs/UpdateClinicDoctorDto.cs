namespace Platform.Api.DTOs;

public sealed class UpdateClinicDoctorDto
{
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public List<string> SpecialtyCodes { get; set; } = new();
    public List<Guid> AssignedRoomIds { get; set; } = new();
    public int SlotDurationMinutes { get; set; } = 20;
    public bool IsActive { get; set; } = true;
}
