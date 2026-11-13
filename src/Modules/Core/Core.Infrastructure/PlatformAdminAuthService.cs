using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Core.Infrastructure;

/// <summary>
/// Authentication, forced hourly-TOTP enrollment, recovery key verification, step-up, and break-glass 2FA reset for PlatformAdmins.
/// </summary>
public sealed class PlatformAdminAuthService : IPlatformAdminAuthService
{
    private readonly CoreDbContext _dbContext;
    private readonly IHourlyTotpService _totpService;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IClock _clock;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<PlatformAdmin> _passwordHasher = new();

    public PlatformAdminAuthService(
        CoreDbContext dbContext,
        IHourlyTotpService totpService,
        IPasswordPolicy passwordPolicy,
        IClock clock,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.PlatformAdmins.AnyAsync(cancellationToken))
        {
            return;
        }

        string seedEmail = (_configuration["PlatformAdmin:SeedEmail"]
                            ?? Environment.GetEnvironmentVariable("PLATFORM_ADMIN_SEED_EMAIL")
                            ?? "ahmed@platform.local").Trim().ToLowerInvariant();
        string seedPassword = _configuration["PlatformAdmin:SeedPassword"]
                              ?? Environment.GetEnvironmentVariable("PLATFORM_ADMIN_SEED_PASSWORD")
                              ?? "AdminInitial#2026";
        string seedName = _configuration["PlatformAdmin:SeedName"] ?? "Ahmed (Platform Super Admin)";

        var admin = new PlatformAdmin(
            GuidV7.NewGuid(),
            seedEmail,
            seedName,
            "placeholder",
            _clock.UtcNow,
            mustChangePassword: true);

        string passwordHash = _passwordHasher.HashPassword(admin, seedPassword);
        admin.UpdatePasswordHash(passwordHash);

        // Preserve MustChangePassword = true after hashing initial seed password
        var adminRebuilt = new PlatformAdmin(
            admin.Id,
            seedEmail,
            seedName,
            passwordHash,
            _clock.UtcNow,
            mustChangePassword: true);

        string secret = _totpService.GenerateSecretBase32();
        adminRebuilt.SetPendingTotpSecret(_totpService.ProtectSecret(secret));

        _dbContext.PlatformAdmins.Add(adminRebuilt);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<PlatformAdminLoginResult>> LoginAsync(
        string email,
        string password,
        string? hourlyCode,
        string? recoveryKey,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Result<PlatformAdminLoginResult>.Failure(
                Error.Unauthorized("INVALID_CREDENTIALS", "Invalid credentials."));
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var admin = await _dbContext.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);

        if (admin == null)
        {
            return Result<PlatformAdminLoginResult>.Failure(
                Error.Unauthorized("INVALID_CREDENTIALS", "Invalid credentials."));
        }

        if (admin.IsLockedOut(_clock.UtcNow))
        {
            return Result<PlatformAdminLoginResult>.Failure(
                Error.Unauthorized("ACCOUNT_LOCKED", "Account is locked due to repeated failed attempts."));
        }

        var pwResult = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        if (pwResult == PasswordVerificationResult.Failed)
        {
            admin.RecordFailedLogin(_clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                action: "PlatformAdminLoginFailed",
                entityName: "PlatformAdmin",
                entityId: admin.Id.ToString(),
                oldValues: null,
                newValues: null,
                userId: admin.Id,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);

            return Result<PlatformAdminLoginResult>.Failure(
                Error.Unauthorized("INVALID_CREDENTIALS", "Invalid credentials."));
        }

        // Forced enrollment check
        if (!admin.TotpEnrolled || admin.MustChangePassword)
        {
            string rawSecret;
            if (string.IsNullOrWhiteSpace(admin.EncryptedTotpSecret))
            {
                rawSecret = _totpService.GenerateSecretBase32();
                admin.SetPendingTotpSecret(_totpService.ProtectSecret(rawSecret));
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                rawSecret = _totpService.UnprotectSecret(admin.EncryptedTotpSecret);
            }

            var (enrollmentToken, enrollmentExpiry) = GenerateAdminJwt(admin, totpEnrolled: false, lifetime: TimeSpan.FromMinutes(20));

            await _auditService.LogAsync(
                action: "PlatformAdminEnrollmentStarted",
                entityName: "PlatformAdmin",
                entityId: admin.Id.ToString(),
                oldValues: null,
                newValues: null,
                userId: admin.Id,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);

            return Result<PlatformAdminLoginResult>.Success(new PlatformAdminLoginResult(
                AdminId: admin.Id,
                Email: admin.Email,
                FullName: admin.FullName,
                AccessToken: enrollmentToken,
                ExpiresAtUtc: enrollmentExpiry,
                RequiresEnrollment: true,
                MustChangePassword: admin.MustChangePassword,
                ManualSetupKey: rawSecret,
                OtpAuthUri: _totpService.BuildOtpAuthUri(admin.Email, rawSecret)));
        }

        // Enrolled admin: must supply either hourlyCode or recoveryKey
        if (string.IsNullOrWhiteSpace(hourlyCode) && string.IsNullOrWhiteSpace(recoveryKey))
        {
            return Result<PlatformAdminLoginResult>.Failure(
                Error.Unauthorized("TOTP_CODE_REQUIRED", "Hourly verification code is required."));
        }

        long? acceptedTimeStep = null;

        if (!string.IsNullOrWhiteSpace(hourlyCode))
        {
            if (string.IsNullOrWhiteSpace(admin.EncryptedTotpSecret))
            {
                return Result<PlatformAdminLoginResult>.Failure(
                    Error.Unauthorized("INVALID_CREDENTIALS", "Invalid credentials."));
            }

            string rawSecret = _totpService.UnprotectSecret(admin.EncryptedTotpSecret);
            var totpValidation = _totpService.ValidateCode(rawSecret, hourlyCode, _clock.UtcNow, admin.LastAcceptedTimeStep);

            if (!totpValidation.IsValid)
            {
                if (string.Equals(totpValidation.FailureCode, "CODE_REPLAYED", StringComparison.Ordinal))
                {
                    await _auditService.LogAsync(
                        action: "PlatformAdminTotpReplayRefused",
                        entityName: "PlatformAdmin",
                        entityId: admin.Id.ToString(),
                        oldValues: null,
                        newValues: null,
                        userId: admin.Id,
                        ipAddress: ipAddress,
                        requestId: requestId,
                        cancellationToken: cancellationToken);

                    return Result<PlatformAdminLoginResult>.Failure(
                        Error.Unauthorized("CODE_REPLAYED", "This hourly verification code has already been used."));
                }

                admin.RecordFailedLogin(_clock.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);

                await _auditService.LogAsync(
                    action: "PlatformAdminLoginFailed",
                    entityName: "PlatformAdmin",
                    entityId: admin.Id.ToString(),
                    oldValues: null,
                    newValues: null,
                    userId: admin.Id,
                    ipAddress: ipAddress,
                    requestId: requestId,
                    cancellationToken: cancellationToken);

                return Result<PlatformAdminLoginResult>.Failure(
                    Error.Unauthorized("INVALID_CREDENTIALS", "Invalid credentials."));
            }

            acceptedTimeStep = totpValidation.AcceptedTimeStep;
        }
        else if (!string.IsNullOrWhiteSpace(recoveryKey))
        {
            string candidateHash = _totpService.HashRecoveryKey(recoveryKey, admin.Id);
            var hashes = JsonSerializer.Deserialize<List<string>>(admin.RecoveryCodeHashesJson) ?? new List<string>();
            int matchIndex = hashes.FindIndex(h => string.Equals(h, candidateHash, StringComparison.Ordinal));
            if (matchIndex < 0)
            {
                admin.RecordFailedLogin(_clock.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result<PlatformAdminLoginResult>.Failure(
                    Error.Unauthorized("INVALID_CREDENTIALS", "Invalid credentials."));
            }

            hashes.RemoveAt(matchIndex);
            admin.UpdateRecoveryCodeHashes(JsonSerializer.Serialize(hashes));

            await _auditService.LogAsync(
                action: "PlatformAdminRecoveryKeyUsed",
                entityName: "PlatformAdmin",
                entityId: admin.Id.ToString(),
                oldValues: null,
                newValues: JsonSerializer.Serialize(new { remainingKeys = hashes.Count }),
                userId: admin.Id,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        admin.RecordSuccessfulLogin(acceptedTimeStep, _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var (fullToken, fullExpiry) = GenerateAdminJwt(admin, totpEnrolled: true, lifetime: TimeSpan.FromHours(8));

        await _auditService.LogAsync(
            action: "PlatformAdminLoggedIn",
            entityName: "PlatformAdmin",
            entityId: admin.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { acceptedTimeStep }),
            userId: admin.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<PlatformAdminLoginResult>.Success(new PlatformAdminLoginResult(
            AdminId: admin.Id,
            Email: admin.Email,
            FullName: admin.FullName,
            AccessToken: fullToken,
            ExpiresAtUtc: fullExpiry,
            RequiresEnrollment: false,
            MustChangePassword: false,
            ManualSetupKey: null,
            OtpAuthUri: null));
    }

    public async Task<Result<PlatformAdminEnrollmentResult>> CompleteEnrollmentAsync(
        Guid adminId,
        string newPassword,
        string hourlyCode,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var admin = await _dbContext.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Id == adminId, cancellationToken);

        if (admin == null)
        {
            return Result<PlatformAdminEnrollmentResult>.Failure(
                Error.NotFound("ADMIN_NOT_FOUND", "Platform admin not found."));
        }

        var passwordValidation = _passwordPolicy.Validate(newPassword, admin.Email);
        if (!passwordValidation.IsSuccess)
        {
            return Result<PlatformAdminEnrollmentResult>.Failure(passwordValidation.Error);
        }

        if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
        {
            return Result<PlatformAdminEnrollmentResult>.Failure(
                Error.Validation("PASSWORD_UNCHANGED", "New password must differ from the current password."));
        }

        if (string.IsNullOrWhiteSpace(admin.EncryptedTotpSecret))
        {
            return Result<PlatformAdminEnrollmentResult>.Failure(
                Error.Validation("TOTP_NOT_INITIALIZED", "TOTP secret is not initialized."));
        }

        string rawSecret = _totpService.UnprotectSecret(admin.EncryptedTotpSecret);
        var totpValidation = _totpService.ValidateCode(rawSecret, hourlyCode, _clock.UtcNow, lastAcceptedTimeStep: null);
        if (!totpValidation.IsValid || !totpValidation.AcceptedTimeStep.HasValue)
        {
            return Result<PlatformAdminEnrollmentResult>.Failure(
                Error.Unauthorized("INVALID_TOTP_CODE", "The hourly verification code is invalid. Ensure your authenticator uses a 3600-second period."));
        }

        var rawRecoveryKeys = _totpService.GenerateRecoveryKeys(10);
        var hashedKeys = rawRecoveryKeys.Select(k => _totpService.HashRecoveryKey(k, admin.Id)).ToList();
        string newHash = _passwordHasher.HashPassword(admin, newPassword);

        admin.CompleteEnrollment(
            newPasswordHash: newHash,
            acceptedTimeStep: totpValidation.AcceptedTimeStep.Value,
            recoveryCodeHashesJson: JsonSerializer.Serialize(hashedKeys),
            nowUtc: _clock.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var (fullToken, fullExpiry) = GenerateAdminJwt(admin, totpEnrolled: true, lifetime: TimeSpan.FromHours(8));

        await _auditService.LogAsync(
            action: "PlatformAdminEnrollmentCompleted",
            entityName: "PlatformAdmin",
            entityId: admin.Id.ToString(),
            oldValues: null,
            newValues: null,
            userId: admin.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<PlatformAdminEnrollmentResult>.Success(new PlatformAdminEnrollmentResult(
            AdminId: admin.Id,
            Email: admin.Email,
            AccessToken: fullToken,
            ExpiresAtUtc: fullExpiry,
            RecoveryKeys: rawRecoveryKeys));
    }

    public async Task<Result> VerifyStepUpAsync(
        Guid adminId,
        string password,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure(Error.Unauthorized("STEP_UP_INVALID", "Password is required for step-up verification."));
        }

        var admin = await _dbContext.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Id == adminId, cancellationToken);

        if (admin == null)
        {
            return Result.Failure(Error.NotFound("ADMIN_NOT_FOUND", "Platform admin not found."));
        }

        if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            return Result.Failure(Error.Unauthorized("STEP_UP_INVALID", "Invalid password for step-up verification."));
        }

        admin.RecordStepUp(_clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PlatformAdminStepUpVerified",
            entityName: "PlatformAdmin",
            entityId: admin.Id.ToString(),
            oldValues: null,
            newValues: null,
            userId: admin.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetTwoFactorBreakGlassAsync(
        string email,
        string breakGlassSecret,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        string expectedSecret = _configuration["PlatformAdmin:BreakGlassSecret"]
                                ?? Environment.GetEnvironmentVariable("PLATFORM_ADMIN_BREAK_GLASS_SECRET")
                                ?? "dev-break-glass-secret-key-2026";

        if (string.IsNullOrWhiteSpace(breakGlassSecret) ||
            !string.Equals(breakGlassSecret.Trim(), expectedSecret, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Unauthorized("INVALID_BREAK_GLASS_SECRET", "Invalid break-glass secret."));
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var admin = await _dbContext.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);

        if (admin == null)
        {
            return Result.Failure(Error.NotFound("ADMIN_NOT_FOUND", "Platform admin not found."));
        }

        string freshSecret = _totpService.GenerateSecretBase32();
        admin.ResetTwoFactorBreakGlass(_totpService.ProtectSecret(freshSecret), _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PlatformAdminBreakGlassReset2FA",
            entityName: "PlatformAdmin",
            entityId: admin.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { email = normalizedEmail }),
            userId: admin.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    private (string Token, DateTimeOffset ExpiresAtUtc) GenerateAdminJwt(
        PlatformAdmin admin,
        bool totpEnrolled,
        TimeSpan lifetime)
    {
        string jwtSecret = _configuration["Security:JwtSecret"]
                           ?? "clinic-platform-ultra-secure-jwt-secret-signing-key-minimum-32-chars";
        string issuer = _configuration["Security:JwtIssuer"] ?? "ClinicPlatform";
        string audience = _configuration["Security:JwtAudience"] ?? "ClinicPlatformClients";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Email, admin.Email),
            new(ClaimTypes.Name, admin.FullName),
            new(ClaimTypes.Role, "SuperAdmin"),
            new("realm", "PlatformAdmin"),
            new("totp_enrolled", totpEnrolled ? "true" : "false"),
            new("securityStamp", admin.SecurityStamp)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var issuedAtUtc = _clock.UtcNow;
        var expiresAtUtc = issuedAtUtc.Add(lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = issuedAtUtc.UtcDateTime,
            IssuedAt = issuedAtUtc.UtcDateTime,
            Expires = expiresAtUtc.UtcDateTime,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return (handler.WriteToken(token), expiresAtUtc);
    }
}
