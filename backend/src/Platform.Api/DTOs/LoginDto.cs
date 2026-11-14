using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for user login requests.
/// </summary>
public sealed class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public string? DeviceName { get; set; }
}
