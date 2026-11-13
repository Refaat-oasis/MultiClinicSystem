using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Platform.Api.Controllers;
using Xunit;

namespace ArchitectureTests;

public sealed class AdminHostSweepTests
{
    private static readonly HashSet<string> ApprovedPlatformControllers = new()
    {
        nameof(PlatformAuthController),
        nameof(PlatformOwnersController),
        nameof(PlatformClinicsController),
        nameof(PlatformMonitoringController)
    };

    private static readonly HashSet<string> ApprovedAnonymousPlatformActions = new()
    {
        $"{nameof(PlatformAuthController)}.{nameof(PlatformAuthController.Login)}",
        $"{nameof(PlatformAuthController)}.{nameof(PlatformAuthController.BreakGlassReset)}"
    };

    private static readonly HashSet<string> AllowedPlatformPolicies = new()
    {
        "PlatformAdmin",
        "PlatformAdminEnrollment"
    };

    [Fact]
    public void Given_PlatformControllers_When_InspectingRoutesAndPolicies_Then_AllPlatformRoutesAreStrictlyProtected()
    {
        var apiAssembly = typeof(PlatformAuthController).Assembly;
        var controllers = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var platformControllers = controllers
            .Where(c => c.GetCustomAttribute<RouteAttribute>()?.Template?.StartsWith("api/v1/platform", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        platformControllers.Should().NotBeEmpty();
        platformControllers.Select(c => c.Name).Should().BeSubsetOf(ApprovedPlatformControllers);

        foreach (var controller in platformControllers)
        {
            var controllerAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>();
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttributes().Any(a => a is HttpMethodAttribute));

            foreach (var action in actions)
            {
                string key = $"{controller.Name}.{action.Name}";
                bool isAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() != null;

                if (isAnonymous)
                {
                    ApprovedAnonymousPlatformActions.Should().Contain(
                        key,
                        because: $"anonymous platform endpoint {key} must be explicitly allowlisted");
                    continue;
                }

                var authorizeAttr = action.GetCustomAttribute<AuthorizeAttribute>() ?? controllerAuthorize;
                authorizeAttr.Should().NotBeNull(because: $"platform endpoint {key} requires [Authorize(Policy = ...)]");
                AllowedPlatformPolicies.Should().Contain(
                    authorizeAttr!.Policy,
                    because: $"platform endpoint {key} must require PlatformAdmin or PlatformAdminEnrollment policy");
            }
        }
    }

    private static readonly string[] RequiredInspectWriteVerbs = ["POST", "PUT", "DELETE", "PATCH"];

    [Fact]
    public void Given_PlatformClinicsController_When_CheckingInspectWrites_Then_AllWriteVerbsReturn403ReadOnlyGuard()
    {
        var method = typeof(PlatformClinicsController).GetMethod(nameof(PlatformClinicsController.RejectInspectWrite));
        method.Should().NotBeNull();

        var httpAttrs = method!.GetCustomAttributes<HttpMethodAttribute>().ToList();
        var verbs = httpAttrs.SelectMany(a => a.HttpMethods).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        verbs.Should().Contain(RequiredInspectWriteVerbs);
    }
}
