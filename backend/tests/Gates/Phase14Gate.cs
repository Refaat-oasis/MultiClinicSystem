using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using ClosedXML.Excel;
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

public sealed class Phase14Gate : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string GateConnectionString = "Server=localhost;Database=ClinicPlatform_Gate14_Test;Integrated Security=True;TrustServerCertificate=True;";
    private readonly GateTestClock _clock = new();
    private readonly WebApplicationFactory<Program> _factory;

    public Phase14Gate(WebApplicationFactory<Program> factory)
    {
        _clock.SetTime(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));
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
    public async Task Phase14_IntegrationGate_FinancePayrollDapperReportsExcelPdfAndImport()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Host = "admin.clinic.localhost";
        (string adminToken, _) = await EnrollAndLoginSuperAdminAsync(adminClient);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        string ownerAEmail = $"g14-ownera-{Guid.NewGuid():N}@clinic.local";
        string ownerBEmail = $"g14-ownerb-{Guid.NewGuid():N}@clinic.local";

        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G14 A",
            Email = ownerAEmail,
            MaxClinics = 3
        })).EnsureSuccessStatusCode();

        (await adminClient.PostAsJsonAsync("/api/v1/platform/owners", new CreateOwnerDto
        {
            FullName = "Owner G14 B",
            Email = ownerBEmail,
            MaxClinics = 3
        })).EnsureSuccessStatusCode();

        using var clinicAClient = _factory.CreateClient();
        clinicAClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerAToken = await ActivateAndLoginClinicUserAsync(clinicAClient, ownerAEmail, "OwnerPass#2026", "dev-g14-a");
        clinicAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAToken);
        clinicAClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-g14-a");

        using var clinicBClient = _factory.CreateClient();
        clinicBClient.DefaultRequestHeaders.Host = "app.clinic.localhost";
        string ownerBToken = await ActivateAndLoginClinicUserAsync(clinicBClient, ownerBEmail, "OwnerPass#2026", "dev-g14-b");
        clinicBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerBToken);
        clinicBClient.DefaultRequestHeaders.Add("X-Device-Id", "dev-g14-b");

        var createARes = await clinicAClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Gate 14 Clinic A", "UTC", "EGP", "None", "dev-g14-a"));
        createARes.EnsureSuccessStatusCode();
        Guid clinicA = (await createARes.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await CompleteOnboardingAsync(clinicAClient, clinicA, $"g14-a-{clinicA:N}"[..16], ["dental"]);

        var createBRes = await clinicBClient.PostAsJsonAsync("/api/v1/clinics", new CreateClinicDto("Gate 14 Clinic B", "UTC", "EGP", "None", "dev-g14-b"));
        createBRes.EnsureSuccessStatusCode();
        Guid clinicB = (await createBRes.Content.ReadFromJsonAsync<MyClinicCardViewModel>())!.ClinicId;
        await CompleteOnboardingAsync(clinicBClient, clinicB, $"g14-b-{clinicB:N}"[..16], ["dental"]);

        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicA}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "scheduling", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "clinical", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "inventory", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "finance", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "reports", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "excel-import", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        // Clinic B: finance + reports OFF (only patients & billing enabled) to verify module-off gating + isolation
        (await adminClient.PutAsJsonAsync($"/api/v1/platform/clinics/{clinicB}/modules", new UpdateClinicModulesDto
        {
            StepUpPassword = "SuperAdminGate#2026",
            Modules =
            [
                new ModuleToggleItemDto { ModuleKey = "patients", Enabled = true },
                new ModuleToggleItemDto { ModuleKey = "billing", Enabled = true }
            ]
        })).EnsureSuccessStatusCode();

        // 1. Seed Patient + Doctor + Charge + Payment (2000) + Refund (250) + Operating Expense (400) in Clinic A
        var docResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/doctors", new CreateClinicDoctorDto
        {
            FullName = "د. طارق عادل",
            TitleAr = "أخصائي",
            TitleEn = "Specialist",
            SpecialtyCodes = ["dental"],
            SlotDurationMinutes = 30
        });
        docResp.EnsureSuccessStatusCode();
        var doctorA = await docResp.Content.ReadFromJsonAsync<ClinicDoctorViewModel>();

        var patResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/patients",
            new CreatePatientDto(null, "سامي نصر", "01055551401", null, null, 34, "Male", null, null, null, "dental", false, null));
        patResp.EnsureSuccessStatusCode();
        var patA = await patResp.Content.ReadFromJsonAsync<PatientDetailViewModel>();

        var chargeResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/billing/charges", new CreateChargeDto(
            null, patA!.Id, null, doctorA!.Id, null, 2000m, false, null, null, null, null, null, null, null, null, null, "Gate14 charge", true, null));
        chargeResp.EnsureSuccessStatusCode();
        var chargeA = await chargeResp.Content.ReadFromJsonAsync<ChargeViewModel>();

        var payResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/billing/payments", new RecordPaymentDto(
            null, patA.Id, chargeA!.Id, false, [new PaymentSplitDto(PaymentMethod.Cash, 2000m, null)], null));
        payResp.EnsureSuccessStatusCode();
        var payments = await payResp.Content.ReadFromJsonAsync<List<PaymentViewModel>>();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/billing/refunds", new CreateRefundDto(
            null, patA.Id, payments![0].Id, chargeA.Id, PaymentMethod.Cash, 250m, "Gate14 partial refund", null))).EnsureSuccessStatusCode();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/billing/expenses", new CreateExpenseDto(
            null, "Utilities", "Gate14 Electricity", PaymentMethod.Cash, 400m, null, true, null, null))).EnsureSuccessStatusCode();

        // 2. Extended Finance: Fixed Cost (1000) + Employee (1000) + Bonus (300) + Advance (100) + Payroll Finalized (1200)
        var fcResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/finance/fixed-costs", new UpsertFixedCostDto
        {
            Category = FixedCostCategory.Rent,
            TitleAr = "إيجار أكتوبر",
            TitleEn = "October Rent",
            Amount = 1000m,
            Recurrence = FixedCostRecurrence.Monthly,
            PaidFromMethod = PaymentMethod.Cash,
            EffectiveFromDate = new DateOnly(2026, 10, 5),
            IsActive = true,
            OperationId = GuidV7.NewGuid()
        });
        fcResp.EnsureSuccessStatusCode();

        var empResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/finance/employees", new UpsertEmployeeDto
        {
            FullName = "د. طارق عادل",
            RoleTitle = "Doctor",
            DoctorId = doctorA.Id,
            Phone = "01022220001",
            BaseSalaryAmount = 1000m,
            PaidFromMethod = PaymentMethod.InstaPay,
            HiredOnDate = new DateOnly(2026, 1, 1),
            IsActive = true,
            OperationId = GuidV7.NewGuid()
        });
        empResp.EnsureSuccessStatusCode();
        var employee = await empResp.Content.ReadFromJsonAsync<EmployeeViewModel>();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/finance/payroll/adjustments", new RecordPayrollAdjustmentDto
        {
            EmployeeId = employee!.Id,
            PeriodYear = 2026,
            PeriodMonth = 10,
            AdjustmentType = PayrollAdjustmentType.Bonus,
            Amount = 300m,
            Reason = "On-call bonus",
            OccurredOnDate = new DateOnly(2026, 10, 10),
            OperationId = GuidV7.NewGuid()
        })).EnsureSuccessStatusCode();

        (await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/finance/payroll/adjustments", new RecordPayrollAdjustmentDto
        {
            EmployeeId = employee.Id,
            PeriodYear = 2026,
            PeriodMonth = 10,
            AdjustmentType = PayrollAdjustmentType.Advance,
            Amount = 100m,
            Reason = "Mid-month advance",
            OccurredOnDate = new DateOnly(2026, 10, 12),
            OperationId = GuidV7.NewGuid()
        })).EnsureSuccessStatusCode();

        var runCreateResp = await clinicAClient.PostAsJsonAsync($"/api/v1/clinics/{clinicA}/finance/payroll/runs", new CreatePayrollRunDto
        {
            PeriodYear = 2026,
            PeriodMonth = 10,
            PaidFromMethod = PaymentMethod.InstaPay,
            OperationId = GuidV7.NewGuid()
        });
        runCreateResp.EnsureSuccessStatusCode();
        var payrollRun = await runCreateResp.Content.ReadFromJsonAsync<PayrollRunViewModel>();
        payrollRun!.TotalNetPay.Should().Be(1200m);

        var runFinResp = await clinicAClient.PostAsJsonAsync(
            $"/api/v1/clinics/{clinicA}/finance/payroll/runs/{payrollRun.Id}/finalize",
            new FinalizePayrollRunDto { OperationId = GuidV7.NewGuid() });
        runFinResp.EnsureSuccessStatusCode();

        // 3. Reconcile Dapper Reports: Income vs Expenses & Doctor Summary & Excel/PDF Exports
        var incResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicA}/reports/income-vs-expenses?fromDate=2026-10-01&toDate=2026-10-31");
        incResp.EnsureSuccessStatusCode();
        var inc = await incResp.Content.ReadFromJsonAsync<IncomeVsExpensesReportViewModel>();
        inc!.GrossPayments.Should().Be(2000m);
        inc.Refunds.Should().Be(250m);
        inc.NetIncome.Should().Be(1750m);
        inc.OperatingExpenses.Should().Be(400m);
        inc.FixedCostsTotal.Should().Be(1000m);
        inc.PayrollTotal.Should().Be(1200m);
        inc.TotalExpenses.Should().Be(2600m);
        inc.NetProfit.Should().Be(-850m);

        var docsReportResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicA}/reports/doctors?fromDate=2026-10-01&toDate=2026-10-31");
        docsReportResp.EnsureSuccessStatusCode();
        var docRows = await docsReportResp.Content.ReadFromJsonAsync<List<DoctorFinancialReportRowViewModel>>();
        docRows!.Single(d => d.DoctorId == doctorA.Id).PayrollCost.Should().Be(1200m);

        var xlsxExportResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicA}/reports/exports?reportKind=IncomeVsExpenses&format=Excel&fromDate=2026-10-01&toDate=2026-10-31");
        xlsxExportResp.EnsureSuccessStatusCode();
        byte[] xlsxBytes = await xlsxExportResp.Content.ReadAsByteArrayAsync();
        using (var wb = new XLWorkbook(new MemoryStream(xlsxBytes)))
        {
            var ws = wb.Worksheets.First();
            string allCells = string.Join(" ", ws.CellsUsed().Select(c => c.GetValue<string>()));
            allCells.Should().Contain("1750");
            allCells.Should().Contain("2600");
        }

        var pdfExportResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicA}/reports/exports?reportKind=IncomeVsExpenses&format=Pdf&fromDate=2026-10-01&toDate=2026-10-31");
        pdfExportResp.EnsureSuccessStatusCode();
        string pdfText = Encoding.ASCII.GetString(await pdfExportResp.Content.ReadAsByteArrayAsync());
        pdfText.Should().StartWith("%PDF-1.4");
        pdfText.Should().Contain("NetIncome: 1750.00");

        // 4. Excel Import: Bad file rejects with 0 saved; Good file commits atomically
        using var badWb = new XLWorkbook();
        var badWs = badWb.AddWorksheet("Patients");
        badWs.Cell(1, 1).Value = "FullName";
        badWs.Cell(1, 2).Value = "Phone";
        badWs.Cell(1, 3).Value = "Gender";
        badWs.Cell(1, 4).Value = "DateOfBirth";
        badWs.Cell(2, 1).Value = "Bad Row Only";
        badWs.Cell(2, 2).Value = "";
        badWs.Cell(2, 3).Value = "InvalidGender";
        badWs.Cell(2, 4).Value = "bad-date";
        using var badMs = new MemoryStream();
        badWb.SaveAs(badMs);

        using var badMultipart = new MultipartFormDataContent();
        badMultipart.Add(new ByteArrayContent(badMs.ToArray()), "File", "bad.xlsx");
        var previewResp = await clinicAClient.PostAsync($"/api/v1/clinics/{clinicA}/import/Patients/preview", badMultipart);
        previewResp.EnsureSuccessStatusCode();
        var preview = await previewResp.Content.ReadFromJsonAsync<ImportValidationViewModel>();
        preview!.CanCommit.Should().BeFalse();
        preview.RejectedRows.Should().BeGreaterThanOrEqualTo(1);

        using var goodWb = new XLWorkbook();
        var goodWs = goodWb.AddWorksheet("Patients");
        goodWs.Cell(1, 1).Value = "FullName";
        goodWs.Cell(1, 2).Value = "Phone";
        goodWs.Cell(1, 3).Value = "Gender";
        goodWs.Cell(1, 4).Value = "DateOfBirth";
        goodWs.Cell(1, 5).Value = "SpecialtyCode";
        goodWs.Cell(2, 1).Value = "مستورد بوابة 14";
        goodWs.Cell(2, 2).Value = "01077771401";
        goodWs.Cell(2, 3).Value = "Female";
        goodWs.Cell(2, 4).Value = "1996-05-10";
        goodWs.Cell(2, 5).Value = "dental";
        using var goodMs = new MemoryStream();
        goodWb.SaveAs(goodMs);

        using var goodMultipart = new MultipartFormDataContent();
        goodMultipart.Add(new ByteArrayContent(goodMs.ToArray()), "File", "good.xlsx");
        goodMultipart.Add(new StringContent(GuidV7.NewGuid().ToString()), "OperationId");
        var commitResp = await clinicAClient.PostAsync($"/api/v1/clinics/{clinicA}/import/Patients/commit", goodMultipart);
        commitResp.EnsureSuccessStatusCode();
        var committed = await commitResp.Content.ReadFromJsonAsync<ImportValidationViewModel>();
        committed!.Committed.Should().BeTrue();
        committed.ValidRows.Should().Be(1);

        // 5. Dashboard widgets enabled on Clinic A, gone on Clinic B (modules OFF) + 404 Cross-clinic isolation
        var widAResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicA}/dashboards/widgets");
        widAResp.EnsureSuccessStatusCode();
        var widA = await widAResp.Content.ReadFromJsonAsync<DashboardWidgetCatalogViewModel>();
        widA!.Widgets.Select(w => w.WidgetKey).Should().Contain("finance.income-vs-expenses");
        widA.Widgets.Select(w => w.WidgetKey).Should().Contain("reports.exports-run");

        var widDataResp = await clinicAClient.GetAsync($"/api/v1/clinics/{clinicA}/dashboards/widgets/finance.income-vs-expenses?from=2026-10-01T00:00:00Z&to=2026-10-31T23:59:59Z");
        widDataResp.EnsureSuccessStatusCode();
        var widData = await widDataResp.Content.ReadFromJsonAsync<DashboardWidgetDataViewModel>();
        widData!.PrimaryValue.Should().Be(-850m);

        var widBResp = await clinicBClient.GetAsync($"/api/v1/clinics/{clinicB}/dashboards/widgets");
        widBResp.EnsureSuccessStatusCode();
        var widB = await widBResp.Content.ReadFromJsonAsync<DashboardWidgetCatalogViewModel>();
        widB!.Widgets.Select(w => w.WidgetKey).Should().NotContain("finance.income-vs-expenses");

        var crossResp = await clinicBClient.GetAsync($"/api/v1/clinics/{clinicA}/reports/income-vs-expenses?fromDate=2026-10-01&toDate=2026-10-31");
        crossResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 6. Super Admin Cross-Clinic Report & Inspect Snapshot work on admin host, 404 on clinic host
        var platCrossResp = await adminClient.GetAsync("/api/v1/platform/reports/cross-clinic?fromDate=2026-10-01&toDate=2026-10-31");
        platCrossResp.EnsureSuccessStatusCode();
        var crossReport = await platCrossResp.Content.ReadFromJsonAsync<PlatformCrossClinicReportViewModel>();
        crossReport!.TotalClinics.Should().BeGreaterOrEqualTo(2);

        var inspectResp = await adminClient.GetAsync($"/api/v1/platform/clinics/{clinicA}/inspect/finance-reports?fromDate=2026-10-01&toDate=2026-10-31");
        inspectResp.EnsureSuccessStatusCode();
        var inspect = await inspectResp.Content.ReadFromJsonAsync<ClinicFinanceReportsInspectViewModel>();
        inspect!.IncomeVsExpenses.NetProfit.Should().Be(-850m);

        var clinicHostPlatformResp = await clinicAClient.GetAsync("/api/v1/platform/reports/cross-clinic?fromDate=2026-10-01&toDate=2026-10-31");
        clinicHostPlatformResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task CompleteOnboardingAsync(HttpClient clinicClient, Guid clinicId, string slug, List<string> specialties)
    {
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/welcome", null)).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/details",
            new OnboardingDetailsDto("Gate 14 Clinic", "10 Nile St", "Maadi", "Cairo", "01001112233", null, null, null))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/types",
            new OnboardingTypesDto(specialties.Select(s => new SpecialtySelectionDto(s, null, null)).ToList()))).EnsureSuccessStatusCode();
        (await clinicClient.PutAsJsonAsync(
            $"/api/v1/clinics/{clinicId}/onboarding/slug",
            new OnboardingSlugDto(slug))).EnsureSuccessStatusCode();
        (await clinicClient.PostAsync($"/api/v1/clinics/{clinicId}/onboarding/complete", null)).EnsureSuccessStatusCode();
    }

    private async Task<(string Token, string ManualSetupKey)> EnrollAndLoginSuperAdminAsync(HttpClient adminClient)
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

        return (enrolled!.AccessToken, firstBody.ManualSetupKey!);
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

        clinicClient.DefaultRequestHeaders.Authorization = null;
        var loginResp = await clinicClient.PostAsJsonAsync("/api/v1/auth/login", new LoginDto
        {
            Email = email,
            Password = password,
            DeviceId = deviceId,
            DeviceName = "Gate14Browser"
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

