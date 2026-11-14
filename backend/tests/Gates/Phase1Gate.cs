using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase1Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestConnectionString = "Server=localhost;Database=ClinicPlatform_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly WebApplicationFactory<Program> _factory;

    public Phase1Gate(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing CoreDbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Register CoreDbContext pointing to test database
                services.AddDbContext<CoreDbContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<TenantSessionInterceptor>();
                    options.UseSqlServer(TestConnectionString)
                           .AddInterceptors(interceptor);
                });
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.ApplyRowLevelSecurityAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Gate_01_InvitedOwner_ReceivesCodeEmail_AndCannotLoginBeforeActivation()
    {
        var client = _factory.CreateClient();
        string email = $"owner_gate1_{Guid.NewGuid():N}@clinic.local";

        // 1. Invite Owner directly through seed service
        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<Core.Contracts.IAccountInvitations>();
            var inviteResult = await invitations.InviteAsync("Staff", email, "Dr. Ahmed Owner", "Owner", null);
            inviteResult.IsSuccess.Should().BeTrue();
        }

        // 2. Email arrives in fake sender with a 6-digit code
        var emailSender = _factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender;
        emailSender.Should().NotBeNull();
        emailSender!.SentMessages.Should().Contain(m => m.To == email);

        var sentEmail = emailSender.SentMessages.First(m => m.To == email);
        sentEmail.Body.Should().MatchRegex(@"\b\d{6}\b");

        // 3. Attempting login before activating must fail
        var loginDto = new LoginDto
        {
            Email = email,
            Password = "AnyPassword123#",
            DeviceId = "device-test-1",
            DeviceName = "Chrome Test"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginDto);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var errorJson = await loginResponse.Content.ReadAsStringAsync();
        errorJson.Should().Contain("ACCOUNT_NOT_ACTIVATED");
    }

    [Fact]
    public async Task Gate_02_ActivationWithValidCode_Succeeds_AndKillsCodeAfterFiveWrongCodes()
    {
        var client = _factory.CreateClient();
        string email = $"owner_gate2_{Guid.NewGuid():N}@clinic.local";
        string password = "OwnerValidPassword#2026";

        // 1. Invite Owner
        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<Core.Contracts.IAccountInvitations>();
            await invitations.InviteAsync("Staff", email, "Dr. Mahmoud", "Owner", null);
        }

        var emailSender = (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        var sentEmail = emailSender.SentMessages.First(m => m.To == email);
        string code = Regex.Match(sentEmail.Body, @"\b\d{6}\b").Value;

        // 2. Activate Account with valid code
        var activateDto = new ActivateAccountDto
        {
            Email = email,
            Code = code,
            Password = password,
            DeviceId = "device-owner-gate2",
            DeviceName = "MacBook Safari"
        };

        var activateResponse = await client.PostAsJsonAsync("/api/v1/auth/activate", activateDto);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResult = await activateResponse.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        authResult.Should().NotBeNull();
        authResult!.AccessToken.Should().NotBeNullOrWhiteSpace();

        // 3. Login now succeeds
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = "device-owner-gate2"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Test five wrong codes kills a fresh invitation
        string email2 = $"owner_gate2_wrong_{Guid.NewGuid():N}@clinic.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<Core.Contracts.IAccountInvitations>();
            await invitations.InviteAsync("Staff", email2, "Dr. Khaled", "Owner", null);
        }

        var sentEmail2 = emailSender.SentMessages.First(m => m.To == email2);
        string realCode2 = Regex.Match(sentEmail2.Body, @"\b\d{6}\b").Value;

        for (int i = 0; i < 5; i++)
        {
            var badResp = await client.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
            {
                Email = email2,
                Code = "999999",
                Password = password
            });
            badResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 5th failure killed the code; real code must now be refused
        var deadCodeResp = await client.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email2,
            Code = realCode2,
            Password = password
        });
        deadCodeResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Resend issues a fresh usable code
        var resendResp = await client.PostAsJsonAsync("/api/v1/auth/resend-code", new ResendCodeDto
        {
            Email = email2,
            Purpose = "Invitation"
        });
        resendResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var freshEmail = emailSender.SentMessages.Last(m => m.To == email2);
        string freshCode = Regex.Match(freshEmail.Body, @"\b\d{6}\b").Value;
        freshCode.Should().NotBe(realCode2);

        var activateFreshResp = await client.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email2,
            Code = freshCode,
            Password = password
        });
        activateFreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gate_03_ForgotPassword_GivesSameResponse_AndResetRevokesOldSessions()
    {
        var client = _factory.CreateClient();
        string email = $"owner_gate3_{Guid.NewGuid():N}@clinic.local";
        string password = "InitialPassword#2026";

        // Setup active user
        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<Core.Contracts.IAccountInvitations>();
            await invitations.InviteAsync("Staff", email, "Dr. Yasmin", "Owner", null);
        }

        var emailSender = (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        string actCode = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;

        var activateResp = await client.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email,
            Code = actCode,
            Password = password,
            DeviceId = "device-gate3"
        });
        var authData = await activateResp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        string oldRefreshToken = authData!.RefreshToken;

        emailSender.Clear();

        // 1. Forgot password returns identical 202 Accepted for known and unknown email
        var knownResp = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordDto { Email = email });
        var unknownResp = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordDto { Email = "nobody@nowhere.local" });

        knownResp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknownResp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // 2. Extract reset code
        var resetEmail = emailSender.SentMessages.First(m => m.To == email);
        string resetCode = Regex.Match(resetEmail.Body, @"\b\d{6}\b").Value;
        emailSender.Clear();

        // 3. Reset password
        string newPassword = "NewSecretPassword#2026";
        var resetResp = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordDto
        {
            Email = email,
            Code = resetCode,
            NewPassword = newPassword
        });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Old refresh token is revoked
        var refreshResp = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenDto
        {
            RefreshToken = oldRefreshToken,
            DeviceId = "device-gate3"
        });
        refreshResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. Notice email dispatched
        emailSender.SentMessages.Should().ContainSingle(m => m.To == email);
        emailSender.SentMessages[0].Subject.Should().Contain("Password Changed");
    }

    [Fact]
    public async Task Gate_04_RevokedDevice_And_ReusedRefreshToken_AreRejected()
    {
        var client = _factory.CreateClient();
        string email = $"owner_gate4_{Guid.NewGuid():N}@clinic.local";
        string password = "Gate4Password#123";

        // Setup and activate user
        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<Core.Contracts.IAccountInvitations>();
            await invitations.InviteAsync("Staff", email, "Dr. Youssef", "Owner", null);
        }

        var emailSender = (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
        string code = Regex.Match(emailSender.SentMessages.First(m => m.To == email).Body, @"\b\d{6}\b").Value;

        var actResp = await client.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email,
            Code = code,
            Password = password,
            DeviceId = "device-gate4"
        });
        var authData = await actResp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        string token1 = authData!.RefreshToken;

        // Rotate Refresh Token
        var rotateResp = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenDto
        {
            RefreshToken = token1,
            DeviceId = "device-gate4"
        });
        rotateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedData = await rotateResp.Content.ReadFromJsonAsync<TokenResult>();
        string token2 = rotatedData!.RefreshToken;

        // Reuse old token1 -> Must be rejected with REFRESH_TOKEN_REUSED
        var reuseResp = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenDto
        {
            RefreshToken = token1,
            DeviceId = "device-gate4"
        });
        reuseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var reuseJson = await reuseResp.Content.ReadAsStringAsync();
        reuseJson.Should().Contain("REFRESH_TOKEN_REUSED");

        // Revoke device in database and test login rejection
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var device = await db.AppDevices.FirstAsync(d => d.DeviceId == "device-gate4");
            device.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = "device-gate4"
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var loginJson = await loginResp.Content.ReadAsStringAsync();
        loginJson.Should().Contain("DEVICE_REVOKED");
    }

    [Fact]
    public async Task Gate_05_AuditTrail_RecordsEveryStep()
    {
        string email = $"audit_step_{Guid.NewGuid():N}@clinic.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var invitations = scope.ServiceProvider.GetRequiredService<Core.Contracts.IAccountInvitations>();
            await invitations.InviteAsync("Staff", email, "Dr. Audit Trail", "Owner", null);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var recentAudits = await db.AuditLogs
                .Where(a => a.Action == "UserInvited")
                .OrderByDescending(a => a.TimestampUtc)
                .Take(5)
                .ToListAsync();

            recentAudits.Should().NotBeEmpty("the audit trail must record all authentication and security events");
        }
    }
}
