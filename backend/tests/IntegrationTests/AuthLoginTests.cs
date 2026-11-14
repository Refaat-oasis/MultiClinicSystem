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
public sealed class AuthLoginTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public AuthLoginTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_UnconfirmedUser_When_LoginAttempted_Then_ReturnsAccountNotActivated()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, _) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        string email = $"unconfirmed_{Guid.NewGuid():N}@clinic.local";
        await invitations.InviteAsync("Staff", email, "Dr. Mona", "Doctor", null);

        // Act
        var result = await authService.LoginAsync(
            email,
            "AnyPassword123#",
            "device-mona",
            "Browser",
            null,
            null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ACCOUNT_NOT_ACTIVATED");
    }

    [Fact]
    public async Task Given_ActivatedUser_When_ValidCredentialsProvided_Then_LoginSucceeds()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        string email = $"valid_{Guid.NewGuid():N}@clinic.local";
        string password = "StrongPassword#2026";
        await invitations.InviteAsync("Staff", email, "Dr. Mona", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();

        string code = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;
        await authService.ActivateAccountAsync(email, code, password, "device-1", "Browser", null, null);

        // Act
        var loginResult = await authService.LoginAsync(email, password, "device-1", "Browser", null, null);

        // Assert
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Value.Tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginResult.Value.Tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        loginResult.Value.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Given_FiveFailedPasswordAttempts_Then_AccountIsLockedOut()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        string email = $"lockout_{Guid.NewGuid():N}@clinic.local";
        string password = "CorrectPassword#123";
        await invitations.InviteAsync("Staff", email, "Dr. Hany", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();

        string code = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;
        await authService.ActivateAccountAsync(email, code, password, "device-1", "Browser", null, null);

        // Act - 5 consecutive wrong passwords
        for (int i = 0; i < 5; i++)
        {
            var res = await authService.LoginAsync(email, "WrongPassword#999", "device-1", "Browser", null, null);
            res.IsSuccess.Should().BeFalse();
        }

        // 6th attempt even with CORRECT password must fail due to lockout
        var lockedResult = await authService.LoginAsync(email, password, "device-1", "Browser", null, null);

        // Assert
        lockedResult.IsSuccess.Should().BeFalse();
        lockedResult.Error.Code.Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Given_RevokedDevice_When_LoginAttempted_Then_RejectedWithDeviceRevoked()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        string email = $"device_revoked_{Guid.NewGuid():N}@clinic.local";
        string password = "ValidPassword#123";
        await invitations.InviteAsync("Staff", email, "Dr. Ziad", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();

        string code = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;
        await authService.ActivateAccountAsync(email, code, password, "device-revoked-id", "Browser", null, null);

        var user = await userManager.FindByEmailAsync(email);
        await authService.RevokeDeviceAsync(user!.Id, "device-revoked-id", null, null);

        // Act
        var result = await authService.LoginAsync(email, password, "device-revoked-id", "Browser", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DEVICE_REVOKED");
    }

    [Fact]
    public async Task Given_RefreshTokenReuse_When_OldTokenReplayed_Then_EntireFamilyIsRevoked()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        string email = $"reuse_{Guid.NewGuid():N}@clinic.local";
        string password = "ValidPassword#123";
        await invitations.InviteAsync("Staff", email, "Dr. Nadia", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();

        string code = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;
        var actResult = await authService.ActivateAccountAsync(email, code, password, "device-nadia", "Browser", null, null);

        string originalRefreshToken = actResult.Value.Tokens.RefreshToken;

        // Legitimate rotation: Use refresh token to get a second token
        var rotateResult = await authService.RefreshTokenAsync(originalRefreshToken, "device-nadia");
        rotateResult.IsSuccess.Should().BeTrue();
        string secondRefreshToken = rotateResult.Value.RefreshToken;

        // Act - Replay the FIRST refresh token (attacker or race)
        var replayResult = await authService.RefreshTokenAsync(originalRefreshToken, "device-nadia");

        // Assert - Reuse detected!
        replayResult.IsSuccess.Should().BeFalse();
        replayResult.Error.Code.Should().Be("REFRESH_TOKEN_REUSED");

        // Assert - The second refresh token was also revoked in the family!
        var afterReplayResult = await authService.RefreshTokenAsync(secondRefreshToken, "device-nadia");
        afterReplayResult.IsSuccess.Should().BeFalse();
        afterReplayResult.Error.Code.Should().Be("REFRESH_TOKEN_REUSED");
    }
}
