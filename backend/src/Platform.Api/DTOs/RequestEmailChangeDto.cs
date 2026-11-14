using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for requesting an email address modification.
/// </summary>
public sealed class RequestEmailChangeDto
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}
