using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for inviting a staff member or clinic owner.
/// </summary>
public sealed class InviteUserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Staff";

    public Guid? ClinicId { get; set; }
}
