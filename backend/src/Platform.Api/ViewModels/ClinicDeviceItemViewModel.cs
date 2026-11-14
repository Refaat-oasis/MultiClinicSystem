namespace Platform.Api.ViewModels;

public sealed record ClinicDeviceItemViewModel(
    Guid Id,
    Guid UserId,
    string StaffName,
    string StaffEmail,
    string DeviceId,
    string DeviceName,
    bool IsApproved,
    bool IsRevoked,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset CreatedAtUtc);

