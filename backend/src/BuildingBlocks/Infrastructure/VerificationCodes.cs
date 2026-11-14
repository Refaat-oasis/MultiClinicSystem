using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Implements HMAC-SHA256 verification codes bound to user ID and purpose.
/// </summary>
public sealed class VerificationCodes : IVerificationCodes
{
    private readonly byte[] _serverSecret;

    public VerificationCodes(string serverSecret)
    {
        if (string.IsNullOrWhiteSpace(serverSecret))
        {
            throw new ArgumentException("Server secret key must not be null or empty.", nameof(serverSecret));
        }

        _serverSecret = Encoding.UTF8.GetBytes(serverSecret);
    }

    public string GenerateCode()
    {
        int number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6", CultureInfo.InvariantCulture);
    }

    public string ComputeHmac(string code, string userId, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        string payload = $"{userId.Trim()}:{purpose.Trim()}:{code.Trim()}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(_serverSecret);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    public bool Verify(string rawCode, string storedHmac, string userId, string purpose)
    {
        if (string.IsNullOrWhiteSpace(rawCode) || string.IsNullOrWhiteSpace(storedHmac))
        {
            return false;
        }

        try
        {
            byte[] expectedHash = Convert.FromHexString(storedHmac.Trim());
            string computedHex = ComputeHmac(rawCode, userId, purpose);
            byte[] actualHash = Convert.FromHexString(computedHex);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
