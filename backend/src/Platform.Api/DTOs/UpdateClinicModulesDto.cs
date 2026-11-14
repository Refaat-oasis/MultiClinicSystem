namespace Platform.Api.DTOs;

public sealed class UpdateClinicModulesDto
{
    public List<ModuleToggleItemDto> Modules { get; set; } = new();
    public string? StepUpPassword { get; set; }
}

