using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Core.Infrastructure;

/// <summary>
/// JWT token generation, refresh token management, and cryptographic reuse detection service.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly IClock _clock;
    private readonly string _jwtSecret;
    private readonly string _issuer;
    private readonly string _audience;

    public TokenService(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        IClock clock,
        IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentNullException.ThrowIfNull(configuration);

        _jwtSecret = configuration["Security:JwtSecret"]
                     ?? "clinic-platform-ultra-secure-jwt-secret-signing-key-minimum-32-chars";
        _issuer = configuration["Security:JwtIssuer"] ?? "ClinicPlatform";
        _audience = configuration["Security:JwtAudience"] ?? "ClinicPlatformClients";
    }

    public async Task<Result<TokenResult>> GenerateTokensAsync(
        AppUser user,
        string deviceId,
        Guid? clinicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        // Fetch User Roles
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new("deviceId", deviceId.Trim()),
            new("securityStamp", user.SecurityStamp ?? string.Empty)
        };

        if (clinicId.HasValue)
        {
            claims.Add(new Claim("clinic_id", clinicId.Value.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAtUtc = _clock.UtcNow.AddMinutes(15);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc.UtcDateTime,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        string accessToken = tokenHandler.WriteToken(token);

        // Generate Refresh Token
        string rawRefreshToken = GenerateSecureTokenString();
        string tokenHash = HashToken(rawRefreshToken);
        var familyId = GuidV7.NewGuid();

        var refreshToken = new RefreshToken(
            id: GuidV7.NewGuid(),
            userId: user.Id,
            tokenHash: tokenHash,
            deviceId: deviceId.Trim(),
            expiryUtc: _clock.UtcNow.AddDays(7),
            familyId: familyId,
            createdAtUtc: _clock.UtcNow);

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResult>.Success(new TokenResult(accessToken, rawRefreshToken, expiresAtUtc));
    }

    public async Task<Result<TokenResult>> RefreshTokensAsync(
        string rawRefreshToken,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("REFRESH_TOKEN_REQUIRED", "Refresh token is missing."));
        }

        string tokenHash = HashToken(rawRefreshToken);
        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken == null)
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("INVALID_REFRESH_TOKEN", "The provided refresh token is invalid."));
        }

        // Reuse Detection: If token is already revoked or replaced, compromise suspected
        if (existingToken.IsRevoked || existingToken.ReplacedByTokenId != null)
        {
            // Revoke entire token family
            var familyTokens = await _dbContext.RefreshTokens
                .Where(t => t.FamilyId == existingToken.FamilyId && !t.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var famToken in familyTokens)
            {
                famToken.Revoke(_clock.UtcNow);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<TokenResult>.Failure(Error.Unauthorized(
                "REFRESH_TOKEN_REUSED",
                "Refresh token reuse detected. All sessions in this family have been terminated."));
        }

        if (existingToken.ExpiryUtc <= _clock.UtcNow)
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("REFRESH_TOKEN_EXPIRED", "The refresh token has expired."));
        }

        // Check if device is revoked
        var device = await _dbContext.AppDevices
            .FirstOrDefaultAsync(d => d.UserId == existingToken.UserId && d.DeviceId == deviceId, cancellationToken);

        if (device != null && device.IsRevoked)
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("DEVICE_REVOKED", "This device has been revoked."));
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user == null || !user.IsActive)
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("USER_INACTIVE", "User account is disabled or missing."));
        }

        // Rotate Refresh Token
        string newRawRefreshToken = GenerateSecureTokenString();
        string newTokenHash = HashToken(newRawRefreshToken);

        var newToken = new RefreshToken(
            id: GuidV7.NewGuid(),
            userId: user.Id,
            tokenHash: newTokenHash,
            deviceId: deviceId,
            expiryUtc: _clock.UtcNow.AddDays(7),
            familyId: existingToken.FamilyId,
            createdAtUtc: _clock.UtcNow);

        existingToken.Replace(newToken.Id, _clock.UtcNow);
        _dbContext.RefreshTokens.Add(newToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate new Access Token
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new("deviceId", deviceId.Trim()),
            new("securityStamp", user.SecurityStamp ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = _clock.UtcNow.AddMinutes(15);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc.UtcDateTime,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        string accessToken = tokenHandler.WriteToken(token);

        return Result<TokenResult>.Success(new TokenResult(accessToken, newRawRefreshToken, expiresAtUtc));
    }

    public async Task RevokeTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(_clock.UtcNow);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeDeviceTokensAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.DeviceId == deviceId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(_clock.UtcNow);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateSecureTokenString()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
