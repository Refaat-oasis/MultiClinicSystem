namespace Platform.Api.DTOs;

public sealed class UpdateClinicRoomDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? SpecialtyCode { get; set; }
    public string? FloorOrLocation { get; set; }
    public bool IsActive { get; set; } = true;
}
