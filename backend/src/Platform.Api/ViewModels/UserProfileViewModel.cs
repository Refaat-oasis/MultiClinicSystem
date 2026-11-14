namespace Platform.Api.ViewModels;

public sealed record UserProfileViewModel(
    Guid UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    Guid? AvatarAssetId,
    string Locale,
    string Theme,
    IReadOnlyList<UserDeviceItemViewModel> Devices,
    IReadOnlyList<UserClinicMembershipViewModel> Clinics);

