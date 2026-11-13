using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Permission-Ceiling Sweep:
/// Verifies every permission across all modules is blocked when its module is off,
/// reserved permissions cannot enter custom roles, and delegation ceiling cannot be bypassed.
/// </summary>
public sealed class PermissionCeilingSweepTests
{
    private readonly ModuleCatalogService _catalog = new();

    [Fact]
    public void Given_EverySwitchableModulePermission_When_ModuleIsOff_Then_PermissionIsDormantAndNeverEffective()
    {
        var switchableModules = _catalog.GetSwitchableModules();
        var allPermissions = _catalog.GetPermissionCatalog();

        foreach (var module in switchableModules)
        {
            var remainingActiveModules = switchableModules
                .Where(m => !string.Equals(m.ModuleKey, module.ModuleKey, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.ModuleKey)
                .ToList();

            var activeCatalogPerms = _catalog.GetActivePermissionsForModules(remainingActiveModules);
            var modulePerms = allPermissions
                .Where(p => string.Equals(p.ModuleKey, module.ModuleKey, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Key)
                .ToList();

            modulePerms.Should().NotBeEmpty($"module '{module.ModuleKey}' must declare at least one permission");

            var evaluation = EffectivePermissionsEvaluator.Evaluate(
                isOwner: false,
                rolePermissions: modulePerms,
                userGrants: modulePerms,
                userDenies: Array.Empty<string>(),
                activeCatalogPermissions: activeCatalogPerms);

            evaluation.EffectivePermissions.Should().NotIntersectWith(modulePerms);
            evaluation.DormantPermissions.Should().Contain(modulePerms);
        }
    }

    [Fact]
    public void Given_ReservedOwnerPermissions_When_CheckingCatalogAndValidator_Then_AlwaysForbiddenOnCustomRoles()
    {
        var allKnown = _catalog.GetPermissionCatalog().Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string reserved in EffectivePermissionsEvaluator.ReservedOwnerPermissions)
        {
            var result = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
                isSystemOwnerRole: false,
                requestedPermissions: [reserved],
                allKnownPermissions: allKnown,
                actorEffectivePermissions: allKnown);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("RESERVED_PERMISSION_FORBIDDEN");
        }
    }

    [Fact]
    public void Given_AllNonReservedPermissions_When_ActorLacksPermission_Then_DelegationCeilingBlocksGrant()
    {
        var allKnown = _catalog.GetPermissionCatalog().Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emptyActor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var perm in _catalog.GetPermissionCatalog().Where(p => !p.IsReserved))
        {
            var result = EffectivePermissionsEvaluator.ValidateUserGrant(
                perm.Key,
                allKnown,
                emptyActor,
                targetIsOwner: false);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("DELEGATION_CEILING_EXCEEDED");
        }
    }
}

