using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for requesting a fresh verification code.
/// </summary>
public sealed class ResendCodeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Purpose { get; set; } = "Invitation";
}
