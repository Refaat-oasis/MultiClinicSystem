namespace Core.Application;

/// <summary>
/// Service for RFC 6238 style hourly TOTP generation, encryption at rest, validation with boundary window & replay protection, and recovery keys.
/// </summary>
public interface IHourlyTotpService
{
    string GenerateSecretBase32();
    string ProtectSecret(string base32Secret);
    string UnprotectSecret(string encryptedSecret);
    string BuildOtpAuthUri(string email, string base32Secret);
    long GetTimeStep(DateTimeOffset timestampUtc);
    string ComputeCodeForStep(string base32Secret, long timeStep);
    string ComputeCurrentCode(string base32Secret, DateTimeOffset timestampUtc);
    TotpValidationResult ValidateCode(string base32Secret, string inputCode, DateTimeOffset timestampUtc, long? lastAcceptedTimeStep);
    IReadOnlyList<string> GenerateRecoveryKeys(int count = 10);
    string HashRecoveryKey(string rawKey, Guid adminId);
}

