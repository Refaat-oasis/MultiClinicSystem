using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for forced Super Admin password change and hourly TOTP enrollment.
/// </summary>
public sealed class PlatformAdminEnrollDto
{
    [Required]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string HourlyCode { get; set; } = string.Empty;
}

