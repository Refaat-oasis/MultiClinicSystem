using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Core.Domain;

/// <summary>
/// Generates and verifies short-lived HMAC-SHA256 tokens for patient portal signed media/PDF downloads.
/// </summary>
public static class PortalSignedTokenHelper
{
    public static string GenerateToken(
        string secret,
        Guid clinicId,
        Guid patientId,
        string resourceKind,
        Guid resourceId,
        long expiresAtUnixSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKind);

        string payload = BuildPayload(clinicId, patientId, resourceKind, resourceId, expiresAtUnixSeconds);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        string encodedSig = Base64UrlEncode(sig);
        return $"{expiresAtUnixSeconds.ToString(CultureInfo.InvariantCulture)}.{encodedSig}";
    }

    public static bool ValidateToken(
        string secret,
        string token,
        Guid clinicId,
        Guid patientId,
        string resourceKind,
        Guid resourceId,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(resourceKind))
        {
            return false;
        }

        string[] parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiresUnix))
        {
            return false;
        }

        if (nowUtc.ToUnixTimeSeconds() > expiresUnix)
        {
            return false;
        }

        string payload = BuildPayload(clinicId, patientId, resourceKind, resourceId, expiresUnix);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        byte[] providedSig = Base64UrlDecode(parts[1]);
        if (providedSig.Length != expectedSig.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedSig, providedSig);
    }

    private static string BuildPayload(Guid clinicId, Guid patientId, string resourceKind, Guid resourceId, long expiresAtUnixSeconds)
        => $"portal-dl|{clinicId:D}|{patientId:D}|{resourceKind.Trim().ToLowerInvariant()}|{resourceId:D}|{expiresAtUnixSeconds.ToString(CultureInfo.InvariantCulture)}";

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        try
        {
            string padded = input.Replace('-', '+').Replace('_', '/');
            int mod = padded.Length % 4;
            if (mod == 2)
            {
                padded += "==";
            }
            else if (mod == 3)
            {
                padded += "=";
            }
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }
}
