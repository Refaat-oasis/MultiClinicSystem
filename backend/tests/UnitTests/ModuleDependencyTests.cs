using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class ModuleDependencyTests
{
    private readonly ModuleCatalogService _catalog = new();

    [Fact]
    public void Given_Catalog_When_FetchingSwitchableModules_Then_AllSixteenModulesExistWithoutCore()
    {
        var switchable = _catalog.GetSwitchableModules();

        switchable.Should().HaveCount(16);
        switchable.Should().NotContain(m => m.ModuleKey == "core");
        switchable.Select(m => m.ModuleKey).Should().Contain(
        [
            "patients", "patient-history", "attachments", "scheduling", "queue",
            "clinical", "billing", "finance", "treatment-plans", "inventory",
            "time-engine", "reports", "excel-import", "portal", "portal-booking", "offline-sync"
        ]);
    }

    [Fact]
    public void Given_ClinicalRequestedWithoutScheduling_When_ValidatingTogglePlan_Then_ReturnsModulePrerequisiteOff()
    {
        var currentEnabled = new HashSet<string>(["patients"], StringComparer.OrdinalIgnoreCase);

        var result = _catalog.ValidateModuleChanges(
            currentEnabled,
            [new ModuleToggleInput("clinical", true, null)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MODULE_PREREQUISITE_OFF");
    }

    [Fact]
    public void Given_PortalBookingRequestedWithoutPortal_When_ValidatingTogglePlan_Then_ReturnsModulePrerequisiteOff()
    {
        var currentEnabled = new HashSet<string>(["patients", "scheduling"], StringComparer.OrdinalIgnoreCase);

        var result = _catalog.ValidateModuleChanges(
            currentEnabled,
            [new ModuleToggleInput("portal-booking", true, null)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MODULE_PREREQUISITE_OFF");
    }

    [Fact]
    public void Given_TreatmentPlansRequestedWithoutBilling_When_ValidatingTogglePlan_Then_ReturnsModulePrerequisiteOff()
    {
        var currentEnabled = new HashSet<string>(["patients", "scheduling"], StringComparer.OrdinalIgnoreCase);

        var result = _catalog.ValidateModuleChanges(
            currentEnabled,
            [new ModuleToggleInput("treatment-plans", true, null)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MODULE_PREREQUISITE_OFF");
    }

    [Fact]
    public void Given_PatientsDisabledWhileSchedulingEnabled_When_ValidatingTogglePlan_Then_ReturnsModuleHasDependents()
    {
        var currentEnabled = new HashSet<string>(["patients", "scheduling"], StringComparer.OrdinalIgnoreCase);

        var result = _catalog.ValidateModuleChanges(
            currentEnabled,
            [new ModuleToggleInput("patients", false, null)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MODULE_HAS_DEPENDENTS");
    }

    [Fact]
    public void Given_PrerequisitesAndModuleEnabledTogether_When_ValidatingTogglePlan_Then_Succeeds()
    {
        var currentEnabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = _catalog.ValidateModuleChanges(
            currentEnabled,
            [
                new ModuleToggleInput("patients", true, "Initial bundle"),
                new ModuleToggleInput("scheduling", true, "Initial bundle"),
                new ModuleToggleInput("clinical", true, "Initial bundle")
            ]);

        result.IsSuccess.Should().BeTrue();
    }
}

