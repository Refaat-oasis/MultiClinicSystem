using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Handles user account invitations, role assignments, and dispatching activation verification codes.
/// </summary>
public sealed class AccountInvitations : IAccountInvitations
{
    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IVerificationCodes _verificationCodes;
    private readonly IClock _clock;
    private readonly IOutbox _outbox;
    private readonly IAuditService _auditService;

    public AccountInvitations(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        IVerificationCodes verificationCodes,
        IClock clock,
        IOutbox outbox,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _verificationCodes = verificationCodes ?? throw new ArgumentNullException(nameof(verificationCodes));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Result<Guid>> InviteAsync(
        string realm,
        string email,
        string fullName,
        string role,
        Guid? clinicId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<Guid>.Failure(Error.Validation("EMAIL_REQUIRED", "Email address is required."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<Guid>.Failure(Error.Validation("NAME_REQUIRED", "Full name is required."));
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            return Result<Guid>.Failure(Error.Conflict("USER_ALREADY_EXISTS", "A user with this email address already exists."));
        }

        var userId = GuidV7.NewGuid();
        var user = new AppUser(userId, normalizedEmail, fullName.Trim(), _clock.UtcNow)
        {
            EmailConfirmed = false,
            IsActive = false
        };

        var identityResult = await _userManager.CreateAsync(user);
        if (!identityResult.Succeeded)
        {
            string errorMessage = string.Join(", ", identityResult.Errors.Select(e => e.Description));
            return Result<Guid>.Failure(Error.Failure("USER_CREATION_FAILED", errorMessage));
        }

        // Ensure Role Exists and Assign
        if (!string.IsNullOrWhiteSpace(role))
        {
            string roleName = role.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new AppRole(roleName));
            }

            await _userManager.AddToRoleAsync(user, roleName);
        }

        // Link User to Clinic if specified
        if (clinicId.HasValue)
        {
            var userClinic = new UserClinic(
                id: GuidV7.NewGuid(),
                userId: userId,
                clinicId: clinicId.Value,
                role: role ?? "Staff",
                createdAtUtc: _clock.UtcNow);

            _dbContext.UserClinics.Add(userClinic);
        }

        // Create Owner record if role is Owner
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            var owner = new Owner(
                id: GuidV7.NewGuid(),
                userId: userId,
                maxClinics: null,
                createdAtUtc: _clock.UtcNow);

            _dbContext.Owners.Add(owner);
        }

        // Invalidate any older active verification codes for this user
        var oldCodes = await _dbContext.VerificationCodes
            .Where(c => c.UserId == userId && c.Purpose == "Invitation" && c.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var oldCode in oldCodes)
        {
            oldCode.MarkUsed(_clock.UtcNow);
        }

        // Generate 6-digit Code & HMAC
        string rawCode = _verificationCodes.GenerateCode();
        string hmac = _verificationCodes.ComputeHmac(rawCode, userId.ToString(), "Invitation");

        var verificationCode = new VerificationCode(
            id: GuidV7.NewGuid(),
            userId: userId,
            realm: realm ?? "Staff",
            purpose: "Invitation",
            codeHash: hmac,
            expiryUtc: _clock.UtcNow.AddHours(72), // Invitation expiry: 72 hours
            createdAtUtc: _clock.UtcNow,
            maxAttempts: 5);

        _dbContext.VerificationCodes.Add(verificationCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Queue activation code email through Outbox
        var emailPayload = new
        {
            to = normalizedEmail,
            subject = "دعوة لتفعيل حسابك في المنصة الطبية / Account Activation Code",
            body = $"رمز التفعيل الخاص بك هو: {rawCode} (ينتهي خلال 72 ساعة).\nYour activation code is: {rawCode} (expires in 72 hours)."
        };

        await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);

        // Audit Trail
        await _auditService.LogAsync(
            action: "UserInvited",
            entityName: "AppUser",
            entityId: userId.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { email = normalizedEmail, role, clinicId }),
            userId: userId,
            clinicId: clinicId,
            cancellationToken: cancellationToken);

        return Result<Guid>.Success(userId);
    }
}
