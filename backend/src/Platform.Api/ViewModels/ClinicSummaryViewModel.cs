namespace Platform.Api.ViewModels;

/// <summary>
/// Clinic summary presentation model for the Admin Portal.
/// </summary>
public sealed record ClinicSummaryViewModel(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    DateTimeOffset? SubscriptionExpiryUtc,
    int GraceDays,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> ModulesEnabled);

