using BuildingBlocks.Domain;
using Core.Domain;

namespace Core.Application;

/// <summary>
/// Service responsible for minting JWT access tokens, managing refresh tokens, and detecting token reuse.
/// </summary>
public interface ITokenService
{
    Task<Result<TokenResult>> GenerateTokensAsync(
        AppUser user,
        string deviceId,
        Guid? clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<TokenResult>> RefreshTokensAsync(
        string rawRefreshToken,
        string deviceId,
        CancellationToken cancellationToken = default);

    Task RevokeTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RevokeDeviceTokensAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);
}
