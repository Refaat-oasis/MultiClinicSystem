namespace Core.Domain;

/// <summary>
/// Refresh token entity with family-based reuse detection and rotation support.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public string DeviceId { get; private set; }
    public DateTimeOffset ExpiryUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private RefreshToken()
    {
        TokenHash = string.Empty;
        DeviceId = string.Empty;
    }

    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        string deviceId,
        DateTimeOffset expiryUtc,
        Guid familyId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash ?? string.Empty;
        DeviceId = deviceId ?? string.Empty;
        ExpiryUtc = expiryUtc;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        IsRevoked = false;
    }

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && nowUtc < ExpiryUtc && ReplacedByTokenId == null;

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        IsRevoked = true;
        RevokedAtUtc = revokedAtUtc;
    }

    public void Replace(Guid newTokenId, DateTimeOffset nowUtc)
    {
        ReplacedByTokenId = newTokenId;
        IsRevoked = true;
        RevokedAtUtc = nowUtc;
    }
}
