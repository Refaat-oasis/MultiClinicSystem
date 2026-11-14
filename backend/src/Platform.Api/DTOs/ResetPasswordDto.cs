using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for resetting a user password using an emailed verification code.
/// </summary>
public sealed class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
