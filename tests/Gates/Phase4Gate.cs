using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase4Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate4_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase4Gate(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
                if (dbDescriptor != null)
                {
                    services.Remove(dbDescriptor);
                }

                services.AddDbContext<CoreDbContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<TenantSessionInterceptor>();
                    options.UseSqlServer(GateConnectionString)
                           .AddInterceptors(interceptor);
                });

                var clockDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IClock));
                if (clockDescriptor != null)
                {
                    services.Remove(clockDescriptor);
                }

                services.AddSingleton<IClock>(_clock);
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await db.ApplyRowLevelSecurityAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Gate_Phase4_EndToEnd_ModulesRolesDashboardsAndStructure()
    {
        using var adminClient = CreateClient(isAdminHost: true);
        using var ownerClient = CreateClient(isAdminHost: false);

        // Step 1: Super Admin sets up Owner with 2 clinics (Clinic A, Clinic B), enables patients + scheduling + billing on Clinic A
        string adminJwt = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);

        string ownerEmail = $"p4-owner-{Guid.NewGuid():N}@clinic.local";
        var createOwnerResp = await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "د. كريم مصطفى",
            Email = ownerEmail,
            Phone = "01022334455",
            MaxClinics = 3
        });
        createOwnerResp.StatusCode.Should().Be(HttpStatusCode.Created);

        string ownerToken = await ActivateAndLoginClinicUserAsync(ownerClient, ownerEmail, "OwnerStrong#2026", "owner-device-1");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var clinicAResp = await ownerClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة الصفوة أ", "Africa/Cairo", "EGP", "NearestPound", "owner-device-1"));
        var clinicBResp = await ownerClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة الصفوة ب", "Africa/Cairo", "EGP", "NearestPound", "owner-device-1"));
        clinicAResp.StatusCode.Should().Be(HttpStatusCode.Created);
        clinicBResp.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid clinicAId = (await clinicAResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        Guid clinicBId = (await clinicBResp.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;

        // Owner cannot change modules from clinic endpoint -> 403
        var ownerToggleAttempt = await ownerClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            Modules = [new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true }]
        });
        ownerToggleAttempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Prerequisite check on admin host: clinical without scheduling -> 409 MODULE_PREREQUISITE_OFF
        var prereqFail = await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true }]
        });
        prereqFail.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await prereqFail.Content.ReadAsStringAsync()).Should().Contain("MODULE_PREREQUISITE_OFF");

        // Super Admin enables patients, scheduling, billing on Clinic A
        var enableModules = await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true, Note = "Phase 4 Gate" },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true, Note = "Phase 4 Gate" },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true, Note = "Phase 4 Gate" }
            ]
        });
        enableModules.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: Clinic Owner checks read-only manifest & clones Receptionist -> Senior Receptionist
        var manifest = await ownerClient.GetFromJsonAsync<MyModulesManifestViewModel>($"/api/v1/clinics/{clinicAId}/modules");
        manifest!.Modules.Count(m => m.Enabled).Should().Be(3);

        var roles = await ownerClient.GetFromJsonAsync<List<ClinicRoleViewModel>>($"/api/v1/clinics/{clinicAId}/roles");
        var receptionistRole = roles!.First(r => r.Name == "Receptionist");

        var cloneResp = await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/roles/{receptionistRole.RoleId}/clone",
            new CloneClinicRoleDto { NewName = "Senior Receptionist", NewNameAr = "استقبال أول", NewNameEn = "Senior Receptionist" });
        cloneResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var seniorRole = await cloneResp.Content.ReadFromJsonAsync<ClinicRoleViewModel>();

        // Reserved owner permission rejected on custom role
        var reservedResp = await ownerClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/roles/{seniorRole!.RoleId}/permissions",
            new UpdateRolePermissionsDto { Permissions = ["patients.read", "clinic.settings.edit"] });
        reservedResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reservedResp.Content.ReadAsStringAsync()).Should().Contain("RESERVED_PERMISSION_FORBIDDEN");

        // Step 3: Owner invites staff member, staff activates on new device (blocked until device approved), Grant/Deny & Dormant flow
        string staffEmail = $"p4-staff-{Guid.NewGuid():N}@clinic.local";
        var inviteResp = await ownerClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/staff/invite",
            new InviteStaffMemberDto { FullName = "منى استقبال", Email = staffEmail, RoleName = "Senior Receptionist" });
        inviteResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var staffMember = await inviteResp.Content.ReadFromJsonAsync<StaffMemberViewModel>();

        using var staffClient = CreateClient(isAdminHost: false);
        string staffToken = await ActivateAndLoginClinicUserAsync(staffClient, staffEmail, "StaffStrong#2026!", "staff-tablet-1");
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // Staff device unapproved -> widget catalog returns 403 DEVICE_APPROVAL_REQUIRED
        var staffBlocked = await staffClient.GetAsync($"/api/v1/clinics/{clinicAId}/dashboards/widgets");
        staffBlocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staffBlocked.Content.ReadAsStringAsync()).Should().Contain("DEVICE_APPROVAL_REQUIRED");

        // Owner approves staff device
        var approveResp = await ownerClient.PostAsync($"/api/v1/clinics/{clinicAId}/devices/staff-tablet-1/approve", null);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner sets Grant billing.discount & Deny patients.write
        var overrideResp = await ownerClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/staff/{staffMember!.UserId}/overrides",
            new SetUserPermissionOverridesDto
            {
                Overrides =
                [
                    new PermissionOverrideItemDto { PermissionKey = "billing.discount", Effect = "Grant" },
                    new PermissionOverrideItemDto { PermissionKey = "patients.write", Effect = "Deny" }
                ]
            });
        overrideResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Staff now queries billing widget successfully
        var billingWidgetOk = await staffClient.GetAsync($"/api/v1/clinics/{clinicAId}/dashboards/widgets/billing.revenue-summary");
        billingWidgetOk.StatusCode.Should().Be(HttpStatusCode.OK);

        // Turn billing OFF -> billing becomes dormant and widget is blocked
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "billing", Enabled = false }]
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var staffPermsOff = await staffClient.GetFromJsonAsync<UserEffectivePermissionsViewModel>($"/api/v1/clinics/{clinicAId}/permissions/me");
        staffPermsOff!.EffectivePermissions.Should().NotContain("billing.discount");
        staffPermsOff.DormantPermissions.Should().Contain(["billing.read", "billing.write", "billing.discount"]);

        var billingWidgetOff = await staffClient.GetAsync($"/api/v1/clinics/{clinicAId}/dashboards/widgets/billing.revenue-summary");
        billingWidgetOff.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Turn billing BACK ON -> dormant permissions automatically return
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules = [new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true }]
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var staffPermsOn = await staffClient.GetFromJsonAsync<UserEffectivePermissionsViewModel>($"/api/v1/clinics/{clinicAId}/permissions/me");
        staffPermsOn!.EffectivePermissions.Should().Contain(["billing.read", "billing.write", "billing.discount"]);

        // Step 4: Clinic Structure (Room, Doctor, Working Hours, Day Off, NumberBlock) & Owner Portfolio
        var roomResp = await ownerClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/rooms", new CreateClinicRoomDto
        {
            Code = "C-1", NameAr = "عيادة 1", NameEn = "Room 1", SpecialtyCode = "dental", FloorOrLocation = "First"
        });
        roomResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var room = await roomResp.Content.ReadFromJsonAsync<ClinicRoomViewModel>();

        var docResp = await ownerClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/doctors", new CreateClinicDoctorDto
        {
            FullName = "د. هاني سمير", TitleAr = "أخصائي", TitleEn = "Specialist",
            SpecialtyCodes = ["dental"], AssignedRoomIds = [room!.Id], SlotDurationMinutes = 30
        });
        docResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var hoursResp = await ownerClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/working-hours", new SaveWorkingHoursDto
        {
            Items = [new WorkingHourItemDto { DayOfWeek = 0, StartTime = "10:00", EndTime = "18:00", IsClosed = false }]
        });
        hoursResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dayOffResp = await ownerClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/days-off", new CreateClinicDayOffDto
        {
            StartDate = "2026-12-31", EndDate = "2027-01-01", ReasonAr = "عطلة", ReasonEn = "Holiday"
        });
        dayOffResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var block1 = await ownerClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/number-blocks/reserve", new ReserveNumberBlockDto
        {
            DeviceId = "owner-device-1", Kind = "Receipt", BlockSize = 500
        });
        block1.StatusCode.Should().Be(HttpStatusCode.Created);
        var b1 = await block1.Content.ReadFromJsonAsync<NumberBlockViewModel>();
        b1!.FromNumber.Should().Be(1);
        b1.ToNumber.Should().Be(500);

        var portfolio = await ownerClient.GetFromJsonAsync<OwnerPortfolioViewModel>("/api/v1/portfolio");
        portfolio!.TotalClinics.Should().Be(2);
        portfolio.Clinics.Select(c => c.ClinicId).Should().Contain([clinicAId, clinicBId]);

        var auditEntries = await ownerClient.GetFromJsonAsync<List<AuditLogItemViewModel>>($"/api/v1/clinics/{clinicAId}/audit");
        auditEntries!.Should().Contain(a => a.Action == "NumberBlockReserved");
    }

    private HttpClient CreateClient(bool isAdminHost)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri(isAdminHost ? "https://admin.clinic.local" : "https://app.clinic.local")
        });
        client.DefaultRequestHeaders.Add("X-Host-Type", isAdminHost ? "admin" : "clinic");
        return client;
    }

    private async Task<string> EnrollAndLoginSuperAdminAsync(HttpClient adminClient)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM [core].[PlatformAdmins];");
        }

        var loginFirst = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformAdminLoginDto
        {
            Email = "ahmed@platform.local",
            Password = "AdminInitial#2026"
        });
        loginFirst.EnsureSuccessStatusCode();
        var firstBody = await loginFirst.Content.ReadFromJsonAsync<PlatformAdminLoginViewModel>();

        string totpCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var totpService = scope.ServiceProvider.GetRequiredService<IHourlyTotpService>();
            totpCode = totpService.ComputeCurrentCode(firstBody!.ManualSetupKey!, _clock.UtcNow);
        }

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBody!.AccessToken);
        var enrollResp = await adminClient.PostAsJsonAsync("/api/v1/platform/auth/enroll", new PlatformAdminEnrollDto
        {
            NewPassword = "SuperAdminGate#2026",
            HourlyCode = totpCode
        });
        enrollResp.EnsureSuccessStatusCode();
        var enrolled = await enrollResp.Content.ReadFromJsonAsync<TotpEnrollmentCompletedViewModel>();
        adminClient.DefaultRequestHeaders.Authorization = null;

        return enrolled!.AccessToken;
    }

    private async Task<string> ActivateAndLoginClinicUserAsync(HttpClient clinicClient, string email, string password, string deviceId)
    {
        string code = await ExtractLatestCodeFromOutboxAsync(email);
        var activateResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountDto
        {
            Email = email,
            Code = code,
            Password = password
        });
        activateResp.EnsureSuccessStatusCode();

        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "GateBrowser"
        });
        loginResp.EnsureSuccessStatusCode();
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthResponseViewModel>();
        return authBody!.AccessToken;
    }

    private async Task<string> ExtractLatestCodeFromOutboxAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>() as FakeEmailSender
                          ?? throw new InvalidOperationException("FakeEmailSender not registered.");
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
        await outbox.ProcessPendingMessagesAsync();
        var sent = emailSender.SentMessages.Last(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase));
        return Regex.Match(sent.Body, @"\b(\d{6})\b").Groups[1].Value;
    }
}
