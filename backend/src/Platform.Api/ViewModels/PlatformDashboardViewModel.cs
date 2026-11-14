namespace Platform.Api.ViewModels;

/// <summary>
/// Platform Super Admin dashboard aggregate metrics view model.
/// </summary>
public sealed record PlatformDashboardViewModel(
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
    IReadOnlyList<AuditLogItemViewModel> RecentAuditEntries);

