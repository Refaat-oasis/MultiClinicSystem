using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for rotating refresh tokens.
/// </summary>
public sealed class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    [Required]
    public string DeviceId { get; set; } = string.Empty;
}
