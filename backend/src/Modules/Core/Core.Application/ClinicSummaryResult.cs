namespace Core.Application;

/// <summary>
/// Summary of a clinic belonging to an owner.
/// </summary>
public sealed record ClinicSummaryResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    DateTimeOffset? SubscriptionExpiryUtc,
    int GraceDays,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> ModulesEnabled);

