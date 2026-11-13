using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for Super Admin step-up password confirmation.
/// </summary>
public sealed class PlatformStepUpDto
{
    [Required]
    public string Password { get; set; } = string.Empty;
}

