namespace BuildingBlocks.Application;

/// <summary>
/// Service contract for generating and verifying cryptographic 6-digit verification codes.
/// Uses HMAC-SHA256 with a server-side secret key bound to user ID and purpose.
/// </summary>
public interface IVerificationCodes
{
    /// <summary>
    /// Generates a cryptographically random 6-digit verification code string.
    /// </summary>
    string GenerateCode();

    /// <summary>
    /// Computes the HMAC-SHA256 hash for a given 6-digit code, bound to user ID and purpose.
    /// </summary>
    string ComputeHmac(string code, string userId, string purpose);

    /// <summary>
    /// Verifies a provided code against a stored HMAC hash using fixed-time comparison.
    /// </summary>
    bool Verify(string rawCode, string storedHmac, string userId, string purpose);
}

