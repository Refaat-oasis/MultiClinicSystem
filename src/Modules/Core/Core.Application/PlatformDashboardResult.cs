namespace Core.Application;

/// <summary>
/// Platform-wide dashboard metrics for Super Admin monitoring.
/// </summary>
public sealed record PlatformDashboardResult(
    int TotalOwners,
    int ActiveOwners,
    int TotalClinics,
    int ActiveClinics,
    int PendingOnboardingClinics,
    int ReadOnlyClinics,
    int SuspendedClinics,
    int ClinicsAwaitingModuleSetup,
    int ExpiringSubscriptionsCount,
    int FailedLoginsLast24Hours,
    int PendingOutboxMessages,
    IReadOnlyList<AuditLogItemResult> RecentAuditEntries);

