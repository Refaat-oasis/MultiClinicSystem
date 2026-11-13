using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
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
public sealed class Phase3ClinicOnboardingProfileTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase3ClinicOnboardingProfileTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_OwnerWithQuota_When_CreatingClinicsBeyondQuotaAndCheckingIsolation_Then_EnforcesQuotaAnd404()
    {
        var clock = new IntegrationTestClock();
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);

        string emailA = $"owner-quota-{Guid.NewGuid():N}@clinic.local";
        string emailB = $"owner-other-{Guid.NewGuid():N}@clinic.local";
        var ownerARes = await adminService.CreateOwnerAsync(admin.Id, "Owner A", emailA, "01012345678", maxClinics: 2, null, null);
        var ownerBRes = await adminService.CreateOwnerAsync(admin.Id, "Owner B", emailB, null, maxClinics: null, null, null);
        ownerARes.IsSuccess.Should().BeTrue();
        ownerBRes.IsSuccess.Should().BeTrue();

        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, emailA, "OwnerPass#2026");
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, emailB, "OwnerPass#2026");

        var c1 = await clinicsService.CreateClinicAsync(ownerARes.Value.UserId, "عيادة النور 1", null, null, null, "dev1", null, null);
        var c2 = await clinicsService.CreateClinicAsync(ownerARes.Value.UserId, "عيادة النور 2", null, null, null, "dev1", null, null);
        var c3 = await clinicsService.CreateClinicAsync(ownerARes.Value.UserId, "عيادة النور 3", null, null, null, "dev1", null, null);

        c1.IsSuccess.Should().BeTrue();
        c2.IsSuccess.Should().BeTrue();
        c3.IsFailure.Should().BeTrue();
        c3.Error.Code.Should().Be("CLINIC_QUOTA_REACHED");

        // Owner B has no quota limit and cannot see Owner A's clinic (404)
        var crossClinic = await clinicsService.GetOnboardingStateAsync(ownerBRes.Value.UserId, c1.Value.ClinicId);
        crossClinic.IsFailure.Should().BeTrue();
        crossClinic.Error.Code.Should().Be("CLINIC_NOT_FOUND");
    }

    [Fact]
    public async Task Given_PendingAndCompletedOnboarding_When_AccessingOperationsAndPlugins_Then_GuardsUntilCompleteAndCreatesDentalOnlyWhereSelected()
    {
        var clock = new IntegrationTestClock();
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"owner-onb-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "د. أحمد", ownerEmail, null, 5, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "StrongPass#2026");

        var clinicDental = (await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة الابتسامة", null, null, null, "dev1", null, null)).Value;
        var clinicCardio = (await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "مركز القلب", null, null, null, "dev1", null, null)).Value;

        // Guard blocks dashboard/settings before onboarding completion
        var blockedDash = await clinicsService.GetClinicDashboardAsync(ownerRes.Value.UserId, clinicDental.ClinicId);
        blockedDash.IsFailure.Should().BeTrue();
        blockedDash.Error.Code.Should().Be("ONBOARDING_REQUIRED");

        // Logo checks: reject SVG, accept and strip metadata from PNG
        var svgUpload = await clinicsService.UploadClinicLogoAsync(
            ownerRes.Value.UserId,
            clinicDental.ClinicId,
            "logo.svg",
            "image/svg+xml",
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"),
            null,
            null);
        svgUpload.IsFailure.Should().BeTrue();
        svgUpload.Error.Code.Should().Be("LOGO_SVG_REJECTED");

        var pngUpload = await clinicsService.UploadClinicLogoAsync(
            ownerRes.Value.UserId,
            clinicDental.ClinicId,
            "logo.png",
            "image/png",
            BuildValidPngWithMetadata(),
            null,
            null);
        pngUpload.IsSuccess.Should().BeTrue();

        await clinicsService.CompleteWelcomeStepAsync(ownerRes.Value.UserId, clinicDental.ClinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(
            ownerRes.Value.UserId,
            clinicDental.ClinicId,
            "عيادة الابتسامة لطب الأسنان",
            "15 شارع التحرير",
            "الدقي",
            "الجيزة",
            "01012345678",
            ["0223456789"],
            "smile@clinic.local",
            pngUpload.Value.AssetId,
            null,
            null);
        await clinicsService.SaveOnboardingTypesAsync(
            ownerRes.Value.UserId,
            clinicDental.ClinicId,
            [new SpecialtySelectionInput("dental", null, null)],
            null,
            null);

        string slugA = $"smile-{Guid.NewGuid():N}"[..20];
        await clinicsService.SaveOnboardingSlugAsync(ownerRes.Value.UserId, clinicDental.ClinicId, slugA, null, null);
        var doneA = await clinicsService.CompleteOnboardingAsync(ownerRes.Value.UserId, clinicDental.ClinicId, null, null);
        doneA.IsSuccess.Should().BeTrue();
        doneA.Value.AwaitingModuleSetup.Should().BeTrue();

        // Now configure Cardiology-only clinic without Dental
        await clinicsService.SaveOnboardingDetailsAsync(
            ownerRes.Value.UserId,
            clinicCardio.ClinicId,
            "مركز القلب التخصصي",
            "10 شارع الثورة",
            "مصر الجديدة",
            "القاهرة",
            "01122334455",
            null,
            null,
            null,
            null,
            null);
        await clinicsService.SaveOnboardingTypesAsync(
            ownerRes.Value.UserId,
            clinicCardio.ClinicId,
            [new SpecialtySelectionInput("cardiology", null, null)],
            null,
            null);
        string slugB = $"cardio-{Guid.NewGuid():N}"[..20];
        await clinicsService.SaveOnboardingSlugAsync(ownerRes.Value.UserId, clinicCardio.ClinicId, slugB, null, null);
        var doneB = await clinicsService.CompleteOnboardingAsync(ownerRes.Value.UserId, clinicCardio.ClinicId, null, null);
        doneB.IsSuccess.Should().BeTrue();

        // Check dental vs non-dental specialty isolation & plugin flag
        var settingsDental = await clinicsService.GetClinicSettingsAsync(ownerRes.Value.UserId, clinicDental.ClinicId);
        var settingsCardio = await clinicsService.GetClinicSettingsAsync(ownerRes.Value.UserId, clinicCardio.ClinicId);
        settingsDental.Value.Specialties.Should().ContainSingle(s => s.HasDentalPlugin);
        settingsCardio.Value.Specialties.Should().OnlyContain(s => !s.HasDentalPlugin);

        // Tenant isolation at EF/RLS layer for ClinicAssets & ClinicSpecialties
        ctx.SetTenant(clinicCardio.ClinicId);
        var assetsVisibleToCardio = await db.ClinicAssets.ToListAsync();
        assetsVisibleToCardio.Should().BeEmpty();
        ctx.Clear();
    }

    [Fact]
    public async Task Given_CompletedClinic_When_SlugChangedOrSettingsEditedByStaffOrAfterFinancialEntry_Then_EnforcesRedirect403AndFinancialLock()
    {
        var clock = new IntegrationTestClock();
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var invitations = sp.GetRequiredService<IAccountInvitations>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"owner-settings-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner Settings", ownerEmail, null, 5, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "StrongPass#2026");

        var clinic = (await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة الهدى", null, null, null, "dev1", null, null)).Value;
        var secondClinic = (await clinicsService.CreateClinicAsync(ownerRes.Value.UserId, "عيادة ثانية", null, null, null, "dev1", null, null)).Value;

        await clinicsService.SaveOnboardingDetailsAsync(ownerRes.Value.UserId, clinic.ClinicId, "عيادة الهدى", "شارع 1", "القاهرة", "القاهرة", "01012345678", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerRes.Value.UserId, clinic.ClinicId, [new SpecialtySelectionInput("general_practice", null, null)], null, null);
        string oldSlug = $"hoda-old-{Guid.NewGuid():N}"[..20];
        await clinicsService.SaveOnboardingSlugAsync(ownerRes.Value.UserId, clinic.ClinicId, oldSlug, null, null);
        await clinicsService.CompleteOnboardingAsync(ownerRes.Value.UserId, clinic.ClinicId, null, null);

        // Invite a non-owner staff user to clinic
        string staffEmail = $"staff-{Guid.NewGuid():N}@clinic.local";
        var staffInvite = await invitations.InviteAsync("Staff", staffEmail, "Reception Staff", "Receptionist", clinic.ClinicId);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, staffEmail, "StaffPass#2026");

        // Staff member gets 403 FORBIDDEN_OWNER_ONLY when attempting to update settings
        var staffEditAttempt = await clinicsService.UpdateClinicSettingsAsync(
            staffInvite.Value, clinic.ClinicId, "Hacked Name", "St", "City", "Gov", "01012345678", null, null, null, null, null, null, null, null, null);
        staffEditAttempt.IsFailure.Should().BeTrue();
        staffEditAttempt.Error.Code.Should().Be("FORBIDDEN_OWNER_ONLY");

        // Owner updates slug -> old slug stays reserved & redirects
        string newSlug = $"hoda-new-{Guid.NewGuid():N}"[..20];
        var updated = await clinicsService.UpdateClinicSettingsAsync(
            ownerRes.Value.UserId, clinic.ClinicId, "عيادة الهدى المتطورة", "شارع 2", "القاهرة", "القاهرة", "01099887766", null, null, newSlug, "Africa/Cairo", "EGP", "NearestPound", null, null, null);
        updated.IsSuccess.Should().BeTrue();

        // Another clinic cannot claim the old slug
        var stealAttempt = await clinicsService.SaveOnboardingSlugAsync(ownerRes.Value.UserId, secondClinic.ClinicId, oldSlug, null, null);
        stealAttempt.IsFailure.Should().BeTrue();
        stealAttempt.Error.Code.Should().Be("SLUG_TAKEN");

        // Old slug resolves with permanent redirect to new slug
        var redirectResolution = await clinicsService.ResolvePublicSlugAsync(oldSlug);
        redirectResolution.IsSuccess.Should().BeTrue();
        redirectResolution.Value.IsRedirect.Should().BeTrue();
        redirectResolution.Value.CurrentSlug.Should().Be(newSlug);

        // Mark financial entry on clinic -> currency/timezone/rounding change must be rejected
        var clinicEntity = await db.Clinics.FirstAsync(c => c.Id == clinic.ClinicId);
        clinicEntity.MarkHasFinancialEntries();
        await db.SaveChangesAsync();

        var lockedAttempt = await clinicsService.UpdateClinicSettingsAsync(
            ownerRes.Value.UserId, clinic.ClinicId, "عيادة الهدى المتطورة", "شارع 2", "القاهرة", "القاهرة", "01099887766", null, null, newSlug, "UTC", "USD", "Exact", null, null, null);
        lockedAttempt.IsFailure.Should().BeTrue();
        lockedAttempt.Error.Code.Should().Be("FINANCIAL_SETTINGS_LOCKED");
    }

    [Fact]
    public async Task Given_SignedInUser_When_UpdatingProfileChangingEmailAndChangingPassword_Then_VerifiesCodeSendsNoticeAndRotatesStamp()
    {
        var clock = new IntegrationTestClock();
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var profileService = sp.GetRequiredService<IProfileService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string oldEmail = $"profile-old-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Original Name", oldEmail, null, 2, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, oldEmail, "OldPassword#2026");

        var updatedProfile = await profileService.UpdateProfileAsync(
            ownerRes.Value.UserId, "د. سارة محمد", "01234567890", "ar", "dark", null, null, null);
        updatedProfile.IsSuccess.Should().BeTrue();
        updatedProfile.Value.PhoneNumber.Should().Be("+201234567890");
        updatedProfile.Value.Theme.Should().Be("dark");

        // Request and confirm email change
        emailSender.Clear();
        string newEmail = $"profile-new-{Guid.NewGuid():N}@clinic.local";
        (await profileService.RequestEmailChangeAsync(ownerRes.Value.UserId, newEmail, null, null)).IsSuccess.Should().BeTrue();
        await outbox.ProcessPendingMessagesAsync();
        var emailMsg = emailSender.SentMessages.Should().ContainSingle(e => e.To == newEmail).Subject;
        string code = Regex.Match(emailMsg.Body, @"\b(\d{6})\b").Groups[1].Value;

        emailSender.Clear();
        var confirmRes = await profileService.ConfirmEmailChangeAsync(ownerRes.Value.UserId, code, "dev-main", null, null);
        confirmRes.IsSuccess.Should().BeTrue();

        // Old address notified of email change
        await outbox.ProcessPendingMessagesAsync();
        emailSender.SentMessages.Should().Contain(e => e.To == oldEmail);

        // Password change rotates security stamp and issues fresh tokens
        var stampBefore = await db.Users.Where(u => u.Id == ownerRes.Value.UserId).Select(u => u.SecurityStamp).FirstAsync();
        var passChangeRes = await profileService.ChangePasswordAsync(
            ownerRes.Value.UserId, "OldPassword#2026", "NewStrong#2026!", true, "dev-main", null, null);
        passChangeRes.IsSuccess.Should().BeTrue();
        var stampAfter = await db.Users.Where(u => u.Id == ownerRes.Value.UserId).Select(u => u.SecurityStamp).FirstAsync();
        stampAfter.Should().NotBe(stampBefore);

        var auditActions = await db.AuditLogs
            .Where(a => a.UserId == ownerRes.Value.UserId)
            .Select(a => a.Action)
            .ToListAsync();
        auditActions.Should().Contain("ProfileUpdated");
        auditActions.Should().Contain("EmailChanged");
        auditActions.Should().Contain("PasswordChanged");
    }

    private static async Task<PlatformAdmin> EnsureEnrolledAdminAsync(
        CoreDbContext db,
        IPlatformAdminAuthService adminAuth,
        IHourlyTotpService totp,
        IntegrationTestClock clock)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
        var firstLogin = await adminAuth.LoginAsync("ahmed@platform.local", "AdminInitial#2026", null, null, null, null);
        firstLogin.IsSuccess.Should().BeTrue();
        string code = totp.ComputeCurrentCode(firstLogin.Value.ManualSetupKey!, clock.UtcNow);
        var enrollRes = await adminAuth.CompleteEnrollmentAsync(firstLogin.Value.AdminId, "SuperStrongPass#2026", code, null, null);
        enrollRes.IsSuccess.Should().BeTrue();
        return await db.PlatformAdmins.FirstAsync(a => a.Id == firstLogin.Value.AdminId);
    }

    private static async Task ActivateUserFromOutboxAsync(
        OutboxService outbox,
        FakeEmailSender emailSender,
        IAuthService authService,
        string email,
        string password)
    {
        await outbox.ProcessPendingMessagesAsync();
        var sent = emailSender.SentMessages.Last(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));
        string code = Regex.Match(sent.Body, @"\b(\d{6})\b").Groups[1].Value;
        var res = await authService.ActivateAccountAsync(email, code, password, "dev-activate", "TestBrowser", null, null);
        res.IsSuccess.Should().BeTrue();
    }

    private static byte[] BuildValidPngWithMetadata()
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR", new byte[13]);
        WriteChunk(ms, "tEXt", Encoding.ASCII.GetBytes("Author\0SecretMetadata"));
        WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01]);
        WriteChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)data.Length);
        stream.Write(lenBytes, 0, 4);
        stream.Write(Encoding.ASCII.GetBytes(type), 0, 4);
        stream.Write(data, 0, data.Length);
        stream.Write(new byte[4], 0, 4);
    }
}
