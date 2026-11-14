namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to push a late waiting patient back one position in the queue.
/// </summary>
public sealed class PushLateQueueEntryDto
{
    public bool MarkNoShowOnExceed { get; set; } = true;
}

