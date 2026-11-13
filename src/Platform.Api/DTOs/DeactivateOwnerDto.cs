namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for deactivating an owner with optional inline step-up password.
/// </summary>
public sealed class DeactivateOwnerDto
{
    public string? StepUpPassword { get; set; }

    public string? Reason { get; set; }
}

