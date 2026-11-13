using System.Security.Cryptography;
using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Isolated portal principal for a clinic patient (or minor's guardian).
/// </summary>
public sealed class PatientAccount : ITenantEntity, ISoftDeletable
{
    private const int MaxPrintedPinAttempts = 5;
    private const int MaxFailedLoginAttempts = 5;

    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid PatientId { get; private set; }
    public string? Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public bool IsEmailConfirmed { get; private set; }
    public string? PasswordHash { get; private set; }
    public string SecurityStamp { get; private set; }
    public PatientAccountStatus Status { get; private set; }
    public int FailedAccessCount { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public string? PrintedCodeHmac { get; private set; }
    public DateTimeOffset? PrintedCodeExpiresAtUtc { get; private set; }
    public int PrintedCodeAttempts { get; private set; }
    public bool IsGuardianAccount { get; private set; }
    public string? GuardianName { get; private set; }
    public int NoShowCount { get; private set; }
    public int NoShowsSinceUnblock { get; private set; }
    public bool IsOnlineBookingBlocked { get; private set; }
    public string? OnlineBookingBlockReason { get; private set; }
    public DateTimeOffset? OnlineBookingBlockedAtUtc { get; private set; }
    public Guid? OnlineBookingBlockedByUserId { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PatientAccount()
    {
        NormalizedEmail = string.Empty;
        SecurityStamp = string.Empty;
    }

    public PatientAccount(
        Guid id,
        Guid clinicId,
        Guid patientId,
        string? email,
        bool isGuardianAccount,
        string? guardianName,
        DateTimeOffset createdAtUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("Patient ID cannot be empty.", nameof(patientId));
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        PatientId = patientId;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        NormalizedEmail = Email?.ToUpperInvariant() ?? string.Empty;
        IsEmailConfirmed = false;
        SecurityStamp = GenerateStamp();
        Status = PatientAccountStatus.Invited;
        IsGuardianAccount = isGuardianAccount;
        GuardianName = isGuardianAccount ? guardianName?.Trim() : null;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static string GeneratePrintedActivationPin()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, 10);
    }

    public void UpdateContactAndGuardian(string? email, bool isGuardianAccount, string? guardianName, DateTimeOffset nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            string trimmed = email.Trim();
            if (!string.Equals(NormalizedEmail, trimmed.ToUpperInvariant(), StringComparison.Ordinal))
            {
                Email = trimmed;
                NormalizedEmail = trimmed.ToUpperInvariant();
                IsEmailConfirmed = false;
                SecurityStamp = GenerateStamp();
            }
        }

        IsGuardianAccount = isGuardianAccount;
        GuardianName = isGuardianAccount ? guardianName?.Trim() : null;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void IssuePrintedActivationCode(string codeHmac, DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHmac);
        PrintedCodeHmac = codeHmac;
        PrintedCodeExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        PrintedCodeAttempts = 0;
        if (Status == PatientAccountStatus.Disabled)
        {
            Status = PatientAccountStatus.Invited;
        }
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public bool CanAttemptPrintedPin(DateTimeOffset nowUtc)
    {
        return !string.IsNullOrWhiteSpace(PrintedCodeHmac)
               && PrintedCodeExpiresAtUtc.HasValue
               && nowUtc.ToUniversalTime() <= PrintedCodeExpiresAtUtc.Value
               && PrintedCodeAttempts < MaxPrintedPinAttempts
               && Status != PatientAccountStatus.Disabled;
    }

    public void RecordPrintedPinFailure(DateTimeOffset nowUtc)
    {
        PrintedCodeAttempts++;
        if (PrintedCodeAttempts >= MaxPrintedPinAttempts)
        {
            PrintedCodeHmac = null;
        }
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void ConsumePrintedPinAndSetPendingEmail(string email, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        IsEmailConfirmed = false;
        PrintedCodeHmac = null;
        PrintedCodeExpiresAtUtc = null;
        PrintedCodeAttempts = 0;
        Status = PatientAccountStatus.PendingEmailConfirmation;
        SecurityStamp = GenerateStamp();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void ActivateWithPassword(string passwordHash, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        IsEmailConfirmed = true;
        PrintedCodeHmac = null;
        PrintedCodeExpiresAtUtc = null;
        PrintedCodeAttempts = 0;
        Status = PatientAccountStatus.Active;
        FailedAccessCount = 0;
        LockoutEndUtc = null;
        SecurityStamp = GenerateStamp();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void ResetPassword(string passwordHash, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        IsEmailConfirmed = true;
        FailedAccessCount = 0;
        LockoutEndUtc = null;
        if (Status == PatientAccountStatus.Invited || Status == PatientAccountStatus.PendingEmailConfirmation)
        {
            Status = PatientAccountStatus.Active;
        }
        SecurityStamp = GenerateStamp();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public bool IsLockedOut(DateTimeOffset nowUtc)
    {
        return LockoutEndUtc.HasValue && nowUtc.ToUniversalTime() < LockoutEndUtc.Value;
    }

    public void RecordLoginSuccess(DateTimeOffset nowUtc)
    {
        DateTimeOffset utc = nowUtc.ToUniversalTime();
        FailedAccessCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = utc;
        UpdatedAtUtc = utc;
    }

    public void RecordLoginFailure(DateTimeOffset nowUtc)
    {
        DateTimeOffset utc = nowUtc.ToUniversalTime();
        FailedAccessCount++;
        if (FailedAccessCount >= MaxFailedLoginAttempts)
        {
            LockoutEndUtc = utc.AddMinutes(15);
        }
        UpdatedAtUtc = utc;
    }

    public void Disable(DateTimeOffset nowUtc)
    {
        Status = PatientAccountStatus.Disabled;
        SecurityStamp = GenerateStamp();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void Enable(DateTimeOffset nowUtc)
    {
        Status = string.IsNullOrWhiteSpace(PasswordHash)
            ? PatientAccountStatus.Invited
            : PatientAccountStatus.Active;
        FailedAccessCount = 0;
        LockoutEndUtc = null;
        SecurityStamp = GenerateStamp();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void RecordNoShow(int autoBlockThreshold, DateTimeOffset nowUtc)
    {
        DateTimeOffset utc = nowUtc.ToUniversalTime();
        NoShowCount++;
        NoShowsSinceUnblock++;
        if (autoBlockThreshold > 0 && NoShowsSinceUnblock >= autoBlockThreshold)
        {
            IsOnlineBookingBlocked = true;
            OnlineBookingBlockReason = "AutoBlockedAfterNoShows";
            OnlineBookingBlockedAtUtc = utc;
            OnlineBookingBlockedByUserId = null;
        }

        UpdatedAtUtc = utc;
    }

    public void BlockOnlineBooking(Guid? blockedByUserId, string? reason, DateTimeOffset nowUtc)
    {
        DateTimeOffset utc = nowUtc.ToUniversalTime();
        IsOnlineBookingBlocked = true;
        OnlineBookingBlockReason = string.IsNullOrWhiteSpace(reason) ? "BlockedByStaff" : reason.Trim();
        OnlineBookingBlockedAtUtc = utc;
        OnlineBookingBlockedByUserId = blockedByUserId == Guid.Empty ? null : blockedByUserId;
        UpdatedAtUtc = utc;
    }

    public void UnblockOnlineBooking(DateTimeOffset nowUtc)
    {
        DateTimeOffset utc = nowUtc.ToUniversalTime();
        IsOnlineBookingBlocked = false;
        NoShowsSinceUnblock = 0;
        OnlineBookingBlockReason = null;
        OnlineBookingBlockedAtUtc = null;
        OnlineBookingBlockedByUserId = null;
        UpdatedAtUtc = utc;
    }

    private static string GenerateStamp()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }
}

