using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for updating a clinic's lifecycle status and subscription dates.
/// </summary>
public sealed class UpdateClinicStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? SubscriptionExpiryUtc { get; set; }

    public int? GraceDays { get; set; }
}

