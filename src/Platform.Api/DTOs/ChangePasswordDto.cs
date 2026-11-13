namespace Platform.Api.DTOs;

public sealed record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword,
    bool SignOutOtherDevices = true,
    string? DeviceId = null);

