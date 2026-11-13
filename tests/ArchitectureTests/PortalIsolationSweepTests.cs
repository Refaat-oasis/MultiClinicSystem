using System.Reflection;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Controllers;
using Platform.Api.Filters;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Portal Isolation Sweep:
/// - No patient-portal route accepts a patientId or accountId parameter (on route, query, or DTO body).
/// - Every authenticated endpoint on PatientPortalController enforces the isolated portal.patient policy.
/// - PatientPortalController enforces Cache-Control: no-store and X-Robots-Tag: noindex via PortalSecurityHeadersAttribute.
/// - No portal DTO or ViewModel exposes cost, markup, lab cost, or doctor-private note properties.
/// </summary>
public sealed class PortalIsolationSweepTests
{
    private static readonly string[] ForbiddenPortalFieldTokens =
    {
        "ToolCost",
        "MaterialsCost",
        "Markup",
        "LabCost",
        "PrivateNote",
        "PrivateDoctorNote",
        "InternalNote"
    };

    [Fact]
    public void Given_PatientPortalController_When_InspectingActions_Then_NoEndpointAcceptsPatientIdAndAllEnforcePortalPolicyAndHeaders()
    {
        Type controller = typeof(PatientPortalController);
        controller.GetCustomAttribute<PortalSecurityHeadersAttribute>(true)
            .Should().NotBeNull("all portal responses must carry Cache-Control: no-store and X-Robots-Tag: noindex");

        MethodInfo[] actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null)
            .ToArray();

        actions.Should().NotBeEmpty();

        foreach (MethodInfo action in actions)
        {
            foreach (ParameterInfo param in action.GetParameters())
            {
                string paramName = param.Name ?? string.Empty;
                paramName.Should().NotContainEquivalentOf("patientId", $"Portal endpoint {action.Name} must never accept a patient ID from client input.");
                paramName.Should().NotContainEquivalentOf("accountId", $"Portal endpoint {action.Name} must never accept an account ID from client input.");

                if (param.ParameterType.IsClass && param.ParameterType != typeof(string))
                {
                    foreach (PropertyInfo prop in param.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        prop.Name.Should().NotContainEquivalentOf("patientId", $"Portal DTO {param.ParameterType.Name} must not accept patientId.");
                        prop.Name.Should().NotContainEquivalentOf("accountId", $"Portal DTO {param.ParameterType.Name} must not accept accountId.");
                    }
                }
            }

            bool isAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>(true) != null;
            if (!isAnonymous)
            {
                AuthorizeAttribute? authAttr = action.GetCustomAttribute<AuthorizeAttribute>(true);
                authAttr.Should().NotBeNull($"Authenticated portal action {action.Name} must specify [Authorize]");
                authAttr!.Policy.Should().Be("portal.patient", $"Portal action {action.Name} must require the isolated portal.patient policy");
            }
        }
    }

    [Fact]
    public void Given_AllPortalViewModelsAndResults_When_InspectingProperties_Then_CostMarkupAndPrivateNotesAreExcluded()
    {
        IEnumerable<Type> typesToInspect = typeof(PatientPortalController).Assembly.GetTypes()
            .Concat(typeof(IPatientPortalService).Assembly.GetTypes())
            .Where(t => t.IsClass && t.Name.Contains("Portal", StringComparison.Ordinal) && !t.Name.EndsWith("Controller", StringComparison.Ordinal));

        List<string> violations = new();
        foreach (Type type in typesToInspect)
        {
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (ForbiddenPortalFieldTokens.Any(tok => prop.Name.Contains(tok, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{type.Name}.{prop.Name}");
                }
            }
        }

        violations.Should().BeEmpty("portal DTOs and ViewModels must never expose tool costs, material costs, markup, lab costs, or private notes.");
    }

    [Fact]
    public void Given_PortalDomainTables_When_CheckingTenantContract_Then_ImplementTenantAndSoftDelete()
    {
        Type[] portalEntities = { typeof(PatientAccount), typeof(PortalConsent), typeof(ClinicPortalSetting) };
        foreach (Type entity in portalEntities)
        {
            typeof(ITenantEntity).IsAssignableFrom(entity).Should().BeTrue($"{entity.Name} must implement ITenantEntity.");
            typeof(ISoftDeletable).IsAssignableFrom(entity).Should().BeTrue($"{entity.Name} must implement ISoftDeletable.");
        }
    }
}

