namespace Core.Application;

/// <summary>
/// Combined result model representing a successful authentication event.
/// </summary>
public sealed class AuthResult
{
    public TokenResult Tokens { get; }
    public UserInfoResult User { get; }

    public AuthResult(TokenResult tokens, UserInfoResult user)
    {
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        User = user ?? throw new ArgumentNullException(nameof(user));
    }
}
