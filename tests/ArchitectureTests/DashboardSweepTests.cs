using System.Reflection;
using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Platform.Api.ViewModels;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Dashboard Sweep:
/// Verifies every registered dashboard widget declares valid metadata (module, permission, scope)
/// and widget aggregate payloads never expose patient-identifying fields.
/// </summary>
public sealed class DashboardSweepTests
{
    private static readonly string[] ForbiddenIdentifierTokens =
    [
        "PatientId", "PatientName", "FullName", "Phone", "Mobile",
        "NationalId", "MedicalNotes", "Diagnosis", "Prescription", "Allergy"
    ];

    [Fact]
    public void Given_AllRegisteredWidgets_When_InspectingManifests_Then_EveryWidgetHasValidModulePermissionAndScope()
    {
        var catalog = new ModuleCatalogService();
        var knownModules = catalog.GetAllManifests().Select(m => m.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownPermissions = catalog.GetPermissionCatalog().Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var widgets = catalog.GetAllWidgetDefinitions();

        widgets.Should().NotBeEmpty();

        foreach (var widget in widgets)
        {
            knownModules.Should().Contain(widget.ModuleKey);
            knownPermissions.Should().Contain(widget.RequiredPermission);
            widget.AllowedScope.Should().BeOneOf("Clinic", "Doctor", "Branch");
            widget.TitleAr.Should().NotBeNullOrWhiteSpace();
            widget.TitleEn.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Given_WidgetPayloadsAndPortfolioViewModels_When_ReflectingProperties_Then_NoPatientIdentifierFieldsExist()
    {
        Type[] aggregateTypes =
        [
            typeof(DashboardWidgetDataResult),
            typeof(DashboardWidgetDataViewModel),
            typeof(OwnerPortfolioResult),
            typeof(OwnerPortfolioClinicItemResult),
            typeof(OwnerPortfolioViewModel),
            typeof(OwnerPortfolioClinicItemViewModel)
        ];

        foreach (var type in aggregateTypes)
        {
            var propertyNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
            foreach (string token in ForbiddenIdentifierTokens)
            {
                propertyNames.Should().NotContain(
                    name => name.Contains(token, StringComparison.OrdinalIgnoreCase),
                    $"aggregate dashboard type '{type.Name}' must never expose patient-identifying field '{token}'");
            }
        }
    }
}
