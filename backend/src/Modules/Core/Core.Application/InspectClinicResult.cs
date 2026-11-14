using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Read-only clinic inspection snapshot accessed through IPlatformAccess.
/// </summary>
public sealed record InspectClinicResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    Guid? OwnerId,
    DateTimeOffset? SubscriptionExpiryUtc,
    int GraceDays,
    int SampleRecordsCount,
    Money SampleRecordsTotalAmount,
    bool ReadOnlyMode);

