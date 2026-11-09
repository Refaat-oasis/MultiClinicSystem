using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Business service interface handling user authentication, invitation activations, password recovery, and device sessions.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResult>> LoginAsync(
        string email,
        string password,
        string deviceId,
        string deviceName,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResult>> ActivateAccountAsync(
        string email,
        string code,
        string password,
        string deviceId,
        string deviceName,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ForgotPasswordAsync(
        string email,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ResendCodeAsync(
        string email,
        string purpose,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResult>> RefreshTokenAsync(
        string rawRefreshToken,
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<Result> RequestEmailChangeAsync(
        Guid userId,
        string newEmail,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmailChangeAsync(
        Guid userId,
        string code,
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
