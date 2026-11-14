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
/// Platform Super Admin domain service for owner management, clinic directory, step-up enforcement,
/// read-only inspect mode with PatientAccessLog recording, and platform monitoring widgets.
/// </summary>
public sealed class PlatformAdminService : IPlatformAdminService
{
    private static readonly TimeSpan StepUpWindow = TimeSpan.FromMinutes(10);

    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAccountInvitations _accountInvitations;
    private readonly IAuthService _authService;
    private readonly IPlatformAdminAuthService _adminAuthService;
    private readonly IPlatformAccess _platformAccess;
    private readonly ITokenService _tokenService;
    private readonly IModuleCatalogService _moduleCatalog;
    private readonly IOutbox _outbox;
    private readonly IClock _clock;
    private readonly IAuditService _auditService;

    public PlatformAdminService(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        IAccountInvitations accountInvitations,
        IAuthService authService,
        IPlatformAdminAuthService adminAuthService,
        IPlatformAccess platformAccess,
        ITokenService tokenService,
        IModuleCatalogService moduleCatalog,
        IOutbox outbox,
        IClock clock,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _accountInvitations = accountInvitations ?? throw new ArgumentNullException(nameof(accountInvitations));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _adminAuthService = adminAuthService ?? throw new ArgumentNullException(nameof(adminAuthService));
        _platformAccess = platformAccess ?? throw new ArgumentNullException(nameof(platformAccess));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Result<OwnerSummaryResult>> CreateOwnerAsync(
        Guid adminId,
        string fullName,
        string email,
        string? phone,
        int? maxClinics,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var inviteResult = await _accountInvitations.InviteAsync(
            realm: "Staff",
            email: email,
            fullName: fullName,
            role: "Owner",
            clinicId: null,
            cancellationToken: cancellationToken);

        if (!inviteResult.IsSuccess)
        {
            return Result<OwnerSummaryResult>.Failure(inviteResult.Error);
        }

        Guid userId = inviteResult.Value;
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null && !string.IsNullOrWhiteSpace(phone))
        {
            user.PhoneNumber = phone.Trim();
            await _userManager.UpdateAsync(user);
        }

        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);
        if (owner == null)
        {
            owner = new Owner(GuidV7.NewGuid(), userId, maxClinics, _clock.UtcNow);
            _dbContext.Owners.Add(owner);
        }
        else if (maxClinics.HasValue)
        {
            owner.UpdateMaxClinics(maxClinics);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OwnerCreatedBySuperAdmin",
            entityName: "Owner",
            entityId: owner.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { email = user?.Email, maxClinics }),
            userId: adminId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<OwnerSummaryResult>.Success(new OwnerSummaryResult(
            OwnerId: owner.Id,
            UserId: userId,
            FullName: user?.FullName ?? fullName,
            Email: user?.Email ?? email,
            PhoneNumber: user?.PhoneNumber,
            Status: owner.Status,
            EmailConfirmed: user?.EmailConfirmed ?? false,
            MaxClinics: owner.MaxClinics,
            ClinicsCount: 0,
            CreatedAtUtc: owner.CreatedAtUtc,
            Clinics: Array.Empty<ClinicSummaryResult>()));
    }

    public async Task<Result<IReadOnlyList<OwnerSummaryResult>>> ListOwnersAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var owners = await _dbContext.Owners
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = owners.Select(o => o.UserId).ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var ownerIds = owners.Select(o => o.Id).ToList();
        var clinics = await _dbContext.Clinics
            .Where(c => c.OwnerId.HasValue && ownerIds.Contains(c.OwnerId.Value))
            .ToListAsync(cancellationToken);

        var clinicIds = clinics.Select(c => c.Id).ToList();
        var enabledModulesByClinic = await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            var rows = await _dbContext.ClinicModules
                .Where(m => clinicIds.Contains(m.ClinicId) && m.Enabled)
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(m => m.ClinicId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.ModuleKey).OrderBy(k => k).ToList());
        }, reason: "SuperAdmin List Clinic Modules", actorId: "platform-admin", cancellationToken: cancellationToken);

        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var results = new List<OwnerSummaryResult>();
        foreach (var owner in owners)
        {
            users.TryGetValue(owner.UserId, out var user);
            var ownerClinics = clinics
                .Where(c => c.OwnerId == owner.Id)
                .Select(c => MapClinicSummary(c, enabledModulesByClinic.TryGetValue(c.Id, out var mods) ? mods : Array.Empty<string>()))
                .ToList();

            if (term != null)
            {
                bool matchesOwner =
                    (user?.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user?.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
                bool matchesClinic = ownerClinics.Any(c =>
                    c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Slug.Contains(term, StringComparison.OrdinalIgnoreCase));

                if (!matchesOwner && !matchesClinic)
                {
                    continue;
                }
            }

            results.Add(new OwnerSummaryResult(
                OwnerId: owner.Id,
                UserId: owner.UserId,
                FullName: user?.FullName ?? string.Empty,
                Email: user?.Email ?? string.Empty,
                PhoneNumber: user?.PhoneNumber,
                Status: owner.Status,
                EmailConfirmed: user?.EmailConfirmed ?? false,
                MaxClinics: owner.MaxClinics,
                ClinicsCount: ownerClinics.Count,
                CreatedAtUtc: owner.CreatedAtUtc,
                Clinics: ownerClinics));
        }

        return Result<IReadOnlyList<OwnerSummaryResult>>.Success(results);
    }

    public async Task<Result<OwnerDetailResult>> GetOwnerDetailAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var owner = await _dbContext.Owners
            .FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);

        if (owner == null)
        {
            return Result<OwnerDetailResult>.Failure(Error.NotFound("OWNER_NOT_FOUND", "Owner not found."));
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == owner.UserId, cancellationToken);

        var clinics = await _dbContext.Clinics
            .Where(c => c.OwnerId == owner.Id)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var ownerClinicIds = clinics.Select(c => c.Id).ToList();
        var enabledModulesByClinic = await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            var rows = await _dbContext.ClinicModules
                .Where(m => ownerClinicIds.Contains(m.ClinicId) && m.Enabled)
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(m => m.ClinicId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.ModuleKey).OrderBy(k => k).ToList());
        }, reason: "SuperAdmin Get Owner Detail Modules", actorId: "platform-admin", cancellationToken: cancellationToken);

        return Result<OwnerDetailResult>.Success(new OwnerDetailResult(
            OwnerId: owner.Id,
            UserId: owner.UserId,
            FullName: user?.FullName ?? string.Empty,
            Email: user?.Email ?? string.Empty,
            PhoneNumber: user?.PhoneNumber,
            Status: owner.Status,
            EmailConfirmed: user?.EmailConfirmed ?? false,
            MaxClinics: owner.MaxClinics,
            CreatedAtUtc: owner.CreatedAtUtc,
            Clinics: clinics.Select(c => MapClinicSummary(c, enabledModulesByClinic.TryGetValue(c.Id, out var mods) ? mods : Array.Empty<string>())).ToList()));
    }

    public async Task<Result> ResendOwnerInvitationCodeAsync(
        Guid adminId,
        Guid ownerId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
        if (owner == null)
        {
            return Result.Failure(Error.NotFound("OWNER_NOT_FOUND", "Owner not found."));
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == owner.UserId, cancellationToken);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Result.Failure(Error.NotFound("USER_NOT_FOUND", "Owner user record not found."));
        }

        var resendResult = await _authService.ResendCodeAsync(
            user.Email,
            "Invitation",
            ipAddress,
            requestId,
            cancellationToken);

        if (!resendResult.IsSuccess)
        {
            return resendResult;
        }

        await _auditService.LogAsync(
            action: "OwnerInvitationResent",
            entityName: "Owner",
            entityId: owner.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { email = user.Email }),
            userId: adminId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeactivateOwnerAsync(
        Guid adminId,
        Guid ownerId,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var stepUp = await EnsureStepUpAsync(adminId, stepUpPassword, ipAddress, requestId, cancellationToken);
        if (!stepUp.IsSuccess)
        {
            return stepUp;
        }

        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
        if (owner == null)
        {
            return Result.Failure(Error.NotFound("OWNER_NOT_FOUND", "Owner not found."));
        }

        owner.Deactivate();

        var user = await _userManager.FindByIdAsync(owner.UserId.ToString());
        if (user != null)
        {
            user.IsActive = false;
            await _userManager.UpdateSecurityStampAsync(user);
            await _userManager.UpdateAsync(user);
            await _tokenService.RevokeTokensForUserAsync(user.Id, cancellationToken);
        }

        var ownerClinics = await _dbContext.Clinics
            .Where(c => c.OwnerId == owner.Id)
            .ToListAsync(cancellationToken);

        foreach (var clinic in ownerClinics)
        {
            clinic.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OwnerDeactivated",
            entityName: "Owner",
            entityId: owner.Id.ToString(),
            oldValues: JsonSerializer.Serialize(new { status = "Active" }),
            newValues: JsonSerializer.Serialize(new { status = "Suspended" }),
            userId: adminId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ReactivateOwnerAsync(
        Guid adminId,
        Guid ownerId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
        if (owner == null)
        {
            return Result.Failure(Error.NotFound("OWNER_NOT_FOUND", "Owner not found."));
        }

        owner.Activate();

        var user = await _userManager.FindByIdAsync(owner.UserId.ToString());
        if (user != null)
        {
            user.IsActive = true;
            await _userManager.UpdateAsync(user);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OwnerReactivated",
            entityName: "Owner",
            entityId: owner.Id.ToString(),
            oldValues: JsonSerializer.Serialize(new { status = "Suspended" }),
            newValues: JsonSerializer.Serialize(new { status = "Active" }),
            userId: adminId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UpdateOwnerQuotaAsync(
        Guid adminId,
        Guid ownerId,
        int? maxClinics,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var stepUp = await EnsureStepUpAsync(adminId, stepUpPassword, ipAddress, requestId, cancellationToken);
        if (!stepUp.IsSuccess)
        {
            return stepUp;
        }

        if (maxClinics.HasValue && maxClinics.Value < 0)
        {
            return Result.Failure(Error.Validation("INVALID_QUOTA", "Max clinics quota cannot be negative."));
        }

        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
        if (owner == null)
        {
            return Result.Failure(Error.NotFound("OWNER_NOT_FOUND", "Owner not found."));
        }

        int? oldQuota = owner.MaxClinics;
        owner.UpdateMaxClinics(maxClinics);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OwnerQuotaUpdated",
            entityName: "Owner",
            entityId: owner.Id.ToString(),
            oldValues: JsonSerializer.Serialize(new { maxClinics = oldQuota }),
            newValues: JsonSerializer.Serialize(new { maxClinics }),
            userId: adminId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public Task<Result<IReadOnlyList<OwnerSummaryResult>>> GetClinicDirectoryAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        return ListOwnersAsync(search, cancellationToken);
    }

    public async Task<Result> UpdateClinicStatusAsync(
        Guid adminId,
        Guid clinicId,
        string status,
        DateTimeOffset? subscriptionExpiryUtc,
        int? graceDays,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "ReadOnly",
            "Suspended",
            "PendingOnboarding"
        };

        if (string.IsNullOrWhiteSpace(status) || !allowedStatuses.Contains(status.Trim()))
        {
            return Result.Failure(Error.Validation("INVALID_CLINIC_STATUS", "Status must be Active, ReadOnly, Suspended, or PendingOnboarding."));
        }

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        if (clinic == null)
        {
            return Result.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        string oldStatus = clinic.Status;
        string normalizedStatus = allowedStatuses.First(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (string.Equals(normalizedStatus, "Suspended", StringComparison.Ordinal))
        {
            clinic.Deactivate();
        }
        else if (string.Equals(normalizedStatus, "Active", StringComparison.Ordinal))
        {
            clinic.Activate();
        }
        else
        {
            clinic.SetStatus(normalizedStatus);
        }

        if (subscriptionExpiryUtc.HasValue || graceDays.HasValue)
        {
            clinic.UpdateSubscription(
                subscriptionExpiryUtc ?? clinic.SubscriptionExpiryUtc,
                graceDays ?? clinic.GraceDays);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicStatusUpdated",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: JsonSerializer.Serialize(new { status = oldStatus }),
            newValues: JsonSerializer.Serialize(new { status = clinic.Status, subscriptionExpiryUtc, graceDays = clinic.GraceDays }),
            userId: adminId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result> MoveClinicOwnerAsync(
        Guid adminId,
        Guid clinicId,
        Guid targetOwnerId,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var stepUp = await EnsureStepUpAsync(adminId, stepUpPassword, ipAddress, requestId, cancellationToken);
        if (!stepUp.IsSuccess)
        {
            return stepUp;
        }

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        if (clinic == null)
        {
            return Result.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var targetOwner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == targetOwnerId, cancellationToken);
        if (targetOwner == null)
        {
            return Result.Failure(Error.NotFound("TARGET_OWNER_NOT_FOUND", "Target owner not found."));
        }

        Guid? oldOwnerId = clinic.OwnerId;
        Owner? oldOwner = oldOwnerId.HasValue
            ? await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == oldOwnerId.Value, cancellationToken)
            : null;

        clinic.MoveToOwner(targetOwner.Id);

        if (oldOwner != null)
        {
            var oldMappings = await _dbContext.UserClinics
                .Where(uc => uc.ClinicId == clinic.Id && uc.UserId == oldOwner.UserId)
                .ToListAsync(cancellationToken);
            _dbContext.UserClinics.RemoveRange(oldMappings);
        }

        bool targetAlreadyLinked = await _dbContext.UserClinics
            .AnyAsync(uc => uc.ClinicId == clinic.Id && uc.UserId == targetOwner.UserId, cancellationToken);
        if (!targetAlreadyLinked)
        {
            _dbContext.UserClinics.Add(new UserClinic(
                GuidV7.NewGuid(),
                targetOwner.UserId,
                clinic.Id,
                "Owner",
                _clock.UtcNow));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicMoved",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: JsonSerializer.Serialize(new { ownerId = oldOwnerId }),
            newValues: JsonSerializer.Serialize(new { ownerId = targetOwner.Id }),
            userId: adminId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result<InspectClinicResult>> InspectClinicAsync(
        Guid adminId,
        Guid clinicId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);

        if (clinic == null)
        {
            return Result<InspectClinicResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var (sampleCount, totalAmount) = await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            var records = await _dbContext.TenantSampleRecords
                .Where(r => r.ClinicId == clinicId)
                .ToListAsync(cancellationToken);

            decimal sum = records.Sum(r => r.Amount.Amount);
            return (records.Count, new Money(sum, Money.DefaultCurrency));
        }, reason: "SuperAdmin Inspect Clinic", actorId: adminId.ToString(), cancellationToken: cancellationToken);

        var accessLog = new PatientAccessLog(
            id: GuidV7.NewGuid(),
            actorId: adminId.ToString(),
            reason: "SuperAdmin Inspect Clinic",
            timestampUtc: _clock.UtcNow,
            clinicId: clinicId,
            details: "ActorType:SuperAdmin;Mode:ReadOnlyInspect");

        _dbContext.PatientAccessLogs.Add(accessLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicInspected",
            entityName: "Clinic",
            entityId: clinic.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { readOnly = true, sampleCount }),
            userId: adminId,
            clinicId: clinic.Id,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<InspectClinicResult>.Success(new InspectClinicResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            Status: clinic.Status,
            OwnerId: clinic.OwnerId,
            SubscriptionExpiryUtc: clinic.SubscriptionExpiryUtc,
            GraceDays: clinic.GraceDays,
            SampleRecordsCount: sampleCount,
            SampleRecordsTotalAmount: totalAmount,
            ReadOnlyMode: true));
    }

    public async Task<Result<PlatformDashboardResult>> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        int totalOwners = await _dbContext.Owners.CountAsync(cancellationToken);
        int activeOwners = await _dbContext.Owners.CountAsync(o => o.Status == "Active", cancellationToken);

        var clinics = await _dbContext.Clinics.ToListAsync(cancellationToken);
        int totalClinics = clinics.Count;
        int activeClinics = clinics.Count(c => c.Status == "Active");
        int pendingOnboarding = clinics.Count(c => c.Status == "PendingOnboarding");
        var clinicsWithEnabledModules = await _platformAccess.ExecuteAsPlatformAdminAsync(() =>
            _dbContext.ClinicModules
                .Where(m => m.Enabled)
                .Select(m => m.ClinicId)
                .Distinct()
                .ToListAsync(cancellationToken),
            reason: "SuperAdmin Dashboard Module Count",
            actorId: "platform-admin",
            cancellationToken: cancellationToken);
        var configuredClinicSet = clinicsWithEnabledModules.ToHashSet();
        int awaitingModuleSetup = clinics.Count(c =>
            c.IsActive &&
            !string.Equals(c.Status, "Suspended", StringComparison.OrdinalIgnoreCase) &&
            !configuredClinicSet.Contains(c.Id));
        int readOnly = clinics.Count(c => c.Status == "ReadOnly");
        int suspended = clinics.Count(c => c.Status == "Suspended");

        DateTimeOffset soonBoundary = _clock.UtcNow.AddDays(30);
        int expiringSoon = clinics.Count(c =>
            c.SubscriptionExpiryUtc.HasValue && c.SubscriptionExpiryUtc.Value <= soonBoundary);

        DateTimeOffset since24h = _clock.UtcNow.AddHours(-24);
        int failedLogins = await _dbContext.AuditLogs
            .CountAsync(a => (a.Action == "LoginFailed" || a.Action == "PlatformAdminLoginFailed") && a.TimestampUtc >= since24h, cancellationToken);

        int pendingOutbox = await _dbContext.OutboxMessages
            .CountAsync(m => m.ProcessedAtUtc == null, cancellationToken);

        var recentAudit = await _dbContext.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .Take(10)
            .Select(a => new AuditLogItemResult(
                a.Id,
                a.UserId,
                a.ClinicId,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.OldValues,
                a.NewValues,
                a.TimestampUtc,
                a.IpAddress,
                a.RequestId))
            .ToListAsync(cancellationToken);

        return Result<PlatformDashboardResult>.Success(new PlatformDashboardResult(
            TotalOwners: totalOwners,
            ActiveOwners: activeOwners,
            TotalClinics: totalClinics,
            ActiveClinics: activeClinics,
            PendingOnboardingClinics: pendingOnboarding,
            ReadOnlyClinics: readOnly,
            SuspendedClinics: suspended,
            ClinicsAwaitingModuleSetup: awaitingModuleSetup,
            ExpiringSubscriptionsCount: expiringSoon,
            FailedLoginsLast24Hours: failedLogins,
            PendingOutboxMessages: pendingOutbox,
            RecentAuditEntries: recentAudit));
    }

    public async Task<Result<IReadOnlyList<AuditLogItemResult>>> GetAuditLogsAsync(
        Guid? clinicId,
        string? actionFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AuditLogs.AsQueryable();
        if (clinicId.HasValue)
        {
            query = query.Where(a => a.ClinicId == clinicId.Value);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            string filter = actionFilter.Trim();
            query = query.Where(a => a.Action.Contains(filter));
        }

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(a => new AuditLogItemResult(
                a.Id,
                a.UserId,
                a.ClinicId,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.OldValues,
                a.NewValues,
                a.TimestampUtc,
                a.IpAddress,
                a.RequestId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AuditLogItemResult>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<PatientAccessLogItemResult>>> GetClinicAccessLogsForOwnerAsync(
        Guid ownerUserId,
        Guid? clinicId,
        CancellationToken cancellationToken = default)
    {
        var owner = await _dbContext.Owners
            .FirstOrDefaultAsync(o => o.UserId == ownerUserId, cancellationToken);

        if (owner == null)
        {
            return Result<IReadOnlyList<PatientAccessLogItemResult>>.Success(Array.Empty<PatientAccessLogItemResult>());
        }

        var ownerClinicIds = await _dbContext.Clinics
            .Where(c => c.OwnerId == owner.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (clinicId.HasValue && !ownerClinicIds.Contains(clinicId.Value))
        {
            return Result<IReadOnlyList<PatientAccessLogItemResult>>.Failure(
                Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var targetIds = clinicId.HasValue ? new List<Guid> { clinicId.Value } : ownerClinicIds;

        var logs = await _dbContext.PatientAccessLogs
            .Where(l => l.ClinicId.HasValue && targetIds.Contains(l.ClinicId.Value))
            .OrderByDescending(l => l.TimestampUtc)
            .Take(100)
            .Select(l => new PatientAccessLogItemResult(
                l.Id,
                l.ActorId,
                l.Reason,
                l.TimestampUtc,
                l.ClinicId,
                l.Details))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PatientAccessLogItemResult>>.Success(logs);
    }

    public async Task<Result<IReadOnlyList<ClinicModuleStateResult>>> GetClinicModulesAsync(
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);

        if (clinic == null)
        {
            return Result<IReadOnlyList<ClinicModuleStateResult>>.Failure(
                Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var rows = await _platformAccess.ExecuteAsPlatformAdminAsync(() =>
            _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinicId)
                .ToListAsync(cancellationToken),
            reason: "SuperAdmin Get Clinic Modules",
            actorId: "platform-admin",
            cancellationToken: cancellationToken);

        return Result<IReadOnlyList<ClinicModuleStateResult>>.Success(BuildModuleStates(rows));
    }

    public async Task<Result<IReadOnlyList<ClinicModuleStateResult>>> UpdateClinicModulesAsync(
        Guid adminId,
        Guid clinicId,
        IReadOnlyList<ModuleToggleInput> modules,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var stepUp = await EnsureStepUpAsync(adminId, stepUpPassword, ipAddress, requestId, cancellationToken);
        if (!stepUp.IsSuccess)
        {
            return Result<IReadOnlyList<ClinicModuleStateResult>>.Failure(stepUp.Error);
        }

        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);

        if (clinic == null)
        {
            return Result<IReadOnlyList<ClinicModuleStateResult>>.Failure(
                Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var rows = await _platformAccess.ExecuteAsPlatformAdminAsync(() =>
            _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinicId)
                .ToListAsync(cancellationToken),
            reason: "SuperAdmin Load Clinic Modules",
            actorId: adminId.ToString(),
            cancellationToken: cancellationToken);

        var currentlyEnabled = rows
            .Where(r => r.Enabled)
            .Select(r => r.ModuleKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var validation = _moduleCatalog.ValidateModuleChanges(currentlyEnabled, modules);
        if (validation.IsFailure)
        {
            return Result<IReadOnlyList<ClinicModuleStateResult>>.Failure(validation.Error);
        }

        var oldState = rows
            .Where(r => r.Enabled)
            .Select(r => r.ModuleKey)
            .OrderBy(k => k)
            .ToList();

        DateTimeOffset now = _clock.UtcNow;
        await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            var existingByKey = rows.ToDictionary(r => r.ModuleKey, StringComparer.OrdinalIgnoreCase);

            foreach (var input in modules)
            {
                string key = input.ModuleKey.Trim().ToLowerInvariant();
                if (existingByKey.TryGetValue(key, out var existing))
                {
                    existing.SetEnabled(input.Enabled, adminId, now, input.Note);
                }
                else
                {
                    var created = new ClinicModule(
                        GuidV7.NewGuid(),
                        clinicId,
                        key,
                        input.Enabled,
                        adminId,
                        now,
                        input.Note);
                    _dbContext.ClinicModules.Add(created);
                    rows.Add(created);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, reason: "SuperAdmin Update Clinic Modules", actorId: adminId.ToString(), cancellationToken: cancellationToken);

        var newState = rows
            .Where(r => r.Enabled)
            .Select(r => r.ModuleKey)
            .OrderBy(k => k)
            .ToList();

        await _outbox.EnqueueAsync(
            "ClinicModuleChanged",
            JsonSerializer.Serialize(new
            {
                clinicId,
                changedByAdminId = adminId,
                enabledModules = newState,
                changedAtUtc = now
            }),
            cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicModulesUpdated",
            entityName: "ClinicModule",
            entityId: clinicId.ToString(),
            oldValues: JsonSerializer.Serialize(new { enabledModules = oldState }),
            newValues: JsonSerializer.Serialize(new { enabledModules = newState }),
            userId: adminId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<IReadOnlyList<ClinicModuleStateResult>>.Success(BuildModuleStates(rows));
    }

    private List<ClinicModuleStateResult> BuildModuleStates(IReadOnlyList<ClinicModule> rows)
    {
        var rowByKey = rows.ToDictionary(r => r.ModuleKey, StringComparer.OrdinalIgnoreCase);
        var enabledSet = rows
            .Where(r => r.Enabled)
            .Select(r => r.ModuleKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _moduleCatalog.GetSwitchableModules()
            .Select(def =>
            {
                rowByKey.TryGetValue(def.ModuleKey, out var row);
                bool enabled = row?.Enabled ?? false;
                var missingPrereqs = def.Prerequisites
                    .Where(p => !enabledSet.Contains(p))
                    .ToList();
                var enabledDependents = _moduleCatalog.GetDirectDependents(def.ModuleKey)
                    .Where(d => enabledSet.Contains(d))
                    .ToList();

                return new ClinicModuleStateResult(
                    ModuleKey: def.ModuleKey,
                    TitleAr: def.TitleAr,
                    TitleEn: def.TitleEn,
                    DescriptionAr: def.DescriptionAr,
                    DescriptionEn: def.DescriptionEn,
                    Enabled: enabled,
                    Prerequisites: def.Prerequisites,
                    MissingPrerequisites: missingPrereqs,
                    EnabledDependents: enabledDependents,
                    ChangedAtUtc: row?.ChangedAtUtc,
                    Note: row?.Note);
            })
            .ToList();
    }

    private async Task<Result> EnsureStepUpAsync(
        Guid adminId,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(stepUpPassword))
        {
            return await _adminAuthService.VerifyStepUpAsync(adminId, stepUpPassword, ipAddress, requestId, cancellationToken);
        }

        var admin = await _dbContext.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Id == adminId, cancellationToken);

        if (admin != null && admin.HasValidStepUp(_clock.UtcNow, StepUpWindow))
        {
            return Result.Success();
        }

        return Result.Failure(Error.Forbidden("STEP_UP_REQUIRED", "Step-up password confirmation is required for this sensitive action."));
    }

    private static ClinicSummaryResult MapClinicSummary(Clinic clinic, IReadOnlyList<string> enabledModules)
    {
        return new ClinicSummaryResult(
            ClinicId: clinic.Id,
            Name: clinic.Name,
            Slug: clinic.Slug,
            Status: clinic.Status,
            SubscriptionExpiryUtc: clinic.SubscriptionExpiryUtc,
            GraceDays: clinic.GraceDays,
            IsActive: clinic.IsActive,
            CreatedAtUtc: clinic.CreatedAtUtc,
            ModulesEnabled: enabledModules);
    }
}
