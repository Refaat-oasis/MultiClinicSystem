using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for Super Admin three-field login (email, password, hourly code or recovery key).
/// </summary>
public sealed class PlatformAdminLoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? HourlyCode { get; set; }

    public string? RecoveryKey { get; set; }
}

