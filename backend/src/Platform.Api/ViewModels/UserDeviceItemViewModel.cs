namespace Platform.Api.ViewModels;

public sealed record UserDeviceItemViewModel(
    Guid Id,
    string DeviceId,
    string DeviceName,
    bool IsApproved,
    bool IsRevoked,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc);

