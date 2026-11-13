namespace Platform.Api.ViewModels;

/// <summary>
/// Read-only clinic inspection snapshot view model.
/// </summary>
public sealed record InspectClinicViewModel(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    Guid? OwnerId,
    DateTimeOffset? SubscriptionExpiryUtc,
    int GraceDays,
    int SampleRecordsCount,
    decimal SampleRecordsTotalAmount,
    string Currency,
    bool ReadOnlyMode);

