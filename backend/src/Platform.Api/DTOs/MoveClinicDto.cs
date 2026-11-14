using System.ComponentModel.DataAnnotations;

namespace Platform.Api.DTOs;

/// <summary>
/// Data transfer object for transferring clinic ownership to another owner.
/// </summary>
public sealed class MoveClinicDto
{
    [Required]
    public Guid TargetOwnerId { get; set; }

    public string? StepUpPassword { get; set; }
}

