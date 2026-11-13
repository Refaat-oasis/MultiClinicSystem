namespace Platform.Api.DTOs;

public sealed class ModuleToggleItemDto
{
    public string ModuleKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Note { get; set; }
}
