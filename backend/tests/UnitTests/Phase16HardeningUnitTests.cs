using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class Phase16HardeningUnitTests
{
    [Fact]
    public void Given_ReservedOwnerPermissions_When_Evaluated_Then_ClinicExportIsStrictlyOwnerOnly()
    {
        EffectivePermissionsEvaluator.ReservedOwnerPermissions.Should().Contain("clinic.export");

        var known = new HashSet<string>(["clinic.export", "dashboard.view"], StringComparer.OrdinalIgnoreCase);
        var customRoleResult = EffectivePermissionsEvaluator.ValidateRolePermissionsAssignment(
            isSystemOwnerRole: false,
            requestedPermissions: ["clinic.export", "dashboard.view"],
            allKnownPermissions: known,
            actorEffectivePermissions: known);
        customRoleResult.IsFailure.Should().BeTrue();
        customRoleResult.Error.Code.Should().Be("RESERVED_PERMISSION_FORBIDDEN");

        var grantResult = EffectivePermissionsEvaluator.ValidateUserGrant(
            permissionKey: "clinic.export",
            allKnownPermissions: known,
            actorEffectivePermissions: known,
            targetIsOwner: false);
        grantResult.IsFailure.Should().BeTrue();
        grantResult.Error.Code.Should().Be("RESERVED_PERMISSION_FORBIDDEN");
    }

    [Fact]
    public void Given_ModuleCatalog_When_TestingMinimalPrerequisitesAndSingleOff_Then_DependencyGraphIsConsistent()
    {
        var catalog = new ModuleCatalogService();
        var switchable = catalog.GetSwitchableModules();
        switchable.Count.Should().Be(16);

        var byKey = switchable.ToDictionary(m => m.ModuleKey, StringComparer.OrdinalIgnoreCase);

        // 1. Minimal prerequisite closure for every module enables cleanly on top of empty state
        foreach (var module in switchable)
        {
            var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddClosure(string key)
            {
                if (!closure.Add(key) || !byKey.TryGetValue(key, out var def))
                {
                    return;
                }
                foreach (var prereq in def.Prerequisites)
                {
                    AddClosure(prereq);
                }
            }

            AddClosure(module.ModuleKey);
            var changes = closure.Select(k => new ModuleToggleInput(k, true, null)).ToList();
            var result = catalog.ValidateModuleChanges(new HashSet<string>(), changes);
            result.IsSuccess.Should().BeTrue($"module '{module.ModuleKey}' with its minimal prerequisites [{string.Join(", ", closure)}] must validate cleanly");
        }

        // 2. Enabling any module with prerequisites without those prerequisites must fail with MODULE_PREREQUISITE_OFF
        foreach (var module in switchable.Where(m => m.Prerequisites.Count > 0))
        {
            var result = catalog.ValidateModuleChanges(
                new HashSet<string>(),
                [new ModuleToggleInput(module.ModuleKey, true, null)]);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("MODULE_PREREQUISITE_OFF");
        }

        // 3. Leaf modules (no dependents) can each be turned off independently while all 16 are enabled
        var allEnabled = switchable.Select(m => m.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var module in switchable.Where(m => catalog.GetDirectDependents(m.ModuleKey).Count == 0))
        {
            var result = catalog.ValidateModuleChanges(allEnabled, [new ModuleToggleInput(module.ModuleKey, false, null)]);
            result.IsSuccess.Should().BeTrue($"leaf module '{module.ModuleKey}' must turn off cleanly on its own");
        }
    }
}
