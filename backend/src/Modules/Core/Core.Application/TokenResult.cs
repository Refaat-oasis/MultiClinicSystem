namespace Core.Application;

/// <summary>
/// Result model containing generated authentication tokens and their expiration timestamp.
/// </summary>
public sealed class TokenResult
{
    public string AccessToken { get; }
    public string RefreshToken { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public TokenResult(string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        AccessToken = accessToken ?? string.Empty;
        RefreshToken = refreshToken ?? string.Empty;
        ExpiresAtUtc = expiresAtUtc;
    }
}
