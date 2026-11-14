using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(BuildingBlocks.Domain.Money).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(Platform.Api.Controllers.HealthController).Assembly;

    [Fact]
    public void Given_BuildingBlocksDomain_When_CheckingDependencies_Then_DoesNotDependOnPlatformApiOrInfrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("BuildingBlocks.Domain")
            .ShouldNot()
            .HaveDependencyOnAny("Platform.Api", "Core.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Given_Controllers_When_CheckingDependencies_Then_DoNotDirectlyDependOnDbContext()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Platform.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore.DbContext")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}

