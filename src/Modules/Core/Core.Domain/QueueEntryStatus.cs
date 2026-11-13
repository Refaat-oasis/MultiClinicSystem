namespace Core.Domain;

/// <summary>
/// Status of a live waiting-queue entry for a clinic date.
/// </summary>
public enum QueueEntryStatus
{
    Waiting = 0,
    InSession = 1,
    Served = 2,
    Late = 3,
    NoShow = 4,
    Removed = 5
}

