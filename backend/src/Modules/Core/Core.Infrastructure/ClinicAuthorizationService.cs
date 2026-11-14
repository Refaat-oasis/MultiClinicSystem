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
/// Manages clinic-scoped roles, role permissions, per-user overrides, staff invitations,
/// device approval enforcement, and effective permission calculation.
/// </summary>
public sealed class ClinicAuthorizationService : IClinicAuthorizationService
{
    private readonly CoreDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IModuleCatalogService _moduleCatalog;
    private readonly ISettingsSectionRegistry _settingsSectionRegistry;
    private readonly IAccountInvitations _accountInvitations;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ClinicAuthorizationService(
        CoreDbContext dbContext,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        CurrentClinicContext currentClinicContext,
        IModuleCatalogService moduleCatalog,
        ISettingsSectionRegistry settingsSectionRegistry,
        IAccountInvitations accountInvitations,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
        _settingsSectionRegistry = settingsSectionRegistry ?? throw new ArgumentNullException(nameof(settingsSectionRegistry));
        _accountInvitations = accountInvitations ?? throw new ArgumentNullException(nameof(accountInvitations));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IReadOnlyList<PermissionDefinitionResult> GetPermissionCatalog()
    {
        return _moduleCatalog.GetPermissionCatalog();
    }

    public async Task<Result<MyModulesManifestResult>> GetMyModulesManifestAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: null, requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<MyModulesManifestResult>.Failure(gate.Error);
        }

        var moduleStates = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var rows = await _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinicId)
                .ToListAsync(cancellationToken);

            var rowByKey = rows.ToDictionary(r => r.ModuleKey, StringComparer.OrdinalIgnoreCase);
            var enabledSet = rows.Where(r => r.Enabled).Select(r => r.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _moduleCatalog.GetSwitchableModules()
                .Select(def =>
                {
                    rowByKey.TryGetValue(def.ModuleKey, out var row);
                    bool enabled = row?.Enabled ?? false;
                    var missingPrereqs = def.Prerequisites.Where(p => !enabledSet.Contains(p)).ToList();
                    var enabledDependents = _moduleCatalog.GetDirectDependents(def.ModuleKey)
                        .Where(d => enabledSet.Contains(d))
                        .ToList();

                    return new ClinicModuleStateResult(
                        def.ModuleKey,
                        def.TitleAr,
                        def.TitleEn,
                        def.DescriptionAr,
                        def.DescriptionEn,
                        enabled,
                        def.Prerequisites,
                        missingPrereqs,
                        enabledDependents,
                        row?.ChangedAtUtc,
                        row?.Note);
                })
                .ToList();
        });

        var enabledKeys = gate.Value.EffectivePermissionsResult.EnabledModules;
        var effectiveSet = gate.Value.EffectivePermissionsResult.EffectivePermissions
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var menuItems = _moduleCatalog.GetMenuItemsForModules(enabledKeys, effectiveSet);
        var sections = _settingsSectionRegistry
            .GetAvailableSections(enabledKeys)
            .Where(s => effectiveSet.Contains(s.RequiredPermission))
            .ToList();

        return Result<MyModulesManifestResult>.Success(new MyModulesManifestResult(
            ClinicId: clinicId,
            Modules: moduleStates,
            MenuItems: menuItems,
            SettingsSections: sections,
            EffectivePermissions: gate.Value.EffectivePermissionsResult.EffectivePermissions));
    }

    public async Task<Result<UserEffectivePermissionsResult>> GetUserEffectivePermissionsAsync(
        Guid userId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        if (clinic == null)
        {
            return Result<UserEffectivePermissionsResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);
        bool isOwner = owner != null && clinic.OwnerId == owner.Id;

        var membership = await _dbContext.UserClinics
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ClinicId == clinicId, cancellationToken);

        if (!isOwner && membership == null)
        {
            return Result<UserEffectivePermissionsResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        string roleName = isOwner ? "Owner" : (membership?.Role ?? "Staff");
        var evalResult = await EvaluatePermissionsInternalAsync(clinicId, userId, roleName, isOwner, cancellationToken);
        return Result<UserEffectivePermissionsResult>.Success(evalResult);
    }

    public async Task<Result> EnsureClinicAccessAsync(
        Guid userId,
        Guid clinicId,
        string? requiredPermission,
        string? requiredModuleKey,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission, requiredModuleKey, deviceId, requireCompletedOnboarding: false, cancellationToken);
        return gate.IsSuccess ? Result.Success() : Result.Failure(gate.Error);
    }

    public async Task<Result<IReadOnlyList<ClinicRoleResult>>> ListRolesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "dashboard.view", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicRoleResult>>.Failure(gate.Error);
        }

        var roles = await LoadClinicRolesAsync(clinicId, gate.Value.EffectivePermissionsResult.EnabledModules, cancellationToken);
        return Result<IReadOnlyList<ClinicRoleResult>>.Success(roles);
    }

    public async Task<Result<ClinicRoleResult>> CreateRoleAsync(
        Guid userId,
        Guid clinicId,
        string name,
        string? nameAr,
        string? nameEn,
        IReadOnlyList<string> permissions,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "roles.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicRoleResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<ClinicRoleResult>.Failure(Error.Validation("ROLE_NAME_REQUIRED", "Role name is required."));
        }

        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);

        string cleanName = name.Trim();
        var allKnown = _moduleCatalog.GetPermissionCatalog().Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actorEffective = gate.Value.EffectivePermissionsResult.EffectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var validation = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
            isSystemOwnerRole: false,
            requestedPermissions: permissions ?? Array.Empty<string>(),
            allKnownPermissions: allKnown,
            actorEffectivePermissions: actorEffective);

        if (validation.IsFailure)
        {
            return Result<ClinicRoleResult>.Failure(validation.Error);
        }

        Guid roleId = GuidV7.NewGuid();
        DateTimeOffset now = _clock.UtcNow;

        bool duplicateExists = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicRoles.AnyAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Name == cleanName, cancellationToken));

        if (duplicateExists)
        {
            return Result<ClinicRoleResult>.Failure(Error.Conflict("ROLE_ALREADY_EXISTS", $"Role '{cleanName}' already exists in this clinic."));
        }

        var role = new ClinicRole(
            roleId,
            clinicId,
            cleanName,
            nameAr ?? cleanName,
            nameEn ?? cleanName,
            templateKey: null,
            isSystemOwner: false,
            isEditable: true,
            createdAtUtc: now);

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            _dbContext.ClinicRoles.Add(role);
            foreach (string perm in (permissions ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(
                    GuidV7.NewGuid(),
                    clinicId,
                    roleId,
                    perm,
                    now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await EnsureIdentityRoleExistsAsync(cleanName);

        await _auditService.LogAsync(
            action: "ClinicRoleCreated",
            entityName: "ClinicRole",
            entityId: roleId.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { role.Name, permissions }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        var created = (await LoadClinicRolesAsync(clinicId, gate.Value.EffectivePermissionsResult.EnabledModules, cancellationToken))
            .First(r => r.RoleId == roleId);
        return Result<ClinicRoleResult>.Success(created);
    }

    public async Task<Result<ClinicRoleResult>> CloneRoleAsync(
        Guid userId,
        Guid clinicId,
        Guid sourceRoleId,
        string newName,
        string? newNameAr,
        string? newNameEn,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "roles.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicRoleResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result<ClinicRoleResult>.Failure(Error.Validation("ROLE_NAME_REQUIRED", "New role name is required."));
        }

        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);

        string cleanName = newName.Trim();
        Guid clonedId = GuidV7.NewGuid();
        DateTimeOffset now = _clock.UtcNow;

        var cloneCheck = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var source = await _dbContext.ClinicRoles
                .FirstOrDefaultAsync(r => r.Id == sourceRoleId && r.ClinicId == clinicId && !r.IsVoided, cancellationToken);

            if (source == null)
            {
                return Result<List<string>>.Failure(Error.NotFound("ROLE_NOT_FOUND", "Source role not found."));
            }

            bool duplicateExists = await _dbContext.ClinicRoles
                .AnyAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Name == cleanName, cancellationToken);

            if (duplicateExists)
            {
                return Result<List<string>>.Failure(Error.Conflict("ROLE_ALREADY_EXISTS", $"Role '{cleanName}' already exists."));
            }

            var rawSourcePerms = await _dbContext.ClinicRolePermissions
                .Where(p => p.ClinicId == clinicId && p.ClinicRoleId == sourceRoleId)
                .Select(p => p.PermissionKey)
                .ToListAsync(cancellationToken);

            var sourcePerms = rawSourcePerms
                .Where(p => !EffectivePermissionsEvaluator.ReservedOwnerPermissions.Contains(p))
                .ToList();

            var clonedRole = new ClinicRole(
                clonedId,
                clinicId,
                cleanName,
                newNameAr ?? cleanName,
                newNameEn ?? cleanName,
                templateKey: source.TemplateKey ?? source.Name,
                isSystemOwner: false,
                isEditable: true,
                createdAtUtc: now);

            _dbContext.ClinicRoles.Add(clonedRole);
            foreach (string perm in sourcePerms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(
                    GuidV7.NewGuid(),
                    clinicId,
                    clonedId,
                    perm,
                    now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<List<string>>.Success(sourcePerms);
        });

        if (cloneCheck.IsFailure)
        {
            return Result<ClinicRoleResult>.Failure(cloneCheck.Error);
        }

        await EnsureIdentityRoleExistsAsync(cleanName);

        await _auditService.LogAsync(
            action: "ClinicRoleCloned",
            entityName: "ClinicRole",
            entityId: clonedId.ToString(),
            oldValues: JsonSerializer.Serialize(new { sourceRoleId }),
            newValues: JsonSerializer.Serialize(new { name = cleanName, permissions = cloneCheck.Value }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        var cloned = (await LoadClinicRolesAsync(clinicId, gate.Value.EffectivePermissionsResult.EnabledModules, cancellationToken))
            .First(r => r.RoleId == clonedId);
        return Result<ClinicRoleResult>.Success(cloned);
    }

    public async Task<Result<ClinicRoleResult>> UpdateRolePermissionsAsync(
        Guid userId,
        Guid clinicId,
        Guid roleId,
        IReadOnlyList<string> permissions,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "roles.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicRoleResult>.Failure(gate.Error);
        }

        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);

        var roleInfo = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var role = await _dbContext.ClinicRoles
                .FirstOrDefaultAsync(r => r.Id == roleId && r.ClinicId == clinicId && !r.IsVoided, cancellationToken);
            return role;
        });

        if (roleInfo == null)
        {
            return Result<ClinicRoleResult>.Failure(Error.NotFound("ROLE_NOT_FOUND", "Role not found."));
        }

        var allKnown = _moduleCatalog.GetPermissionCatalog().Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actorEffective = gate.Value.EffectivePermissionsResult.EffectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var validation = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
            roleInfo.IsSystemOwner,
            permissions ?? Array.Empty<string>(),
            allKnown,
            actorEffective);

        if (validation.IsFailure)
        {
            return Result<ClinicRoleResult>.Failure(validation.Error);
        }

        DateTimeOffset now = _clock.UtcNow;
        List<string> oldPerms = new();

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            var existingPerms = await _dbContext.ClinicRolePermissions
                .Where(p => p.ClinicId == clinicId && p.ClinicRoleId == roleId)
                .ToListAsync(cancellationToken);

            oldPerms = existingPerms.Select(p => p.PermissionKey).OrderBy(k => k).ToList();
            _dbContext.ClinicRolePermissions.RemoveRange(existingPerms);

            foreach (string perm in (permissions ?? Array.Empty<string>()).Select(p => p.Trim().ToLowerInvariant()).Distinct())
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(
                    GuidV7.NewGuid(),
                    clinicId,
                    roleId,
                    perm,
                    now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await _auditService.LogAsync(
            action: "ClinicRolePermissionsUpdated",
            entityName: "ClinicRole",
            entityId: roleId.ToString(),
            oldValues: JsonSerializer.Serialize(new { permissions = oldPerms }),
            newValues: JsonSerializer.Serialize(new { permissions = permissions ?? Array.Empty<string>() }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        var updated = (await LoadClinicRolesAsync(clinicId, gate.Value.EffectivePermissionsResult.EnabledModules, cancellationToken))
            .First(r => r.RoleId == roleId);
        return Result<ClinicRoleResult>.Success(updated);
    }

    public async Task<Result> DeleteRoleAsync(
        Guid userId,
        Guid clinicId,
        Guid roleId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "roles.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var result = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var role = await _dbContext.ClinicRoles
                .FirstOrDefaultAsync(r => r.Id == roleId && r.ClinicId == clinicId && !r.IsVoided, cancellationToken);

            if (role == null)
            {
                return Result.Failure(Error.NotFound("ROLE_NOT_FOUND", "Role not found."));
            }

            if (role.IsSystemOwner)
            {
                return Result.Failure(Error.Forbidden("SYSTEM_ROLE_PROTECTED", "The Owner system role cannot be deleted."));
            }

            bool inUse = await _dbContext.UserClinics
                .AnyAsync(uc => uc.ClinicId == clinicId && uc.Role == role.Name, cancellationToken);

            if (inUse)
            {
                return Result.Failure(Error.Conflict("ROLE_IN_USE", "Cannot delete a role while staff members are assigned to it."));
            }

            role.Void(_clock.UtcNow, userId.ToString(), "Deleted by authorized user");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        });

        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "ClinicRoleDeleted",
                entityName: "ClinicRole",
                entityId: roleId.ToString(),
                oldValues: null,
                newValues: null,
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return result;
    }

    public async Task<Result<IReadOnlyList<StaffMemberResult>>> ListStaffAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "users.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<StaffMemberResult>>.Failure(gate.Error);
        }

        var members = await LoadStaffMembersAsync(clinicId, cancellationToken);
        return Result<IReadOnlyList<StaffMemberResult>>.Success(members);
    }

    public async Task<Result<StaffMemberResult>> InviteStaffAsync(
        Guid userId,
        Guid clinicId,
        string fullName,
        string email,
        string roleName,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "users.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<StaffMemberResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Result<StaffMemberResult>.Failure(Error.Validation("ROLE_REQUIRED", "Role name is required."));
        }

        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);
        string cleanRole = roleName.Trim();

        if (string.Equals(cleanRole, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return Result<StaffMemberResult>.Failure(Error.Forbidden("RESERVED_PERMISSION_FORBIDDEN", "Cannot invite an additional Owner role via staff invitation."));
        }

        var clinicRole = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicRoles
                .FirstOrDefaultAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Name == cleanRole, cancellationToken));

        if (clinicRole == null)
        {
            return Result<StaffMemberResult>.Failure(Error.NotFound("ROLE_NOT_FOUND", $"Clinic role '{cleanRole}' was not found."));
        }

        // Delegation ceiling: actor must hold all active permissions of the role being assigned (or check at least user cannot elevate)
        var rolePerms = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicRolePermissions
                .Where(p => p.ClinicId == clinicId && p.ClinicRoleId == clinicRole.Id)
                .Select(p => p.PermissionKey)
                .ToListAsync(cancellationToken));

        var actorEffective = gate.Value.EffectivePermissionsResult.EffectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activePermissions = _moduleCatalog.GetActivePermissionsForModules(gate.Value.EffectivePermissionsResult.EnabledModules);
        foreach (string perm in rolePerms.Where(p => activePermissions.Contains(p)))
        {
            if (!actorEffective.Contains(perm))
            {
                return Result<StaffMemberResult>.Failure(Error.Forbidden(
                    "DELEGATION_CEILING_EXCEEDED",
                    $"Cannot invite staff into role '{cleanRole}' containing permission '{perm}' that you do not hold."));
            }
        }

        await EnsureIdentityRoleExistsAsync(clinicRole.Name);

        var inviteResult = await _accountInvitations.InviteAsync(
            "Staff",
            email,
            fullName,
            clinicRole.Name,
            clinicId,
            cancellationToken);

        if (inviteResult.IsFailure)
        {
            return Result<StaffMemberResult>.Failure(inviteResult.Error);
        }

        await _auditService.LogAsync(
            action: "ClinicStaffInvited",
            entityName: "AppUser",
            entityId: inviteResult.Value.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { email, roleName = clinicRole.Name }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        var members = await LoadStaffMembersAsync(clinicId, cancellationToken);
        return Result<StaffMemberResult>.Success(members.First(m => m.UserId == inviteResult.Value));
    }

    public async Task<Result<StaffMemberResult>> AssignStaffRoleAsync(
        Guid userId,
        Guid clinicId,
        Guid targetStaffUserId,
        string roleName,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "users.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<StaffMemberResult>.Failure(gate.Error);
        }

        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);
        string cleanRole = roleName.Trim();

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        var ownerRecord = clinic?.OwnerId != null
            ? await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == clinic.OwnerId.Value, cancellationToken)
            : null;

        if (ownerRecord != null && ownerRecord.UserId == targetStaffUserId && !string.Equals(cleanRole, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return Result<StaffMemberResult>.Failure(Error.Conflict("LAST_OWNER_PROTECTED", "The primary clinic owner cannot be demoted."));
        }

        if (string.Equals(cleanRole, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return Result<StaffMemberResult>.Failure(Error.Forbidden("RESERVED_PERMISSION_FORBIDDEN", "Cannot assign Owner role to staff."));
        }

        var clinicRole = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicRoles
                .FirstOrDefaultAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Name == cleanRole, cancellationToken));

        if (clinicRole == null)
        {
            return Result<StaffMemberResult>.Failure(Error.NotFound("ROLE_NOT_FOUND", $"Role '{cleanRole}' not found."));
        }

        var membership = await _dbContext.UserClinics
            .FirstOrDefaultAsync(uc => uc.ClinicId == clinicId && uc.UserId == targetStaffUserId, cancellationToken);

        if (membership == null)
        {
            return Result<StaffMemberResult>.Failure(Error.NotFound("STAFF_NOT_FOUND", "Staff member not found in this clinic."));
        }

        string oldRole = membership.Role;
        _dbContext.UserClinics.Remove(membership);
        _dbContext.UserClinics.Add(new UserClinic(GuidV7.NewGuid(), targetStaffUserId, clinicId, clinicRole.Name, _clock.UtcNow));
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureIdentityRoleExistsAsync(clinicRole.Name);
        var targetUser = await _userManager.FindByIdAsync(targetStaffUserId.ToString());
        if (targetUser != null)
        {
            if (!await _userManager.IsInRoleAsync(targetUser, clinicRole.Name))
            {
                await _userManager.AddToRoleAsync(targetUser, clinicRole.Name);
            }
        }

        await _auditService.LogAsync(
            action: "ClinicStaffRoleUpdated",
            entityName: "UserClinic",
            entityId: targetStaffUserId.ToString(),
            oldValues: JsonSerializer.Serialize(new { role = oldRole }),
            newValues: JsonSerializer.Serialize(new { role = clinicRole.Name }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        var members = await LoadStaffMembersAsync(clinicId, cancellationToken);
        return Result<StaffMemberResult>.Success(members.First(m => m.UserId == targetStaffUserId));
    }

    public async Task<Result<StaffMemberResult>> SetUserPermissionOverridesAsync(
        Guid userId,
        Guid clinicId,
        Guid targetStaffUserId,
        IReadOnlyList<PermissionOverrideInput> overrides,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "roles.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<StaffMemberResult>.Failure(gate.Error);
        }

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        var ownerRecord = clinic?.OwnerId != null
            ? await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == clinic.OwnerId.Value, cancellationToken)
            : null;
        bool targetIsOwner = ownerRecord != null && ownerRecord.UserId == targetStaffUserId;

        bool targetMemberExists = targetIsOwner || await _dbContext.UserClinics
            .AnyAsync(uc => uc.ClinicId == clinicId && uc.UserId == targetStaffUserId, cancellationToken);

        if (!targetMemberExists)
        {
            return Result<StaffMemberResult>.Failure(Error.NotFound("STAFF_NOT_FOUND", "Staff member not found in this clinic."));
        }

        var allKnown = _moduleCatalog.GetPermissionCatalog().Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actorEffective = gate.Value.EffectivePermissionsResult.EffectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in overrides ?? Array.Empty<PermissionOverrideInput>())
        {
            string perm = item.PermissionKey?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!allKnown.Contains(perm))
            {
                return Result<StaffMemberResult>.Failure(Error.Validation("UNKNOWN_PERMISSION", $"Unknown permission '{perm}'."));
            }

            if (string.Equals(item.Effect, "Grant", StringComparison.OrdinalIgnoreCase))
            {
                var grantValidation = EffectivePermissionsEvaluator.ValidateUserGrant(perm, allKnown, actorEffective, targetIsOwner);
                if (grantValidation.IsFailure)
                {
                    return Result<StaffMemberResult>.Failure(grantValidation.Error);
                }
            }
            else if (!string.Equals(item.Effect, "Deny", StringComparison.OrdinalIgnoreCase))
            {
                return Result<StaffMemberResult>.Failure(Error.Validation("INVALID_OVERRIDE_EFFECT", "Override effect must be 'Grant' or 'Deny'."));
            }
        }

        DateTimeOffset now = _clock.UtcNow;
        await ExecuteForClinicAsync(clinicId, async () =>
        {
            var existing = await _dbContext.UserPermissionOverrides
                .Where(o => o.ClinicId == clinicId && o.UserId == targetStaffUserId)
                .ToListAsync(cancellationToken);

            _dbContext.UserPermissionOverrides.RemoveRange(existing);

            foreach (var item in overrides ?? Array.Empty<PermissionOverrideInput>())
            {
                _dbContext.UserPermissionOverrides.Add(new UserPermissionOverride(
                    GuidV7.NewGuid(),
                    clinicId,
                    targetStaffUserId,
                    item.PermissionKey.Trim().ToLowerInvariant(),
                    item.Effect,
                    userId,
                    now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await _auditService.LogAsync(
            action: "UserPermissionOverridesUpdated",
            entityName: "UserPermissionOverride",
            entityId: targetStaffUserId.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { overrides }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        var members = await LoadStaffMembersAsync(clinicId, cancellationToken);
        return Result<StaffMemberResult>.Success(members.First(m => m.UserId == targetStaffUserId));
    }

    public async Task<Result> RemoveStaffAsync(
        Guid userId,
        Guid clinicId,
        Guid targetStaffUserId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "users.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        var ownerRecord = clinic?.OwnerId != null
            ? await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == clinic.OwnerId.Value, cancellationToken)
            : null;

        if (ownerRecord != null && ownerRecord.UserId == targetStaffUserId)
        {
            return Result.Failure(Error.Conflict("LAST_OWNER_PROTECTED", "The primary owner of a clinic cannot be removed."));
        }

        var membership = await _dbContext.UserClinics
            .FirstOrDefaultAsync(uc => uc.ClinicId == clinicId && uc.UserId == targetStaffUserId, cancellationToken);

        if (membership == null)
        {
            return Result.Failure(Error.NotFound("STAFF_NOT_FOUND", "Staff member not found in this clinic."));
        }

        _dbContext.UserClinics.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "ClinicStaffRemoved",
            entityName: "UserClinic",
            entityId: targetStaffUserId.ToString(),
            oldValues: JsonSerializer.Serialize(new { membership.Role }),
            newValues: null,
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ClinicDeviceItemResult>>> ListClinicDevicesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "users.manage", requiredModuleKey: null, deviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicDeviceItemResult>>.Failure(gate.Error);
        }

        var memberIds = await _dbContext.UserClinics
            .Where(uc => uc.ClinicId == clinicId)
            .Select(uc => uc.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var users = await _dbContext.Users
            .Where(u => memberIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var devices = await _dbContext.AppDevices
            .Where(d => memberIds.Contains(d.UserId))
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var list = devices.Select(d =>
        {
            users.TryGetValue(d.UserId, out var u);
            return new ClinicDeviceItemResult(
                d.Id,
                d.UserId,
                u?.FullName ?? string.Empty,
                u?.Email ?? string.Empty,
                d.DeviceId,
                d.DeviceName,
                d.IsApproved,
                d.IsRevoked,
                d.ApprovedAtUtc,
                d.RevokedAtUtc,
                d.CreatedAtUtc);
        }).ToList();

        return Result<IReadOnlyList<ClinicDeviceItemResult>>.Success(list);
    }

    public async Task<Result> ApproveClinicDeviceAsync(
        Guid userId,
        Guid clinicId,
        string targetDeviceId,
        string? callerDeviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await AuthorizeClinicUserAsync(userId, clinicId, requiredPermission: "users.manage", requiredModuleKey: null, callerDeviceId, requireCompletedOnboarding: false, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            return Result.Failure(Error.Validation("DEVICE_ID_REQUIRED", "Device ID is required."));
        }

        var memberIds = await _dbContext.UserClinics
            .Where(uc => uc.ClinicId == clinicId)
            .Select(uc => uc.UserId)
            .ToListAsync(cancellationToken);

        string cleanTarget = targetDeviceId.Trim();
        var device = await _dbContext.AppDevices
            .Where(d => d.DeviceId == cleanTarget && memberIds.Contains(d.UserId))
            .OrderByDescending(d => d.UserId != userId)
            .ThenBy(d => d.IsApproved)
            .ThenByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (device == null)
        {
            return Result.Failure(Error.NotFound("DEVICE_NOT_FOUND", "Device not found for any member of this clinic."));
        }

        device.Approve(_clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "DeviceApproved",
            entityName: "AppDevice",
            entityId: device.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { targetUserId = device.UserId, deviceId = device.DeviceId }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    private async Task<Result<(Clinic Clinic, bool IsOwner, UserEffectivePermissionsResult EffectivePermissionsResult)>> AuthorizeClinicUserAsync(
        Guid userId,
        Guid clinicId,
        string? requiredPermission,
        string? requiredModuleKey,
        string? deviceId,
        bool requireCompletedOnboarding,
        CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        if (clinic == null)
        {
            return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        var owner = await _dbContext.Owners.FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);
        bool isOwner = owner != null && clinic.OwnerId == owner.Id;

        var membership = await _dbContext.UserClinics
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ClinicId == clinicId, cancellationToken);

        if (!isOwner && membership == null)
        {
            return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic not found."));
        }

        if (!clinic.IsActive || string.Equals(clinic.Status, "Suspended", StringComparison.OrdinalIgnoreCase))
        {
            return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.Forbidden("CLINIC_SUSPENDED", "Clinic is suspended."));
        }

        if (requireCompletedOnboarding && !string.Equals(clinic.OnboardingStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.Conflict("ONBOARDING_REQUIRED", "Complete clinic onboarding first."));
        }

        // Enforce device approval for non-owner staff members
        if (!isOwner)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.Forbidden("DEVICE_APPROVAL_REQUIRED", "Device approval is required for staff access."));
            }

            var device = await _dbContext.AppDevices
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId.Trim(), cancellationToken);

            if (device == null || device.IsRevoked || !device.IsApproved)
            {
                return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.Forbidden(
                    "DEVICE_APPROVAL_REQUIRED",
                    "This staff device is pending approval by the clinic owner."));
            }
        }

        string roleName = isOwner ? "Owner" : (membership?.Role ?? "Staff");
        var evalResult = await EvaluatePermissionsInternalAsync(clinicId, userId, roleName, isOwner, cancellationToken);

        if (!string.IsNullOrWhiteSpace(requiredModuleKey) &&
            !string.Equals(requiredModuleKey, "core", StringComparison.OrdinalIgnoreCase))
        {
            bool moduleEnabled = evalResult.EnabledModules.Contains(requiredModuleKey, StringComparer.OrdinalIgnoreCase);
            if (!moduleEnabled)
            {
                return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.Forbidden(
                    "MODULE_DISABLED",
                    $"Module '{requiredModuleKey}' is not enabled for this clinic."));
            }
        }

        if (!string.IsNullOrWhiteSpace(requiredPermission))
        {
            bool hasPerm = evalResult.EffectivePermissions.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase);
            if (!hasPerm)
            {
                return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Failure(Error.Forbidden(
                    "FORBIDDEN_PERMISSION",
                    $"Missing required permission '{requiredPermission}'."));
            }
        }

        return Result<(Clinic, bool, UserEffectivePermissionsResult)>.Success((clinic, isOwner, evalResult));
    }

    private async Task<UserEffectivePermissionsResult> EvaluatePermissionsInternalAsync(
        Guid clinicId,
        Guid userId,
        string roleName,
        bool isOwner,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            var enabledModules = await _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinicId && m.Enabled)
                .OrderBy(m => m.ModuleKey)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);

            var activePermissions = _moduleCatalog.GetActivePermissionsForModules(enabledModules);

            var role = await _dbContext.ClinicRoles
                .FirstOrDefaultAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Name == roleName, cancellationToken);

            var rolePerms = role == null
                ? new List<string>()
                : await _dbContext.ClinicRolePermissions
                    .Where(p => p.ClinicId == clinicId && p.ClinicRoleId == role.Id)
                    .Select(p => p.PermissionKey)
                    .ToListAsync(cancellationToken);

            var overrides = await _dbContext.UserPermissionOverrides
                .Where(o => o.ClinicId == clinicId && o.UserId == userId)
                .ToListAsync(cancellationToken);

            var grants = overrides.Where(o => string.Equals(o.Effect, "Grant", StringComparison.OrdinalIgnoreCase)).Select(o => o.PermissionKey);
            var denies = overrides.Where(o => string.Equals(o.Effect, "Deny", StringComparison.OrdinalIgnoreCase)).Select(o => o.PermissionKey);

            var evaluation = EffectivePermissionsEvaluator.Evaluate(isOwner, rolePerms, grants, denies, activePermissions);

            return new UserEffectivePermissionsResult(
                ClinicId: clinicId,
                UserId: userId,
                RoleName: roleName,
                IsOwner: isOwner,
                EffectivePermissions: evaluation.EffectivePermissions,
                DormantPermissions: evaluation.DormantPermissions,
                EnabledModules: enabledModules);
        });
    }

    private async Task EnsureDefaultRolesSeededAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool anyRole = await _dbContext.ClinicRoles
                .AnyAsync(r => r.ClinicId == clinicId, cancellationToken);

            if (anyRole)
            {
                return 0;
            }

            DateTimeOffset now = _clock.UtcNow;
            var allCatalogPerms = _moduleCatalog.GetPermissionCatalog().Select(p => p.Key).ToList();

            var ownerRole = new ClinicRole(GuidV7.NewGuid(), clinicId, "Owner", "المالك", "Owner", "Owner", isSystemOwner: true, isEditable: false, now);
            var doctorRole = new ClinicRole(GuidV7.NewGuid(), clinicId, "Doctor", "طبيب", "Doctor", "Doctor", isSystemOwner: false, isEditable: true, now);
            var assistantRole = new ClinicRole(GuidV7.NewGuid(), clinicId, "Assistant", "مساعد طبيب", "Assistant", "Assistant", isSystemOwner: false, isEditable: true, now);
            var receptionistRole = new ClinicRole(GuidV7.NewGuid(), clinicId, "Receptionist", "استقبال", "Receptionist", "Receptionist", isSystemOwner: false, isEditable: true, now);

            _dbContext.ClinicRoles.AddRange(ownerRole, doctorRole, assistantRole, receptionistRole);

            foreach (string perm in allCatalogPerms)
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(GuidV7.NewGuid(), clinicId, ownerRole.Id, perm, now));
            }

            string[] doctorPerms =
            [
                "dashboard.view", "patients.read", "patients.history.read", "patients.history.write",
                "attachments.read", "attachments.write", "scheduling.read", "scheduling.write",
                "clinical.read", "clinical.write", "treatment_plans.read", "treatment_plans.write", "time_engine.read"
            ];
            foreach (string perm in doctorPerms)
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(GuidV7.NewGuid(), clinicId, doctorRole.Id, perm, now));
            }

            string[] assistantPerms =
            [
                "dashboard.view", "patients.read", "attachments.read", "attachments.write",
                "scheduling.read", "queue.read", "queue.manage", "inventory.read"
            ];
            foreach (string perm in assistantPerms)
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(GuidV7.NewGuid(), clinicId, assistantRole.Id, perm, now));
            }

            string[] receptionistPerms =
            [
                "dashboard.view", "patients.read", "patients.write",
                "scheduling.read", "scheduling.write", "queue.read", "queue.manage",
                "billing.read", "billing.write"
            ];
            foreach (string perm in receptionistPerms)
            {
                _dbContext.ClinicRolePermissions.Add(new ClinicRoleClaim(GuidV7.NewGuid(), clinicId, receptionistRole.Id, perm, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });
    }

    private async Task<IReadOnlyList<ClinicRoleResult>> LoadClinicRolesAsync(
        Guid clinicId,
        IReadOnlyList<string> enabledModules,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);

        var activePerms = _moduleCatalog.GetActivePermissionsForModules(enabledModules);

        var memberships = await _dbContext.UserClinics
            .Where(uc => uc.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            var roles = await _dbContext.ClinicRoles
                .Where(r => r.ClinicId == clinicId && !r.IsVoided)
                .OrderByDescending(r => r.IsSystemOwner)
                .ThenBy(r => r.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var rolePerms = await _dbContext.ClinicRolePermissions
                .Where(rp => rp.ClinicId == clinicId)
                .ToListAsync(cancellationToken);

            var permsByRole = rolePerms
                .GroupBy(rp => rp.ClinicRoleId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.PermissionKey).OrderBy(k => k).ToList());

            return roles.Select(r =>
            {
                var raw = permsByRole.TryGetValue(r.Id, out var list) ? list : new List<string>();
                var eval = EffectivePermissionsEvaluator.Evaluate(
                    r.IsSystemOwner,
                    raw,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    activePerms);

                int count = memberships.Count(m => string.Equals(m.Role, r.Name, StringComparison.OrdinalIgnoreCase));

                return new ClinicRoleResult(
                    RoleId: r.Id,
                    ClinicId: clinicId,
                    Name: r.Name,
                    NameAr: r.NameAr,
                    NameEn: r.NameEn,
                    TemplateKey: r.TemplateKey,
                    IsSystemOwner: r.IsSystemOwner,
                    IsEditable: r.IsEditable,
                    AssignedUsersCount: count,
                    Permissions: raw,
                    EffectivePermissions: eval.EffectivePermissions,
                    DormantPermissions: eval.DormantPermissions);
            }).ToList();
        });
    }

    private async Task<IReadOnlyList<StaffMemberResult>> LoadStaffMembersAsync(
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultRolesSeededAsync(clinicId, cancellationToken);

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        var owner = clinic?.OwnerId != null
            ? await _dbContext.Owners.FirstOrDefaultAsync(o => o.Id == clinic.OwnerId.Value, cancellationToken)
            : null;

        var memberships = await _dbContext.UserClinics
            .Where(uc => uc.ClinicId == clinicId)
            .OrderBy(uc => uc.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = memberships.Select(m => m.UserId).Distinct().ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            var enabledModules = await _dbContext.ClinicModules
                .Where(m => m.ClinicId == clinicId && m.Enabled)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);

            var activePermissions = _moduleCatalog.GetActivePermissionsForModules(enabledModules);
            var roles = await _dbContext.ClinicRoles
                .Where(r => r.ClinicId == clinicId && !r.IsVoided)
                .ToListAsync(cancellationToken);
            var rolePerms = await _dbContext.ClinicRolePermissions
                .Where(p => p.ClinicId == clinicId)
                .ToListAsync(cancellationToken);
            var overrides = await _dbContext.UserPermissionOverrides
                .Where(o => o.ClinicId == clinicId)
                .ToListAsync(cancellationToken);

            var list = new List<StaffMemberResult>();
            foreach (var uc in memberships)
            {
                users.TryGetValue(uc.UserId, out var u);
                bool isOwner = (owner != null && owner.UserId == uc.UserId) || string.Equals(uc.Role, "Owner", StringComparison.OrdinalIgnoreCase);
                var role = roles.FirstOrDefault(r => string.Equals(r.Name, uc.Role, StringComparison.OrdinalIgnoreCase));
                var rPerms = role == null
                    ? Array.Empty<string>()
                    : rolePerms.Where(p => p.ClinicRoleId == role.Id).Select(p => p.PermissionKey).ToArray();
                var userOverrides = overrides.Where(o => o.UserId == uc.UserId).ToList();
                var grants = userOverrides.Where(o => string.Equals(o.Effect, "Grant", StringComparison.OrdinalIgnoreCase)).Select(o => o.PermissionKey);
                var denies = userOverrides.Where(o => string.Equals(o.Effect, "Deny", StringComparison.OrdinalIgnoreCase)).Select(o => o.PermissionKey);

                var eval = EffectivePermissionsEvaluator.Evaluate(isOwner, rPerms, grants, denies, activePermissions);

                list.Add(new StaffMemberResult(
                    UserId: uc.UserId,
                    ClinicId: clinicId,
                    FullName: u?.FullName ?? string.Empty,
                    Email: u?.Email ?? string.Empty,
                    RoleName: uc.Role,
                    ClinicRoleId: role?.Id,
                    IsOwner: isOwner,
                    IsActive: u?.IsActive ?? false,
                    EmailConfirmed: u?.EmailConfirmed ?? false,
                    EffectivePermissions: eval.EffectivePermissions,
                    DormantPermissions: eval.DormantPermissions,
                    Overrides: userOverrides.Select(o => new PermissionOverrideInput(o.PermissionKey, o.Effect)).ToList()));
            }

            return list;
        });
    }

    private async Task EnsureIdentityRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new AppRole(roleName));
        }
    }

    private async Task<T> ExecuteForClinicAsync<T>(Guid clinicId, Func<Task<T>> action)
    {
        Guid? previousClinicId = _currentClinicContext.ClinicId;
        bool wasAdmin = _currentClinicContext.IsPlatformAdmin;
        try
        {
            _currentClinicContext.SetTenant(clinicId);
            return await action();
        }
        finally
        {
            if (wasAdmin)
            {
                _currentClinicContext.SetPlatformAdmin();
            }
            else if (previousClinicId.HasValue)
            {
                _currentClinicContext.SetTenant(previousClinicId.Value);
            }
            else
            {
                _currentClinicContext.Clear();
            }
        }
    }
}
