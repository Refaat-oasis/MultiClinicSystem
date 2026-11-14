using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Application;
using Core.Application;
using Microsoft.AspNetCore.DataProtection;

namespace Core.Infrastructure;

/// <summary>
/// Implements RFC 6238 style hourly TOTP (3600-second period, 6 digits) with 5-minute previous-period grace,
/// replay prevention, DataProtection encryption at rest, and HMAC-hashed single-use recovery keys.
/// </summary>
public sealed class HourlyTotpService : IHourlyTotpService
{
    public const long PeriodSeconds = 3600;
    public const long SkewGraceSeconds = 300; // First 5 minutes of the new hour
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly IDataProtector _protector;
    private readonly IVerificationCodes _verificationCodes;

    public HourlyTotpService(
        IDataProtectionProvider dataProtectionProvider,
        IVerificationCodes verificationCodes)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector("PlatformAdmin.TotpSecret.v1");
        _verificationCodes = verificationCodes ?? throw new ArgumentNullException(nameof(verificationCodes));
    }

    public string GenerateSecretBase32()
    {
        byte[] bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return EncodeBase32(bytes);
    }

    public string ProtectSecret(string base32Secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        return _protector.Protect(base32Secret.Trim());
    }

    public string UnprotectSecret(string encryptedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedSecret);
        return _protector.Unprotect(encryptedSecret);
    }

    public string BuildOtpAuthUri(string email, string base32Secret)
    {
        string escapedLabel = Uri.EscapeDataString($"ClinicPlatformAdmin:{email}");
        string escapedIssuer = Uri.EscapeDataString("ClinicPlatformAdmin");
        return $"otpauth://totp/{escapedLabel}?secret={base32Secret}&issuer={escapedIssuer}&algorithm=SHA1&digits=6&period={PeriodSeconds}";
    }

    public long GetTimeStep(DateTimeOffset timestampUtc)
    {
        return timestampUtc.ToUnixTimeSeconds() / PeriodSeconds;
    }

    public string ComputeCurrentCode(string base32Secret, DateTimeOffset timestampUtc)
    {
        return ComputeCodeForStep(base32Secret, GetTimeStep(timestampUtc));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "RFC 6238 TOTP specifies HMAC-SHA1 for standard authenticator app compatibility.")]
    public string ComputeCodeForStep(string base32Secret, long timeStep)
    {
        byte[] keyBytes = DecodeBase32(base32Secret);
        byte[] counterBytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, timeStep);

        using var hmac = new HMACSHA1(keyBytes);
        byte[] hash = hmac.ComputeHash(counterBytes);

        int offset = hash[^1] & 0x0F;
        int binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    public TotpValidationResult ValidateCode(
        string base32Secret,
        string inputCode,
        DateTimeOffset timestampUtc,
        long? lastAcceptedTimeStep)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(inputCode))
        {
            return new TotpValidationResult(false, null, "INVALID_TOTP_CODE");
        }

        string trimmed = inputCode.Trim();
        if (trimmed.Length != 6)
        {
            return new TotpValidationResult(false, null, "INVALID_TOTP_CODE");
        }

        long unixSeconds = timestampUtc.ToUnixTimeSeconds();
        long currentStep = unixSeconds / PeriodSeconds;
        long secondsIntoCurrentStep = unixSeconds % PeriodSeconds;

        long? matchedStep = null;

        string currentExpected = ComputeCodeForStep(base32Secret, currentStep);
        if (FixedTimeEquals(trimmed, currentExpected))
        {
            matchedStep = currentStep;
        }
        else if (secondsIntoCurrentStep < SkewGraceSeconds && currentStep > 0)
        {
            string previousExpected = ComputeCodeForStep(base32Secret, currentStep - 1);
            if (FixedTimeEquals(trimmed, previousExpected))
            {
                matchedStep = currentStep - 1;
            }
        }

        if (!matchedStep.HasValue)
        {
            return new TotpValidationResult(false, null, "INVALID_TOTP_CODE");
        }

        if (lastAcceptedTimeStep.HasValue && matchedStep.Value <= lastAcceptedTimeStep.Value)
        {
            return new TotpValidationResult(false, null, "CODE_REPLAYED");
        }

        return new TotpValidationResult(true, matchedStep.Value, null);
    }

    public IReadOnlyList<string> GenerateRecoveryKeys(int count = 10)
    {
        var keys = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            byte[] random = new byte[6];
            RandomNumberGenerator.Fill(random);
            string hex = Convert.ToHexString(random);
            keys.Add($"{hex[..4]}-{hex[4..8]}-{hex[8..12]}");
        }

        return keys;
    }

    public string HashRecoveryKey(string rawKey, Guid adminId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawKey);
        string normalized = rawKey.Trim().ToUpperInvariant();
        return _verificationCodes.ComputeHmac(normalized, adminId.ToString(), "PlatformAdminRecovery");
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string EncodeBase32(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = data[0];
        int next = 1;
        int bitsLeft = 8;

        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[next++] & 0xFF;
                    bitsLeft += 8;
                }
                else
                {
                    int pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }

            int index = 0x1F & (buffer >> (bitsLeft - 5));
            bitsLeft -= 5;
            result.Append(Base32Alphabet[index]);
        }

        return result.ToString();
    }

    private static byte[] DecodeBase32(string base32)
    {
        string clean = base32.Trim().TrimEnd('=').ToUpperInvariant();
        if (clean.Length == 0)
        {
            return Array.Empty<byte>();
        }

        int byteCount = clean.Length * 5 / 8;
        byte[] result = new byte[byteCount];
        int buffer = 0;
        int bitsLeft = 0;
        int outIndex = 0;

        foreach (char c in clean)
        {
            int val = Base32Alphabet.IndexOf(c);
            if (val < 0)
            {
                throw new FormatException($"Invalid Base32 character: '{c}'.");
            }

            buffer <<= 5;
            buffer |= val & 0x1F;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                result[outIndex++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                bitsLeft -= 8;
            }
        }

        return result;
    }
}
