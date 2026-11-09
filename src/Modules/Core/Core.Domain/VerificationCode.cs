namespace Core.Domain;

/// <summary>
/// Cryptographic 6-digit verification code record storing HMAC hash, expiration, and rate-limiting attempt counters.
/// </summary>
public sealed class VerificationCode
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Realm { get; private set; }
    public string Purpose { get; private set; }
    public string CodeHash { get; private set; }
    public DateTimeOffset ExpiryUtc { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }
    public string? Payload { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private VerificationCode()
    {
        Realm = string.Empty;
        Purpose = string.Empty;
        CodeHash = string.Empty;
    }

    public VerificationCode(
        Guid id,
        Guid userId,
        string realm,
        string purpose,
        string codeHash,
        DateTimeOffset expiryUtc,
        DateTimeOffset createdAtUtc,
        int maxAttempts = 5,
        string? payload = null)
    {
        Id = id;
        UserId = userId;
        Realm = realm ?? "Staff";
        Purpose = purpose ?? string.Empty;
        CodeHash = codeHash ?? string.Empty;
        ExpiryUtc = expiryUtc;
        Attempts = 0;
        MaxAttempts = maxAttempts > 0 ? maxAttempts : 5;
        CreatedAtUtc = createdAtUtc;
        Payload = payload;
    }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc > ExpiryUtc;

    public bool IsDead() => Attempts >= MaxAttempts || UsedAtUtc.HasValue;

    public void RecordFailedAttempt()
    {
        Attempts++;
    }

    public void MarkUsed(DateTimeOffset nowUtc)
    {
        UsedAtUtc = nowUtc;
    }
}
