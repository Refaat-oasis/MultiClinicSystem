using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for revoking a client device session.
/// </summary>
public sealed class RevokeDeviceDto
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;
}
