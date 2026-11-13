namespace Core.Domain;

public enum SyncConflictStatus
{
    Open = 1,
    ResolvedKeepServer = 2,
    ResolvedApplyClient = 3,
    Dismissed = 4
}

