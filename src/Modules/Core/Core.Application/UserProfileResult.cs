namespace Core.Application;

/// <summary>
/// Self-service profile details including user preferences, devices, and clinic memberships.
/// </summary>
public sealed record UserProfileResult(
    Guid UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    Guid? AvatarAssetId,
    string Locale,
    string Theme,
    IReadOnlyList<UserDeviceItemResult> Devices,
    IReadOnlyList<UserClinicMembershipResult> Clinics);

