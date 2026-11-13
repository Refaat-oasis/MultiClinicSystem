using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class EffectivePermissionsTests
{
    [Fact]
    public void Given_RoleGrantsAndDenies_When_Evaluating_Then_DenyWinsAndInactiveModulesBecomeDormant()
    {
        var roleClaims = new[] { "patients.read", "scheduling.read", "billing.read" };
        var userGrants = new[] { "billing.discount", "clinical.read" };
        var userDenies = new[] { "scheduling.read", "billing.discount" };
        var activeCatalog = new[] { "dashboard.view", "patients.read", "patients.write", "scheduling.read" };

        var eval = EffectivePermissionsEvaluator.Evaluate(
            isOwner: false,
            rolePermissions: roleClaims,
            userGrants: userGrants,
            userDenies: userDenies,
            activeCatalogPermissions: activeCatalog);

        eval.EffectivePermissions.Should().Equal("patients.read");
        eval.DormantPermissions.Should().Equal("billing.read", "clinical.read");
    }

    [Fact]
    public void Given_ModuleReEnabledLater_When_Evaluating_Then_DormantPermissionsAreRestoredAutomatically()
    {
        var roleClaims = new[] { "patients.read", "billing.read", "billing.write" };
        var activeWithBillingOff = new[] { "dashboard.view", "patients.read" };
        var activeWithBillingOn = new[] { "dashboard.view", "patients.read", "billing.read", "billing.write" };

        var evalOff = EffectivePermissionsEvaluator.Evaluate(
            isOwner: false,
            rolePermissions: roleClaims,
            userGrants: Array.Empty<string>(),
            userDenies: Array.Empty<string>(),
            activeCatalogPermissions: activeWithBillingOff);

        evalOff.EffectivePermissions.Should().Equal("patients.read");
        evalOff.DormantPermissions.Should().Equal("billing.read", "billing.write");

        var evalOn = EffectivePermissionsEvaluator.Evaluate(
            isOwner: false,
            rolePermissions: roleClaims,
            userGrants: Array.Empty<string>(),
            userDenies: Array.Empty<string>(),
            activeCatalogPermissions: activeWithBillingOn);

        evalOn.EffectivePermissions.Should().Equal("billing.read", "billing.write", "patients.read");
        evalOn.DormantPermissions.Should().BeEmpty();
    }

    [Fact]
    public void Given_ReservedOwnerPermission_When_ValidatingCustomRoleAssignment_Then_ReturnsReservedPermissionForbidden()
    {
        var allKnown = new HashSet<string>(["clinic.settings.edit", "roles.manage", "patients.read"], StringComparer.OrdinalIgnoreCase);
        var actorEffective = new HashSet<string>(["clinic.settings.edit", "roles.manage", "patients.read"], StringComparer.OrdinalIgnoreCase);

        var resSettings = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
            isSystemOwnerRole: false,
            requestedPermissions: ["patients.read", "clinic.settings.edit"],
            allKnownPermissions: allKnown,
            actorEffectivePermissions: actorEffective);

        var resRoles = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
            isSystemOwnerRole: false,
            requestedPermissions: ["roles.manage"],
            allKnownPermissions: allKnown,
            actorEffectivePermissions: actorEffective);

        resSettings.IsFailure.Should().BeTrue();
        resSettings.Error.Code.Should().Be("RESERVED_PERMISSION_FORBIDDEN");
        resRoles.IsFailure.Should().BeTrue();
        resRoles.Error.Code.Should().Be("RESERVED_PERMISSION_FORBIDDEN");
    }

    [Fact]
    public void Given_ActorLacksPermission_When_GrantingToCustomRoleOrUser_Then_ReturnsDelegationCeilingExceeded()
    {
        var allKnown = new HashSet<string>(["patients.read", "billing.discount"], StringComparer.OrdinalIgnoreCase);
        var actorEffective = new HashSet<string>(["patients.read"], StringComparer.OrdinalIgnoreCase);

        var roleResult = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
            isSystemOwnerRole: false,
            requestedPermissions: ["patients.read", "billing.discount"],
            allKnownPermissions: allKnown,
            actorEffectivePermissions: actorEffective);

        var userGrantResult = EffectivePermissionsEvaluator.ValidateUserGrant(
            permissionKey: "billing.discount",
            allKnownPermissions: allKnown,
            actorEffectivePermissions: actorEffective,
            targetIsOwner: false);

        roleResult.IsFailure.Should().BeTrue();
        roleResult.Error.Code.Should().Be("DELEGATION_CEILING_EXCEEDED");
        userGrantResult.IsFailure.Should().BeTrue();
        userGrantResult.Error.Code.Should().Be("DELEGATION_CEILING_EXCEEDED");
    }
}
