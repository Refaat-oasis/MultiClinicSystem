namespace Platform.Api.DTOs;

public sealed class PermissionOverrideItemDto
{
    public string PermissionKey { get; set; } = string.Empty;
    public string Effect { get; set; } = "Grant";
}
