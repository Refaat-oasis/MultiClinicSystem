using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Policy Sweep: Ensures every API endpoint declares explicit authorization or explicit [AllowAnonymous].
/// </summary>
public sealed class PolicySweepTests
{
    [Fact]
    public void Given_AllControllerActions_When_InspectingAttributes_Then_EveryActionHasAuthorizeOrAllowAnonymous()
    {
        var apiAssembly = typeof(Platform.Api.Controllers.AuthController).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var missingAuthorization = new List<string>();

        foreach (var controller in controllerTypes)
        {
            bool controllerHasAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>(true) != null;
            bool controllerHasAllowAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>(true) != null;

            var actionMethods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

            foreach (var action in actionMethods)
            {
                bool actionHasAuthorize = action.GetCustomAttribute<AuthorizeAttribute>(true) != null;
                bool actionHasAllowAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>(true) != null;

                if (!controllerHasAuthorize && !controllerHasAllowAnonymous && !actionHasAuthorize && !actionHasAllowAnonymous)
                {
                    missingAuthorization.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        missingAuthorization.Should().BeEmpty("every controller action must be guarded by an authorization policy or explicitly marked [AllowAnonymous]");
    }
}
