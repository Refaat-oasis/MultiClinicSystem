using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;
using Xunit;

namespace Gates;

public sealed class Phase9Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate9_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase9Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero));
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
    public async Task Phase9_IntegrationGate_TreatmentPlansFixedPerSessionSlipClosingRefundAndModules()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        string adminToken = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerAEmail = $"g9-owner-a-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g9-owner-b-{Guid.NewGuid():N}@clinic.local";

        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G9 A",
            Email = ownerAEmail,
            MaxClinics = 2
        });
        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G9 B",
            Email = ownerBEmail,
            MaxClinics = 2
        });

        using var clinicAClient = _factory.CreateClient();
        clinicAClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerAToken = await ActivateAndLoginClinicUserAsync(clinicAClient, ownerAEmail, "OwnerPass#2026", "dev-owner-a");
        clinicAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAToken);
        clinicAClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-a");

        using var clinicBClient = _factory.CreateClient();
        clinicBClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerBToken = await ActivateAndLoginClinicUserAsync(clinicBClient, ownerBEmail, "OwnerPass#2026", "dev-owner-b");
        clinicBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerBToken);
        clinicBClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-owner-b");

        var createClinicA = await clinicAClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 9 خطط", "UTC", "EGP", "None", "dev-owner-a"));
        var createClinicB = await clinicBClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 9 معزولة", "UTC", "EGP", "None", "dev-owner-b"));
        Guid clinicAId = (await createClinicA.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        Guid clinicBId = (await createClinicB.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "finance", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "treatment-plans", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        // Clinic B keeps treatment-plans OFF, scheduling & billing ON
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicBId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        (await clinicAClient.PutAsJsonAsync($"/api/v1/clinics/{clinicAId}/working-hours", new SaveWorkingHoursDto
        {
            DoctorId = null,
            Items = [new WorkingHourItemDto { DayOfWeek = (int)DayOfWeek.Monday, StartTime = "09:00", EndTime = "17:00", IsClosed = false }]
        })).EnsureSuccessStatusCode();

        var patResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients",
            new CreatePatientDto(null, "سامي نادر", "01091000001", null, null, 33, "Male", null, null, null, null, false, null));
        patResp.EnsureSuccessStatusCode();
        var patient = await patResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();
        ArgumentNullException.ThrowIfNull(patient);

        var roomResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/rooms",
            new CreateClinicRoomDto
            {
                Code = "G9-R1",
                NameAr = "غرفة خطط",
                NameEn = "Plan Room",
                SpecialtyCode = "dental"
            });
        roomResp.EnsureSuccessStatusCode();
        var room = await roomResp.Content.ReadFromJsonAsync<ClinicRoomViewModel>();
        ArgumentNullException.ThrowIfNull(room);

        var docResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/doctors",
            new CreateClinicDoctorDto
            {
                FullName = "د. كريم فوزي",
                TitleAr = "أخصائي",
                TitleEn = "Ortho",
                SpecialtyCodes = ["dental"],
                AssignedRoomIds = [room.Id],
                SlotDurationMinutes = 30
            });
        docResp.EnsureSuccessStatusCode();
        var doctor = await docResp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();
        ArgumentNullException.ThrowIfNull(doctor);

        var catResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/scheduling/categories",
            new UpsertAppointmentCategoryDto
            {
                SpecialtyCode = "dental",
                NameAr = "جلسة تقويم",
                NameEn = "Ortho Session",
                DefaultDurationMinutes = 30,
                DefaultPriceAmount = 500m,
                DefaultPriceCurrency = "EGP",
                BookableOnline = true
            });
        catResp.EnsureSuccessStatusCode();
        var category = await catResp.Content.ReadFromJsonAsync<AppointmentCategoryViewModel>();
        ArgumentNullException.ThrowIfNull(category);

        // 1. Create 4-session FixedTotal plan with upfront & installments
        var fixedResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/treatment-plans", new CreateTreatmentPlanDto(
            null,
            patient.Id,
            doctor.Id,
            "خطة زراعة وتقويم",
            TreatmentPlanMode.FixedTotal,
            4000m,
            350m,
            1000m,
            new DateOnly(2026, 6, 8),
            true,
            "Gate 9 plan",
            [
                new CreatePlanSessionDto(1, "الجلسة 1", 0, 1, 2, 0m, 100m, null),
                new CreatePlanSessionDto(2, "الجلسة 2", 7, 2, 3, 0m, 100m, null),
                new CreatePlanSessionDto(3, "الجلسة 3", 14, 2, 3, 0m, 80m, null),
                new CreatePlanSessionDto(4, "الجلسة 4", 14, 2, 4, 0m, 70m, null)
            ],
            [
                new CreatePlanInstallmentDto(0, true, new DateOnly(2026, 6, 8), 1000m),
                new CreatePlanInstallmentDto(1, false, new DateOnly(2026, 6, 22), 1500m),
                new CreatePlanInstallmentDto(2, false, new DateOnly(2026, 7, 6), 1500m)
            ],
            GuidV7.NewGuid()));
        fixedResp.EnsureSuccessStatusCode();
        var fixedPlan = await fixedResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();
        ArgumentNullException.ThrowIfNull(fixedPlan);
        fixedPlan.Sessions.Should().HaveCount(4);

        // 2. Suggest & book appointment from Session 1 window + slip Session 2 -> shifts 3 & 4
        var suggestResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{fixedPlan.Id}/sessions/{fixedPlan.Sessions[0].Id}/suggest-slot",
            new SchedulePlanSessionDto(category.Id, room.Id, new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero), GuidV7.NewGuid()));
        suggestResp.EnsureSuccessStatusCode();
        var suggested = await suggestResp.Content.ReadFromJsonAsync<PlanSuggestedSlotViewModel>();
        ArgumentNullException.ThrowIfNull(suggested);
        suggested.BookedAppointment.Should().NotBeNull();

        var slipResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{fixedPlan.Id}/sessions/{fixedPlan.Sessions[1].Id}/slip",
            new RecordPlanSessionSlipDto(new DateOnly(2026, 6, 18), "Rescheduled"));
        slipResp.EnsureSuccessStatusCode();
        var slippedPlan = await slipResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();
        ArgumentNullException.ThrowIfNull(slippedPlan);
        slippedPlan.Sessions[2].TargetDate.Should().Be(new DateOnly(2026, 7, 2));

        // Complete Session 1 on FixedTotal -> zero extra Charge added
        var completeResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{fixedPlan.Id}/sessions/{fixedPlan.Sessions[0].Id}/complete",
            new CompletePlanSessionDto(new DateOnly(2026, 6, 8), null, null, GuidV7.NewGuid()));
        completeResp.EnsureSuccessStatusCode();
        var completedPlan = await completeResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();
        completedPlan!.Sessions[0].LinkedChargeId.Should().BeNull();

        // 3. Per-session plan posts its own charges
        var perResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/treatment-plans", new CreateTreatmentPlanDto(
            null, patient.Id, doctor.Id, "جلسات فردية", TreatmentPlanMode.PerSession, 0m, 0m, 0m, new DateOnly(2026, 6, 8), true, null,
            [new CreatePlanSessionDto(1, "تبييض", 0, 1, 2, 700m, 50m, null)], [], GuidV7.NewGuid()));
        perResp.EnsureSuccessStatusCode();
        var perPlan = await perResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();
        ArgumentNullException.ThrowIfNull(perPlan);

        var compPerResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{perPlan.Id}/sessions/{perPlan.Sessions[0].Id}/complete",
            new CompletePlanSessionDto(new DateOnly(2026, 6, 8), null, null, GuidV7.NewGuid()));
        compPerResp.EnsureSuccessStatusCode();
        var compPerPlan = await compPerResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();
        compPerPlan!.Sessions[0].LinkedChargeId.Should().NotBeNull();
        compPerPlan.TotalBilledOrContractedAmount.Should().Be(700m);

        // 4. Upfront + installment payment, deposit refund, and daily closing verification
        var payUpResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{fixedPlan.Id}/installments/{fixedPlan.Installments[0].Id}/pay",
            new PayPlanInstallmentDto(PaymentMethod.Cash, 1000m, "Upfront deposit", GuidV7.NewGuid()));
        payUpResp.EnsureSuccessStatusCode();

        var payInstResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{fixedPlan.Id}/installments/{fixedPlan.Installments[1].Id}/pay",
            new PayPlanInstallmentDto(PaymentMethod.Visa, 1500m, "Installment 1", GuidV7.NewGuid()));
        payInstResp.EnsureSuccessStatusCode();

        var refundResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/treatment-plans/{fixedPlan.Id}/installments/{fixedPlan.Installments[0].Id}/refund",
            new RefundPlanDepositDto(250m, "Partial upfront refund", GuidV7.NewGuid()));
        refundResp.EnsureSuccessStatusCode();
        var refundedPlan = await refundResp.Content.ReadFromJsonAsync<TreatmentPlanViewModel>();
        refundedPlan!.RemainingBalanceAmount.Should().Be(1750m); // 4000 - (2500 - 250)

        var closeResp = await clinicAClient.GetFromJsonAsync<DailyClosingViewModel>(
            $"/api/v1/clinics/{clinicAId}/billing/closing?date=2026-06-08");
        closeResp!.Methods.First(m => m.Method == PaymentMethod.Cash).PaymentsTotal.Should().Be(1000m);
        closeResp.Methods.First(m => m.Method == PaymentMethod.Cash).RefundsTotal.Should().Be(250m);
        closeResp.Methods.First(m => m.Method == PaymentMethod.Visa).PaymentsTotal.Should().Be(1500m);

        // 5. Super Admin read-only inspect works & writes forbidden; widgets accurate & module-off hides widgets
        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/treatment-plans");
        inspectResp.EnsureSuccessStatusCode();
        var inspectData = await inspectResp.Content.ReadFromJsonAsync<ClinicTreatmentPlansInspectViewModel>();
        inspectData!.TotalPlansCount.Should().Be(2);

        var inspectWriteResp = await adminClient.PostAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/treatment-plans", new { });
        inspectWriteResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var widgetResp = await clinicAClient.GetAsync(
            $"/api/v1/clinics/{clinicAId}/dashboards/widgets/treatment-plans.plan-revenue");
        widgetResp.EnsureSuccessStatusCode();
        var widgetData = await widgetResp.Content.ReadFromJsonAsync<DashboardWidgetDataViewModel>();
        widgetData!.PrimaryValue.Should().Be(2250m);

        // Clinic B with treatment-plans OFF gets 404/403 on plans, 0 plan widgets, while billing still works
        var clinicBPlansResp = await clinicBClient.GetAsync($"/api/v1/clinics/{clinicBId}/treatment-plans");
        ((int)clinicBPlansResp.StatusCode).Should().BeOneOf(403, 404);

        var catalogB = await clinicBClient.GetFromJsonAsync<DashboardWidgetCatalogViewModel>($"/api/v1/clinics/{clinicBId}/dashboards/widgets");
        catalogB!.Widgets.Should().NotContain(w => w.ModuleKey == "treatment-plans");

        var clinicBCat = await clinicBClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicBId}/scheduling/categories",
            new UpsertAppointmentCategoryDto
            {
                SpecialtyCode = "dental",
                NameAr = "فحص",
                NameEn = "Checkup",
                DefaultDurationMinutes = 20,
                DefaultPriceAmount = 200m,
                DefaultPriceCurrency = "EGP",
                BookableOnline = false
            });
        clinicBCat.EnsureSuccessStatusCode();
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
            DeviceName = "Gate9Browser"
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

