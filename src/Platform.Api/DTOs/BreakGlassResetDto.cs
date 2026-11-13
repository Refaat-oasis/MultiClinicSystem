using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for server-authorized break-glass 2FA reset.
/// </summary>
public sealed class BreakGlassResetDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string BreakGlassSecret { get; set; } = string.Empty;
}

