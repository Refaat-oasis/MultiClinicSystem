using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class PasswordResetTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public PasswordResetTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_ForgotPassword_When_CalledForKnownAndUnknownEmail_Then_BothReturnSuccess()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();

        string knownEmail = $"known_{Guid.NewGuid():N}@clinic.local";
        string unknownEmail = $"unknown_{Guid.NewGuid():N}@clinic.local";

        await invitations.InviteAsync("Staff", knownEmail, "Dr. Known", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();
        string actCode = Regex.Match(emailSender.SentMessages.First(m => m.To == knownEmail).Body, @"\b\d{6}\b").Value;
        await authService.ActivateAccountAsync(knownEmail, actCode, "InitialPass#123", "device-1", "Browser", null, null);
        emailSender.Clear();

        // Act
        var knownResult = await authService.ForgotPasswordAsync(knownEmail, null, null);
        var unknownResult = await authService.ForgotPasswordAsync(unknownEmail, null, null);
        await outbox.ProcessPendingMessagesAsync();

        // Assert - Identical response (no email enumeration)
        knownResult.IsSuccess.Should().BeTrue();
        unknownResult.IsSuccess.Should().BeTrue();

        // But email only dispatched to known user
        emailSender.SentMessages.Should().ContainSingle(m => m.To == knownEmail);
        emailSender.SentMessages.Should().NotContain(m => m.To == unknownEmail);
    }

    [Fact]
    public async Task Given_ActiveUser_When_ResetPasswordWithCode_Then_PasswordUpdatedAndSessionsRevoked()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        string email = $"reset_{Guid.NewGuid():N}@clinic.local";
        await invitations.InviteAsync("Staff", email, "Dr. Reset", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();
        string actCode = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;
        var actResult = await authService.ActivateAccountAsync(email, actCode, "OldPassword#123", "device-1", "Browser", null, null);
        string oldRefreshToken = actResult.Value.Tokens.RefreshToken;

        var userBefore = await userManager.FindByEmailAsync(email);
        string initialSecurityStamp = userBefore!.SecurityStamp!;

        emailSender.Clear();

        // Request Password Reset
        await authService.ForgotPasswordAsync(email, null, null);
        await outbox.ProcessPendingMessagesAsync();

        var resetEmail = emailSender.SentMessages.First(m => m.To == email);
        string resetCode = Regex.Match(resetEmail.Body, @"\b\d{6}\b").Value;
        emailSender.Clear();

        // Act - Reset Password
        string newPassword = "NewStrongPassword#2026";
        var resetResult = await authService.ResetPasswordAsync(email, resetCode, newPassword, null, null);
        await outbox.ProcessPendingMessagesAsync();

        // Assert
        resetResult.IsSuccess.Should().BeTrue();

        // 1. Password changed and verified
        var loginWithOld = await authService.LoginAsync(email, "OldPassword#123", "device-1", "Browser", null, null);
        loginWithOld.IsSuccess.Should().BeFalse();

        var loginWithNew = await authService.LoginAsync(email, newPassword, "device-1", "Browser", null, null);
        loginWithNew.IsSuccess.Should().BeTrue();

        // 2. Security stamp rotated
        var userAfter = await userManager.FindByEmailAsync(email);
        userAfter!.SecurityStamp.Should().NotBe(initialSecurityStamp);

        // 3. Old refresh tokens revoked
        var refreshOld = await authService.RefreshTokenAsync(oldRefreshToken, "device-1");
        refreshOld.IsSuccess.Should().BeFalse();

        // 4. Confirmation notice email sent
        emailSender.SentMessages.Should().ContainSingle(m => m.To == email);
        emailSender.SentMessages.First().Subject.Should().Contain("Password Changed");
    }
}
