using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Core authentication and credential management service enforcing security policies and token rotation.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IVerificationCodes _verificationCodes;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IClock _clock;
    private readonly IOutbox _outbox;
    private readonly IAuditService _auditService;

    public AuthService(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        IVerificationCodes verificationCodes,
        IPasswordPolicy passwordPolicy,
        IClock clock,
        IOutbox outbox,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _verificationCodes = verificationCodes ?? throw new ArgumentNullException(nameof(verificationCodes));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Result<AuthResult>> LoginAsync(
        string email,
        string password,
        string deviceId,
        string deviceName,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Result<AuthResult>.Failure(Error.Unauthorized("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return Result<AuthResult>.Failure(Error.Unauthorized("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        // Lockout Check
        if (await _userManager.IsLockedOutAsync(user))
        {
            return Result<AuthResult>.Failure(Error.Unauthorized(
                "ACCOUNT_LOCKED",
                "Account is locked due to repeated failed login attempts. Please try again later."));
        }

        // Activation Check
        if (!user.EmailConfirmed || !user.IsActive)
        {
            return Result<AuthResult>.Failure(Error.Unauthorized(
                "ACCOUNT_NOT_ACTIVATED",
                "Account is not activated. Please enter your verification code to activate your account."));
        }

        // Check Password
        bool isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
        {
            await _userManager.AccessFailedAsync(user);
            await _auditService.LogAsync(
                action: "LoginFailed",
                entityName: "AppUser",
                entityId: user.Id.ToString(),
                oldValues: null,
                newValues: null,
                userId: user.Id,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);

            return Result<AuthResult>.Failure(Error.Unauthorized("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        // Reset failed access count on success
        await _userManager.ResetAccessFailedCountAsync(user);

        // Device Check
        string cleanDeviceId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString("N") : deviceId.Trim();
        var device = await _dbContext.AppDevices
            .FirstOrDefaultAsync(d => d.UserId == user.Id && d.DeviceId == cleanDeviceId, cancellationToken);

        if (device != null && device.IsRevoked)
        {
            return Result<AuthResult>.Failure(Error.Unauthorized("DEVICE_REVOKED", "This device has been revoked."));
        }

        if (device == null)
        {
            device = new AppDevice(GuidV7.NewGuid(), user.Id, cleanDeviceId, deviceName ?? "Web Browser", _clock.UtcNow);
            _dbContext.AppDevices.Add(device);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Resolve Active Clinic
        var userClinic = await _dbContext.UserClinics
            .FirstOrDefaultAsync(uc => uc.UserId == user.Id, cancellationToken);

        var tokenResult = await _tokenService.GenerateTokensAsync(user, cleanDeviceId, userClinic?.ClinicId, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result<AuthResult>.Failure(tokenResult.Error);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userInfo = new UserInfoResult(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.Locale,
            user.Theme,
            roles.ToList(),
            userClinic?.ClinicId);

        await _auditService.LogAsync(
            action: "UserLoggedIn",
            entityName: "AppUser",
            entityId: user.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { deviceId = cleanDeviceId }),
            userId: user.Id,
            clinicId: userClinic?.ClinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<AuthResult>.Success(new AuthResult(tokenResult.Value, userInfo));
    }

    public async Task<Result<AuthResult>> ActivateAccountAsync(
        string email,
        string code,
        string password,
        string deviceId,
        string deviceName,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return Result<AuthResult>.Failure(Error.Validation("INPUT_REQUIRED", "Email and activation code are required."));
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return Result<AuthResult>.Failure(Error.Unauthorized("INVALID_CODE", "Invalid activation code."));
        }

        var passwordValidation = _passwordPolicy.Validate(password, normalizedEmail);
        if (!passwordValidation.IsSuccess)
        {
            return Result<AuthResult>.Failure(passwordValidation.Error);
        }

        var codeEntity = await _dbContext.VerificationCodes
            .Where(c => c.UserId == user.Id && c.Purpose == "Invitation" && c.UsedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (codeEntity == null || codeEntity.IsDead() || codeEntity.IsExpired(_clock.UtcNow))
        {
            return Result<AuthResult>.Failure(Error.Unauthorized("CODE_EXPIRED", "The activation code is expired or invalid."));
        }

        bool codeValid = _verificationCodes.Verify(code, codeEntity.CodeHash, user.Id.ToString(), "Invitation");
        if (!codeValid)
        {
            codeEntity.RecordFailedAttempt();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<AuthResult>.Failure(Error.Unauthorized("INVALID_CODE", "The provided activation code is incorrect."));
        }

        // Code Valid: Mark used and activate user
        codeEntity.MarkUsed(_clock.UtcNow);

        var addPasswordResult = await _userManager.AddPasswordAsync(user, password);
        if (!addPasswordResult.Succeeded)
        {
            string err = string.Join(", ", addPasswordResult.Errors.Select(e => e.Description));
            return Result<AuthResult>.Failure(Error.Failure("PASSWORD_SET_FAILED", err));
        }

        user.EmailConfirmed = true;
        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        // Register device
        string cleanDeviceId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString("N") : deviceId.Trim();
        var device = new AppDevice(GuidV7.NewGuid(), user.Id, cleanDeviceId, deviceName ?? "Web Browser", _clock.UtcNow);
        _dbContext.AppDevices.Add(device);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Resolve Active Clinic
        var userClinic = await _dbContext.UserClinics
            .FirstOrDefaultAsync(uc => uc.UserId == user.Id, cancellationToken);

        var tokenResult = await _tokenService.GenerateTokensAsync(user, cleanDeviceId, userClinic?.ClinicId, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result<AuthResult>.Failure(tokenResult.Error);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userInfo = new UserInfoResult(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.Locale,
            user.Theme,
            roles.ToList(),
            userClinic?.ClinicId);

        await _auditService.LogAsync(
            action: "AccountActivated",
            entityName: "AppUser",
            entityId: user.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { email = normalizedEmail }),
            userId: user.Id,
            clinicId: userClinic?.ClinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<AuthResult>.Success(new AuthResult(tokenResult.Value, userInfo));
    }

    public async Task<Result> ForgotPasswordAsync(
        string email,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Success(); // Always respond identically
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user != null && user.IsActive && user.EmailConfirmed)
        {
            // Cooldown check: 60 seconds
            var recentCode = await _dbContext.VerificationCodes
                .Where(c => c.UserId == user.Id && c.Purpose == "PasswordReset")
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (recentCode == null || (_clock.UtcNow - recentCode.CreatedAtUtc).TotalSeconds >= 60)
            {
                // Invalidate old active codes
                var oldCodes = await _dbContext.VerificationCodes
                    .Where(c => c.UserId == user.Id && c.Purpose == "PasswordReset" && c.UsedAtUtc == null)
                    .ToListAsync(cancellationToken);

                foreach (var old in oldCodes)
                {
                    old.MarkUsed(_clock.UtcNow);
                }

                string rawCode = _verificationCodes.GenerateCode();
                string hmac = _verificationCodes.ComputeHmac(rawCode, user.Id.ToString(), "PasswordReset");

                var verificationCode = new VerificationCode(
                    id: GuidV7.NewGuid(),
                    userId: user.Id,
                    realm: "Staff",
                    purpose: "PasswordReset",
                    codeHash: hmac,
                    expiryUtc: _clock.UtcNow.AddMinutes(15), // 15 minutes expiry
                    createdAtUtc: _clock.UtcNow,
                    maxAttempts: 5);

                _dbContext.VerificationCodes.Add(verificationCode);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var emailPayload = new
                {
                    to = normalizedEmail,
                    subject = "إعادة تعيين كلمة المرور / Password Reset Code",
                    body = $"رمز إعادة تعيين كلمة المرور هو: {rawCode} (ينتهي خلال 15 دقيقة).\nYour reset code is: {rawCode} (expires in 15 minutes)."
                };

                await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);

                await _auditService.LogAsync(
                    action: "ForgotPasswordRequested",
                    entityName: "AppUser",
                    entityId: user.Id.ToString(),
                    oldValues: null,
                    newValues: null,
                    userId: user.Id,
                    ipAddress: ipAddress,
                    requestId: requestId,
                    cancellationToken: cancellationToken);
            }
        }

        // Generic identical response for both known and unknown emails
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure(Error.Validation("INPUT_REQUIRED", "Email and reset code are required."));
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return Result.Failure(Error.Unauthorized("INVALID_CODE", "Invalid reset code."));
        }

        var validation = _passwordPolicy.Validate(newPassword, normalizedEmail);
        if (!validation.IsSuccess)
        {
            return Result.Failure(validation.Error);
        }

        var codeEntity = await _dbContext.VerificationCodes
            .Where(c => c.UserId == user.Id && c.Purpose == "PasswordReset" && c.UsedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (codeEntity == null || codeEntity.IsDead() || codeEntity.IsExpired(_clock.UtcNow))
        {
            return Result.Failure(Error.Unauthorized("CODE_EXPIRED", "The reset code is expired or invalid."));
        }

        bool codeValid = _verificationCodes.Verify(code, codeEntity.CodeHash, user.Id.ToString(), "PasswordReset");
        if (!codeValid)
        {
            codeEntity.RecordFailedAttempt();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Unauthorized("INVALID_CODE", "The provided reset code is incorrect."));
        }

        // Code Valid: Mark used
        codeEntity.MarkUsed(_clock.UtcNow);

        // Reset Password via token
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!resetResult.Succeeded)
        {
            string err = string.Join(", ", resetResult.Errors.Select(e => e.Description));
            return Result.Failure(Error.Failure("RESET_FAILED", err));
        }

        // Rotate security stamp (invalidates active cookie sessions)
        await _userManager.UpdateSecurityStampAsync(user);

        // Revoke all refresh token families
        await _tokenService.RevokeTokensForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send confirmation notice email
        var emailPayload = new
        {
            to = normalizedEmail,
            subject = "تم تغيير كلمة المرور الخاصة بك / Password Changed Notice",
            body = "تم تغيير كلمة المرور لحسابك بنجاح. إذا لم تكن أنت من قام بهذا الإجراء، يرجى التواصل مع إدارة النظام فوراً.\nYour password was successfully updated. If you did not make this change, contact platform security immediately."
        };

        await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);

        await _auditService.LogAsync(
            action: "PasswordReset",
            entityName: "AppUser",
            entityId: user.Id.ToString(),
            oldValues: null,
            newValues: null,
            userId: user.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResendCodeAsync(
        string email,
        string purpose,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Success();
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        string cleanPurpose = string.IsNullOrWhiteSpace(purpose) ? "Invitation" : purpose.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return Result.Success();
        }

        // 60-second cooldown check on active unexpired codes
        var recentActiveCode = await _dbContext.VerificationCodes
            .Where(c => c.UserId == user.Id && c.Purpose == cleanPurpose && c.UsedAtUtc == null && c.Attempts < c.MaxAttempts)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentActiveCode != null && (_clock.UtcNow - recentActiveCode.CreatedAtUtc).TotalSeconds < 60)
        {
            return Result.Failure(Error.Conflict(
                "RESEND_COOLDOWN",
                "Please wait at least 60 seconds before requesting another code."));
        }

        // Invalidate older codes
        var oldCodes = await _dbContext.VerificationCodes
            .Where(c => c.UserId == user.Id && c.Purpose == cleanPurpose && c.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var old in oldCodes)
        {
            old.MarkUsed(_clock.UtcNow);
        }

        string rawCode = _verificationCodes.GenerateCode();
        string hmac = _verificationCodes.ComputeHmac(rawCode, user.Id.ToString(), cleanPurpose);

        var expiry = cleanPurpose == "Invitation"
            ? _clock.UtcNow.AddHours(72)
            : _clock.UtcNow.AddMinutes(15);

        var newCode = new VerificationCode(
            id: GuidV7.NewGuid(),
            userId: user.Id,
            realm: "Staff",
            purpose: cleanPurpose,
            codeHash: hmac,
            expiryUtc: expiry,
            createdAtUtc: _clock.UtcNow,
            maxAttempts: 5);

        _dbContext.VerificationCodes.Add(newCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailPayload = new
        {
            to = normalizedEmail,
            subject = "رمز تحقق جديد / Fresh Verification Code",
            body = $"رمز التحقق الجديد الخاص بك هو: {rawCode}.\nYour fresh verification code is: {rawCode}."
        };

        await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);

        await _auditService.LogAsync(
            action: "VerificationCodeResent",
            entityName: "VerificationCode",
            entityId: newCode.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { purpose = cleanPurpose }),
            userId: user.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public Task<Result<TokenResult>> RefreshTokenAsync(
        string rawRefreshToken,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        return _tokenService.RefreshTokensAsync(rawRefreshToken, deviceId, cancellationToken);
    }

    public async Task<Result> RequestEmailChangeAsync(
        Guid userId,
        string newEmail,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
        {
            return Result.Failure(Error.Validation("EMAIL_REQUIRED", "New email address is required."));
        }

        string normalizedNewEmail = newEmail.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(normalizedNewEmail);
        if (existing != null)
        {
            return Result.Failure(Error.Conflict("EMAIL_IN_USE", "This email address is already in use by another account."));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound("USER_NOT_FOUND", "User account not found."));
        }

        string rawCode = _verificationCodes.GenerateCode();
        string hmac = _verificationCodes.ComputeHmac(rawCode, userId.ToString(), "EmailChange");

        var codeEntity = new VerificationCode(
            id: GuidV7.NewGuid(),
            userId: userId,
            realm: "Staff",
            purpose: "EmailChange",
            codeHash: hmac,
            expiryUtc: _clock.UtcNow.AddMinutes(15),
            createdAtUtc: _clock.UtcNow,
            maxAttempts: 5,
            payload: normalizedNewEmail);

        _dbContext.VerificationCodes.Add(codeEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailPayload = new
        {
            to = normalizedNewEmail,
            subject = "رمز تأكيد تغيير البريد الإلكتروني / Email Change Confirmation Code",
            body = $"رمز تأكيد تغيير البريد الإلكتروني هو: {rawCode} (ينتهي خلال 15 دقيقة).\nYour email change confirmation code is: {rawCode}."
        };

        await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);

        await _auditService.LogAsync(
            action: "EmailChangeRequested",
            entityName: "AppUser",
            entityId: userId.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { newEmail = normalizedNewEmail }),
            userId: userId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(
        Guid userId,
        string code,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound("USER_NOT_FOUND", "User account not found."));
        }

        var codeEntity = await _dbContext.VerificationCodes
            .Where(c => c.UserId == userId && c.Purpose == "EmailChange" && c.UsedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (codeEntity == null || codeEntity.IsDead() || codeEntity.IsExpired(_clock.UtcNow))
        {
            return Result.Failure(Error.Unauthorized("CODE_EXPIRED", "The email change code is expired or invalid."));
        }

        bool valid = _verificationCodes.Verify(code, codeEntity.CodeHash, userId.ToString(), "EmailChange");
        if (!valid)
        {
            codeEntity.RecordFailedAttempt();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Unauthorized("INVALID_CODE", "The provided code is incorrect."));
        }

        codeEntity.MarkUsed(_clock.UtcNow);
        string oldEmail = user.Email ?? string.Empty;
        string newEmail = codeEntity.Payload ?? string.Empty;

        user.Email = newEmail;
        user.UserName = newEmail;
        await _userManager.UpdateAsync(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Notify old email address
        var oldNoticePayload = new
        {
            to = oldEmail,
            subject = "إشعار تغيير البريد الإلكتروني / Email Changed Notice",
            body = $"تم تغيير البريد الإلكتروني لحسابك إلى {newEmail}.\nYour account email was changed to {newEmail}."
        };

        await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(oldNoticePayload), cancellationToken);

        await _auditService.LogAsync(
            action: "EmailChanged",
            entityName: "AppUser",
            entityId: userId.ToString(),
            oldValues: JsonSerializer.Serialize(new { email = oldEmail }),
            newValues: JsonSerializer.Serialize(new { email = newEmail }),
            userId: userId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RevokeDeviceAsync(
        Guid userId,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Result.Failure(Error.Validation("DEVICE_ID_REQUIRED", "Device ID is required."));
        }

        var device = await _dbContext.AppDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId.Trim(), cancellationToken);

        if (device != null)
        {
            device.Revoke(_clock.UtcNow);
            await _tokenService.RevokeDeviceTokensAsync(userId, deviceId.Trim(), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                action: "DeviceRevoked",
                entityName: "AppDevice",
                entityId: device.Id.ToString(),
                oldValues: null,
                newValues: JsonSerializer.Serialize(new { deviceId }),
                userId: userId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return Result.Success();
    }
}
