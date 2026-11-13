using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Self-service user profile operations governed by policy profile.self.
/// </summary>
public interface IProfileService
{
    Task<Result<UserProfileResult>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<UserProfileResult>> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string? phoneNumber,
        string? locale,
        string? theme,
        Guid? avatarAssetId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> RequestEmailChangeAsync(
        Guid userId,
        string newEmail,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResult>> ConfirmEmailChangeAsync(
        Guid userId,
        string code,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResult>> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        bool signOutOtherDevices,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeDeviceAsync(
        Guid userId,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);
}

