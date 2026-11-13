using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Anonymous Endpoint Allowlist Sweep: Every [AllowAnonymous] action must exist in an approved list.
/// </summary>
public sealed class AnonymousEndpointAllowlistTests
{
    private static readonly HashSet<string> ApprovedAnonymousEndpoints = new()
    {
        "HealthController.Get",
        "AuthController.Login",
        "AuthController.Activate",
        "AuthController.ForgotPassword",
        "AuthController.ResetPassword",
        "AuthController.ResendCode",
        "AuthController.RefreshToken",
        "PlatformAuthController.Login",
        "PlatformAuthController.BreakGlassReset",
        "PublicClinicDirectoryController.ResolveBySlug"
    };

    [Fact]
    public void Given_AllControllerActions_When_MarkedAllowAnonymous_Then_MustBeInApprovedAllowlist()
    {
        var apiAssembly = typeof(Platform.Api.Controllers.AuthController).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var unauthorizedAnonymous = new List<string>();

        foreach (var controller in controllerTypes)
        {
            var actionMethods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

            foreach (var action in actionMethods)
            {
                bool isAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>(true) != null ||
                                   controller.GetCustomAttribute<AllowAnonymousAttribute>(true) != null;

                if (isAnonymous)
                {
                    string endpointKey = $"{controller.Name}.{action.Name}";
                    if (!ApprovedAnonymousEndpoints.Contains(endpointKey))
                    {
                        unauthorizedAnonymous.Add(endpointKey);
                    }
                }
            }
        }

        unauthorizedAnonymous.Should().BeEmpty(
            "unauthorized anonymous endpoints discovered! Any new public endpoint must be approved in the allowlist.");
    }
}
