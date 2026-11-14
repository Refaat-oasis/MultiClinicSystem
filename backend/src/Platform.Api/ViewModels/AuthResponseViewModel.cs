namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation model returned upon successful authentication or token issuance.
/// </summary>
public sealed class AuthResponseViewModel
{
    public string AccessToken { get; }
    public string RefreshToken { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public UserViewModel User { get; }

    public AuthResponseViewModel(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAtUtc,
        UserViewModel user)
    {
        AccessToken = accessToken ?? string.Empty;
        RefreshToken = refreshToken ?? string.Empty;
        ExpiresAtUtc = expiresAtUtc;
        User = user ?? throw new ArgumentNullException(nameof(user));
    }
}
