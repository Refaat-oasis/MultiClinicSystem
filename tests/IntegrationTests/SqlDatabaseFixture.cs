using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Shared test fixture managing the lifecycle of the real SQL Server test database.
/// </summary>
public sealed class SqlDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionString = "Server=localhost;Database=ClinicPlatform_Test;Integrated Security=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        var adminContext = new CurrentClinicContext();
        adminContext.SetPlatformAdmin();

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(adminContext))
            .Options;

        await using var context = new CoreDbContext(options, adminContext);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await context.ApplyRowLevelSecurityAsync();
    }

    public async Task DisposeAsync()
    {
        var adminContext = new CurrentClinicContext();
        adminContext.SetPlatformAdmin();

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(adminContext))
            .Options;

        await using var context = new CoreDbContext(options, adminContext);
        await context.Database.EnsureDeletedAsync();
    }

    public CoreDbContext CreateDbContext(ICurrentClinic clinicContext)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(clinicContext))
            .Options;

        return new CoreDbContext(options, clinicContext);
    }
}

