using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class AuditLogTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public AuditLogTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_AuthenticationLifecycleEvents_When_Executed_Then_AuditRowsArePersisted()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        string email = $"audit_{Guid.NewGuid():N}@clinic.local";
        string password = "InitialAuditPassword#123";

        // 1. Invite
        await invitations.InviteAsync("Staff", email, "Dr. Audit", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();

        // 2. Activate
        string actCode = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;
        await authService.ActivateAccountAsync(email, actCode, password, "device-audit", "Browser", "127.0.0.1", "req-001");

        // 3. Login
        await authService.LoginAsync(email, password, "device-audit", "Browser", "127.0.0.1", "req-002");

        // 4. Revoke Device
        var user = await userManager.FindByEmailAsync(email);
        await authService.RevokeDeviceAsync(user!.Id, "device-audit", "127.0.0.1", "req-003");

        // Assert - Audit table contains all expected entries
        var auditLogs = await db.AuditLogs
            .Where(a => a.UserId == user.Id)
            .OrderBy(a => a.TimestampUtc)
            .ToListAsync();

        auditLogs.Should().NotBeEmpty();
        auditLogs.Should().Contain(a => a.Action == "UserInvited");
        auditLogs.Should().Contain(a => a.Action == "AccountActivated");
        auditLogs.Should().Contain(a => a.Action == "UserLoggedIn");
        auditLogs.Should().Contain(a => a.Action == "DeviceRevoked");
    }
}
