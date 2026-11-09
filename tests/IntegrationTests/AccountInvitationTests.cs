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
public sealed class AccountInvitationTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public AccountInvitationTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_InvitedOwner_When_InvitationIssued_Then_EmailDispatchedAndNoRawCodeInDatabase()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        string email = $"owner_{Guid.NewGuid():N}@clinic.local";

        // Act
        var result = await invitations.InviteAsync("Staff", email, "Dr. Sarah", "Owner", null);
        await outbox.ProcessPendingMessagesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();
        user.IsActive.Should().BeFalse();

        emailSender.SentMessages.Should().ContainSingle(m => m.To == email);
        var sentEmail = emailSender.SentMessages.First(m => m.To == email);
        sentEmail.Body.Should().MatchRegex(@"\b\d{6}\b");

        // Extract 6-digit code from email
        var match = Regex.Match(sentEmail.Body, @"\b\d{6}\b");
        string rawCode = match.Value;

        // Verify Database Security: raw 6-digit code MUST NOT be stored in DB
        var storedCode = await db.VerificationCodes.FirstOrDefaultAsync(c => c.UserId == user.Id);
        storedCode.Should().NotBeNull();
        storedCode!.CodeHash.Should().NotBe(rawCode);
        storedCode.CodeHash.Length.Should().Be(64); // SHA-256 Hex is 64 chars
    }

    [Fact]
    public async Task Given_InvitedUser_When_ActivatedWithCorrectCode_Then_UserActivatedAndCodeSingleUse()
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

        string email = $"activate_{Guid.NewGuid():N}@clinic.local";
        await invitations.InviteAsync("Staff", email, "Dr. Omar", "Owner", null);
        await outbox.ProcessPendingMessagesAsync();

        var sentEmail = emailSender.SentMessages.First(m => m.To == email);
        string rawCode = Regex.Match(sentEmail.Body, @"\b\d{6}\b").Value;

        // Act - First Activation with valid code
        var activationResult = await authService.ActivateAccountAsync(
            email,
            rawCode,
            "P@ssw0rdSecure!#99",
            "device-sarah-1",
            "Chrome Browser",
            null,
            null);

        // Assert
        activationResult.IsSuccess.Should().BeTrue();
        activationResult.Value.Tokens.AccessToken.Should().NotBeNullOrWhiteSpace();

        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed.Should().BeTrue();
        user.IsActive.Should().BeTrue();

        // Act & Assert - Second Activation with SAME code must fail (Single-use enforcement)
        var secondAttempt = await authService.ActivateAccountAsync(
            email,
            rawCode,
            "P@ssw0rdSecure!#99",
            "device-sarah-1",
            "Chrome Browser",
            null,
            null);

        secondAttempt.IsSuccess.Should().BeFalse();
        secondAttempt.Error.Code.Should().Be("CODE_EXPIRED");
    }

    [Fact]
    public async Task Given_InvitedUser_When_FiveWrongCodesEntered_Then_CodeIsDeadAndCannotBeUsed()
    {
        // Arrange
        var context = new CurrentClinicContext();
        context.SetPlatformAdmin();
        var (provider, emailSender) = TestServiceFactory.CreateServices(context);
        using var scope = provider.CreateScope();

        var invitations = scope.ServiceProvider.GetRequiredService<IAccountInvitations>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        string email = $"wrongcodes_{Guid.NewGuid():N}@clinic.local";
        await invitations.InviteAsync("Staff", email, "Dr. Tarek", "Doctor", null);
        await outbox.ProcessPendingMessagesAsync();

        var sentEmail = emailSender.SentMessages.First(m => m.To == email);
        string realCode = Regex.Match(sentEmail.Body, @"\b\d{6}\b").Value;

        // Act - Enter 5 consecutive wrong codes
        for (int i = 0; i < 5; i++)
        {
            var failedResult = await authService.ActivateAccountAsync(
                email,
                "000000",
                "P@ssw0rdSecure!#99",
                "device-1",
                "Browser",
                null,
                null);

            failedResult.IsSuccess.Should().BeFalse();
        }

        // Act - Enter the REAL code after 5 failures
        var finalAttempt = await authService.ActivateAccountAsync(
            email,
            realCode,
            "P@ssw0rdSecure!#99",
            "device-1",
            "Browser",
            null,
            null);

        // Assert - Code is dead!
        finalAttempt.IsSuccess.Should().BeFalse();
        finalAttempt.Error.Code.Should().Be("CODE_EXPIRED");
    }
}
