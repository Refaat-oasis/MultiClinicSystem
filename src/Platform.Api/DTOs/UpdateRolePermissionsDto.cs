namespace Platform.Api.DTOs;

public sealed class UpdateRolePermissionsDto
{
    public List<string> Permissions { get; set; } = new();
}
