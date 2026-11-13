namespace Platform.Api.DTOs;

public sealed class CreateClinicRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public List<string> Permissions { get; set; } = new();
}
