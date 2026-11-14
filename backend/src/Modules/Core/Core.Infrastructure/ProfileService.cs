using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements self-service profile updates, email change verification, password rotation with session revocation,
/// and device revocation under policy profile.self.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly IVerificationCodes _verificationCodes;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IOutbox _outbox;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ProfileService(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        IAuthService authService,
        ITokenService tokenService,
        IVerificationCodes verificationCodes,
        IPasswordPolicy passwordPolicy,
        IOutbox outbox,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _verificationCodes = verificationCodes ?? throw new ArgumentNullException(nameof(verificationCodes));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<UserProfileResult>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<UserProfileResult>.Failure(Error.NotFound("USER_NOT_FOUND", "User account not found."));
        }

        return Result<UserProfileResult>.Success(await BuildProfileResultAsync(user, cancellationToken));
    }

    public async Task<Result<UserProfileResult>> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string? phoneNumber,
        string? locale,
        string? theme,
        Guid? avatarAssetId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<UserProfileResult>.Failure(Error.NotFound("USER_NOT_FOUND", "User account not found."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<UserProfileResult>.Failure(Error.Validation("FULL_NAME_REQUIRED", "Full name is required."));
        }

        string? normalizedPhone = user.PhoneNumber;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneNorm = PhoneNormalizer.Normalize(phoneNumber);
            if (phoneNorm.IsFailure)
            {
                return Result<UserProfileResult>.Failure(phoneNorm.Error);
            }

            normalizedPhone = phoneNorm.Value;
        }
        else if (phoneNumber != null)
        {
            normalizedPhone = null;
        }

        string cleanLocale = locale?.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            _ => "ar"
        };

        string cleanTheme = theme?.Trim().ToLowerInvariant() switch
        {
            "dark" => "dark",
            "system" => "system",
            _ => "light"
        };

        string oldValues = JsonSerializer.Serialize(new { user.FullName, user.PhoneNumber, user.Locale, user.Theme });

        user.FullName = fullName.Trim();
        user.PhoneNumber = normalizedPhone;
        if (!string.IsNullOrWhiteSpace(locale))
        {
            user.Locale = cleanLocale;
        }

        if (!string.IsNullOrWhiteSpace(theme))
        {
            user.Theme = cleanTheme;
        }

        if (avatarAssetId.HasValue)
        {
            user.AvatarAssetId = avatarAssetId.Value;
        }

        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(
            action: "ProfileUpdated",
            entityName: "AppUser",
            entityId: user.Id.ToString(),
            oldValues: oldValues,
            newValues: JsonSerializer.Serialize(new { user.FullName, user.PhoneNumber, user.Locale, user.Theme }),
            userId: user.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<UserProfileResult>.Success(await BuildProfileResultAsync(user, cancellationToken));
    }

    public Task<Result> RequestEmailChangeAsync(
        Guid userId,
        string newEmail,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        return _authService.RequestEmailChangeAsync(userId, newEmail, ipAddress, requestId, cancellationToken);
    }

    public async Task<Result<TokenResult>> ConfirmEmailChangeAsync(
        Guid userId,
        string code,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var confirmResult = await _authService.ConfirmEmailChangeAsync(userId, code, ipAddress, requestId, cancellationToken);
        if (confirmResult.IsFailure)
        {
            return Result<TokenResult>.Failure(confirmResult.Error);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<TokenResult>.Failure(Error.NotFound("USER_NOT_FOUND", "User account not found."));
        }

        // Security stamp rotation revokes existing tokens & active sessions
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _userManager.UpdateAsync(user);
        await _tokenService.RevokeTokensForUserAsync(userId, cancellationToken);

        string cleanDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "web-browser" : deviceId.Trim();
        Guid? defaultClinicId = await ResolveDefaultClinicIdAsync(userId, cancellationToken);
        return await _tokenService.GenerateTokensAsync(user, cleanDeviceId, defaultClinicId, cancellationToken);
    }

    public async Task<Result<TokenResult>> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        bool signOutOtherDevices,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("INVALID_CURRENT_PASSWORD", "Current password is required."));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<TokenResult>.Failure(Error.NotFound("USER_NOT_FOUND", "User account not found."));
        }

        var policyCheck = _passwordPolicy.Validate(newPassword, user.Email ?? string.Empty);
        if (!policyCheck.IsSuccess)
        {
            return Result<TokenResult>.Failure(policyCheck.Error);
        }

        bool validCurrent = await _userManager.CheckPasswordAsync(user, currentPassword);
        if (!validCurrent)
        {
            return Result<TokenResult>.Failure(Error.Unauthorized("INVALID_CURRENT_PASSWORD", "The current password is incorrect."));
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changeResult.Succeeded)
        {
            return Result<TokenResult>.Failure(Error.Validation(
                "PASSWORD_CHANGE_FAILED",
                string.Join("; ", changeResult.Errors.Select(e => e.Description))));
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _userManager.UpdateAsync(user);

        string cleanDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "web-browser" : deviceId.Trim();
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked && (!signOutOtherDevices || t.DeviceId != cleanDeviceId))
            .ToListAsync(cancellationToken);

        if (signOutOtherDevices)
        {
            var otherDevices = await _dbContext.AppDevices
                .Where(d => d.UserId == userId && d.DeviceId != cleanDeviceId && !d.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var otherDevice in otherDevices)
            {
                otherDevice.Revoke(_clock.UtcNow);
            }
        }

        foreach (var token in activeTokens)
        {
            token.Revoke(_clock.UtcNow);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _outbox.EnqueueAsync(
            "SendEmail",
            JsonSerializer.Serialize(new
            {
                to = user.Email,
                subject = "إشعار تغيير كلمة المرور / Password Changed Security Notice",
                body = "تم تغيير كلمة المرور الخاصة بحسابك بنجاح.\nYour account password has been changed."
            }),
            cancellationToken);

        await _auditService.LogAsync(
            action: "PasswordChanged",
            entityName: "AppUser",
            entityId: user.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { signOutOtherDevices }),
            userId: user.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        Guid? defaultClinicId = await ResolveDefaultClinicIdAsync(userId, cancellationToken);
        return await _tokenService.GenerateTokensAsync(user, cleanDeviceId, defaultClinicId, cancellationToken);
    }

    public Task<Result> RevokeDeviceAsync(
        Guid userId,
        string deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        return _authService.RevokeDeviceAsync(userId, deviceId, ipAddress, requestId, cancellationToken);
    }

    private async Task<UserProfileResult> BuildProfileResultAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var devices = await _dbContext.AppDevices
            .Where(d => d.UserId == user.Id)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new UserDeviceItemResult(
                d.Id,
                d.DeviceId,
                d.DeviceName,
                d.ApprovedAtUtc != null,
                d.IsRevoked,
                d.CreatedAtUtc,
                d.ApprovedAtUtc ?? d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var memberships = await (
            from uc in _dbContext.UserClinics
            join c in _dbContext.Clinics on uc.ClinicId equals c.Id
            where uc.UserId == user.Id
            orderby c.Name
            select new UserClinicMembershipResult(
                c.Id,
                c.Name,
                c.Slug,
                uc.Role,
                c.Status,
                c.OnboardingStatus))
            .ToListAsync(cancellationToken);

        return new UserProfileResult(
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            AvatarAssetId: user.AvatarAssetId,
            Locale: user.Locale,
            Theme: user.Theme,
            Devices: devices,
            Clinics: memberships);
    }

    private async Task<Guid?> ResolveDefaultClinicIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserClinics
            .Where(uc => uc.UserId == userId)
            .OrderBy(uc => uc.CreatedAtUtc)
            .Select(uc => (Guid?)uc.ClinicId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
