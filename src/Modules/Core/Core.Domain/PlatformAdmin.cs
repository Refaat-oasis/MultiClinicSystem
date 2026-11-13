namespace Core.Domain;

/// <summary>
/// Separate Super Admin identity store for the Platform Admin host, isolated from Clinic Identity users.
/// Enforces hourly (3600s) TOTP verification, forced enrollment, single-use recovery keys, and step-up tracking.
/// </summary>
public sealed class PlatformAdmin
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string FullName { get; private set; }
    public string PasswordHash { get; private set; }
    public bool MustChangePassword { get; private set; }
    public string? EncryptedTotpSecret { get; private set; }
    public bool TotpEnrolled { get; private set; }
    public long? LastAcceptedTimeStep { get; private set; }
    public string RecoveryCodeHashesJson { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset? LastLoginUtc { get; private set; }
    public DateTimeOffset? LastStepUpAtUtc { get; private set; }
    public string SecurityStamp { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private PlatformAdmin()
    {
        Email = string.Empty;
        FullName = string.Empty;
        PasswordHash = string.Empty;
        RecoveryCodeHashesJson = "[]";
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public PlatformAdmin(
        Guid id,
        string email,
        string fullName,
        string passwordHash,
        DateTimeOffset createdAtUtc,
        bool mustChangePassword = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("PlatformAdmin ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        }

        Id = id;
        Email = email.Trim().ToLowerInvariant();
        FullName = string.IsNullOrWhiteSpace(fullName) ? "Platform Admin" : fullName.Trim();
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        MustChangePassword = mustChangePassword;
        TotpEnrolled = false;
        RecoveryCodeHashesJson = "[]";
        SecurityStamp = Guid.NewGuid().ToString("N");
        CreatedAtUtc = createdAtUtc;
    }

    public void UpdatePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));
        }

        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void SetPendingTotpSecret(string encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new ArgumentException("Encrypted TOTP secret cannot be empty.", nameof(encryptedSecret));
        }

        EncryptedTotpSecret = encryptedSecret;
    }

    public void CompleteEnrollment(
        string newPasswordHash,
        long acceptedTimeStep,
        string recoveryCodeHashesJson,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));
        }

        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        TotpEnrolled = true;
        LastAcceptedTimeStep = acceptedTimeStep;
        RecoveryCodeHashesJson = string.IsNullOrWhiteSpace(recoveryCodeHashesJson) ? "[]" : recoveryCodeHashesJson;
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
        LastStepUpAtUtc = nowUtc;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void RecordSuccessfulLogin(long? acceptedTimeStep, DateTimeOffset nowUtc)
    {
        if (acceptedTimeStep.HasValue)
        {
            LastAcceptedTimeStep = acceptedTimeStep.Value;
        }

        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
    }

    public void UpdateRecoveryCodeHashes(string recoveryCodeHashesJson)
    {
        RecoveryCodeHashesJson = string.IsNullOrWhiteSpace(recoveryCodeHashesJson) ? "[]" : recoveryCodeHashesJson;
    }

    public void RecordFailedLogin(DateTimeOffset nowUtc, int maxAttempts = 5, int lockoutMinutes = 15)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutEndUtc = nowUtc.AddMinutes(lockoutMinutes);
        }
    }

    public bool IsLockedOut(DateTimeOffset nowUtc)
    {
        return LockoutEndUtc.HasValue && LockoutEndUtc.Value > nowUtc;
    }

    public void RecordStepUp(DateTimeOffset nowUtc)
    {
        LastStepUpAtUtc = nowUtc;
    }

    public bool HasValidStepUp(DateTimeOffset nowUtc, TimeSpan window)
    {
        return LastStepUpAtUtc.HasValue && (nowUtc - LastStepUpAtUtc.Value) <= window;
    }

    public void ResetTwoFactorBreakGlass(string newEncryptedSecret, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(newEncryptedSecret))
        {
            throw new ArgumentException("Encrypted TOTP secret cannot be empty.", nameof(newEncryptedSecret));
        }

        EncryptedTotpSecret = newEncryptedSecret;
        TotpEnrolled = false;
        MustChangePassword = true;
        LastAcceptedTimeStep = null;
        RecoveryCodeHashesJson = "[]";
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        LastStepUpAtUtc = null;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }
}

