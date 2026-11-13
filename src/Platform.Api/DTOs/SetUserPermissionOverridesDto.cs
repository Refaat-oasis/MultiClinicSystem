namespace Platform.Api.DTOs;

public sealed class SetUserPermissionOverridesDto
{
    public List<PermissionOverrideItemDto> Overrides { get; set; } = new();
}

