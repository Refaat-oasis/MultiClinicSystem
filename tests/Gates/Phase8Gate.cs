using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

public sealed class Phase8Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate8_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase8Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 6, 8, 8, 55, 0, TimeSpan.Zero));
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
    public async Task Phase8_IntegrationGate_BillingLedgerClosingSuppliersLabWorkAndModuleIsolation()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        string adminToken = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerAEmail = $"g8-owner-a-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g8-owner-b-{Guid.NewGuid():N}@clinic.local";

        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G8 A",
            Email = ownerAEmail,
            MaxClinics = 2
        });
        await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G8 B",
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

        var createClinicA = await clinicAClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 8 فواتير", "UTC", "EGP", "NearestPound", "dev-owner-a"));
        var createClinicB = await clinicBClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("عيادة 8 مستقلة", "UTC", "EGP", "None", "dev-owner-b"));
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
                new ModuleToggleItemDto { ModuleKey = "finance", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        // Clinic B: only patients + billing (clinical OFF, finance OFF, inventory OFF)
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicBId}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        var patAResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicAId}/patients",
            new CreatePatientDto(null, "عماد فؤاد", "01081000001", null, null, 34, "Male", null, null, null, null, false, null));
        patAResp.EnsureSuccessStatusCode();
        var patA = await patAResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();

        var patBResp = await clinicBClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicBId}/patients",
            new CreatePatientDto(null, "داليا نبيل", "01081000002", null, null, 29, "Female", null, null, null, null, false, null));
        patBResp.EnsureSuccessStatusCode();
        var patB = await patBResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();

        // 1. Price List Item
        var priceResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/price-list", new UpsertPriceListItemDto(
            null, "COMP-1", "حشو تجميلي", "Composite Filling", ChargeLineKind.Tool, 50m, 60m, true, null));
        priceResp.EnsureSuccessStatusCode();
        var priceItem = await priceResp.Content.ReadFromJsonAsync<PriceListItemViewModel>();

        // 2. Reason enforcement for discount (400 BadRequest when reason is missing)
        var missingReasonResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/charges", new CreateChargeDto(
            null, patA!.Id, null, null, null, 400m, false, null, null, null, 10m, null, "", null, null, null, null, true, null));
        missingReasonResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3. Valid charge with tool markup (400 session + 60 tool = 460 gross, 10% disc = 46 -> 414 net, markup = 10)
        var chargeResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/charges", new CreateChargeDto(
            null, patA.Id, null, null, null, 400m, false, null, null, null, 10m, null, "Membership", null, null,
            [new ChargeLineDto(ChargeLineKind.Tool, priceItem!.Id, null, "Composite", 1m, 50m, 60m)],
            "Session charge", true, null));
        chargeResp.EnsureSuccessStatusCode();
        var charge = await chargeResp.Content.ReadFromJsonAsync<ChargeViewModel>();
        charge!.ToolsMarkupAmount.Should().Be(10m);
        charge.NetTotalAmount.Should().Be(414m);

        // 4. Skipped receipt & Split Multi-Method Payment -> PDF receipt
        var skipResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/skipped-receipts", new RecordSkippedReceiptDto(
            "R-000001", "Damaged print", null));
        skipResp.EnsureSuccessStatusCode();

        var payResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/payments", new RecordPaymentDto(
            null, patA.Id, charge.Id, false,
            [new PaymentSplitDto(PaymentMethod.Cash, 200m, null), new PaymentSplitDto(PaymentMethod.Visa, 100m, null)],
            null));
        payResp.EnsureSuccessStatusCode();
        var payments = await payResp.Content.ReadFromJsonAsync<List<PaymentViewModel>>();
        ArgumentNullException.ThrowIfNull(payments);
        payments.Select(p => p.ReceiptNumber).Should().BeEquivalentTo(["R-000002", "R-000003"]);

        var pdfResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/billing/payments/{payments[0].Id}/pdf");
        pdfResp.EnsureSuccessStatusCode();
        byte[] pdfBytes = await pdfResp.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(pdfBytes).Should().StartWith("%PDF-1.4");
        Encoding.ASCII.GetString(pdfBytes).Should().Contain("R-000002");

        // 5. Partial refund leaving original payment intact
        var refundResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/refunds", new CreateRefundDto(
            null, patA.Id, payments[0].Id, charge.Id, PaymentMethod.Cash, 50m, "Billing adjustment", null));
        refundResp.EnsureSuccessStatusCode();

        var ledgerResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/billing/patients/{patA.Id}/ledger");
        ledgerResp.EnsureSuccessStatusCode();
        var ledger = await ledgerResp.Content.ReadFromJsonAsync<PatientLedgerViewModel>();
        ledger!.Payments.Should().HaveCount(2);
        ledger.CurrentBalance.Should().Be(164m); // 414 - 300 + 50

        // 6. Supplier, Lab Work Order & Expense
        var supResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/suppliers", new CreateSupplierDto(
            null, "Lab Gate 8", SupplierType.Lab, "01100002222", null, null));
        supResp.EnsureSuccessStatusCode();
        var supplier = await supResp.Content.ReadFromJsonAsync<SupplierViewModel>();

        var labResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/lab-work", new CreateLabWorkBillingDto(
            null, patA.Id, null, null, supplier!.Id, "Lab Gate 8", "Bridge", "#14-#16", 300m, 900m, null, null));
        labResp.EnsureSuccessStatusCode();
        var labWork = await labResp.Content.ReadFromJsonAsync<LabWorkBillingViewModel>();
        labWork!.ExpenseId.Should().NotBeNull();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/suppliers/payments", new RecordSupplierPaymentDto(
            null, supplier.Id, PaymentMethod.Cash, 100m, "Deposit", null))).EnsureSuccessStatusCode();

        // 7. Close Day -> Locked (409) -> Reopen -> Unlocked (200)
        DateOnly today = new(2026, 6, 8);
        var closeResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/closing/close", new CloseBillingDayDto(
            today, null, "EOD Gate 8", null));
        closeResp.EnsureSuccessStatusCode();

        var blockedPayResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/payments", new RecordPaymentDto(
            null, patA.Id, charge.Id, false, [new PaymentSplitDto(PaymentMethod.Cash, 10m, null)], null));
        blockedPayResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reopenResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/closing/reopen", new ReopenBillingDayDto(
            today, "Correction", null));
        reopenResp.EnsureSuccessStatusCode();

        var unlockedPayResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicAId}/billing/payments", new RecordPaymentDto(
            null, patA.Id, charge.Id, false, [new PaymentSplitDto(PaymentMethod.InstaPay, 50m, null)], null));
        unlockedPayResp.EnsureSuccessStatusCode();

        // 8. Clinic B (clinical off, finance off, inventory off) works & excludes finance expenses on close
        var clinicBCharge = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/billing/charges", new CreateChargeDto(
            null, patB!.Id, null, null, null, 200m, false, null, null, null, null, null, null, null, null,
            [new ChargeLineDto(ChargeLineKind.Tool, null, null, "Manual Kit", 1m, 10m, 30m)], "Manual", true, null));
        clinicBCharge.EnsureSuccessStatusCode();

        var clinicBClose = await clinicBClient.PostAsJsonAsync($"/api/v1/clinics/{clinicBId}/billing/closing/close", new CloseBillingDayDto(
            today, null, "Close no finance", null));
        clinicBClose.EnsureSuccessStatusCode();
        var closedB = await clinicBClose.Content.ReadFromJsonAsync<DailyClosingViewModel>();
        closedB!.FinanceExpensesExcluded.Should().BeTrue();

        // 9. Cross-clinic isolation returns 404
        var crossResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicAId}/billing/patients/{patB.Id}/ledger");
        crossResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 10. Super Admin read-only inspect billing (POST inspect rejected 403)
        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/billing");
        inspectResp.EnsureSuccessStatusCode();
        var inspectVm = await inspectResp.Content.ReadFromJsonAsync<ClinicBillingInspectViewModel>();
        inspectVm!.ChargesCount.Should().BeGreaterThanOrEqualTo(2);

        var inspectWriteResp = await adminClient.PostAsJsonAsync($"/api/v1/platform/clinics/{clinicAId}/inspect/billing", new { });
        inspectWriteResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

