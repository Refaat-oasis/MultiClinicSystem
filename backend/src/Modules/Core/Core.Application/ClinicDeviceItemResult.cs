namespace Core.Application;

/// <summary>
/// Clinic-level view of a staff member's registered device and its approval status.
/// </summary>
public sealed record ClinicDeviceItemResult(
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

