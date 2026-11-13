namespace Core.Domain;

public enum SyncOperationStatus
{
    Applied = 1,
    DuplicateIgnored = 2,
    Conflict = 3,
    Rejected = 4
}

