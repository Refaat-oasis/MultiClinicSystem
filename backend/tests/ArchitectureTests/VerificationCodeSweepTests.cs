using System.Reflection;
using FluentAssertions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Verification Code Sweep: Asserts that no raw verification codes or plaintext hashes leak into presentation models.
/// </summary>
public sealed class VerificationCodeSweepTests
{
    private static readonly string[] ProhibitedProperties =
    {
        "Code",
        "RawCode",
        "VerificationCode",
        "SecurityCode",
        "CodeHash"
    };

    [Fact]
    public void Given_AllViewModels_When_InspectingProperties_Then_NoViewModelExposesRawOrHashedVerificationCode()
    {
        var apiAssembly = typeof(Platform.Api.ViewModels.AuthResponseViewModel).Assembly;
        var viewModelTypes = apiAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains("ViewModels") && t.IsClass);

        var offendingProperties = new List<string>();

        foreach (var type in viewModelTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (ProhibitedProperties.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                {
                    offendingProperties.Add($"{type.Name}.{prop.Name}");
                }
            }
        }

        offendingProperties.Should().BeEmpty(
            "verification codes must NEVER be returned by any API endpoint or included in ViewModels.");
    }
}
