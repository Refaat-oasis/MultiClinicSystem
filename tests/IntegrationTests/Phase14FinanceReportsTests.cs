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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class Phase14FinanceReportsTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase14FinanceReportsTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_SimulatedMonthInTwoClinics_When_RunningFinanceReportsExportsAndExcelImport_Then_MatchesLedgersToCentEnforcesIsolationAndRejectsBadImportRows()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero)
        };
        var currentClinic = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(currentClinic, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var clinicalService = sp.GetRequiredService<IClinicalService>();
        var billingService = sp.GetRequiredService<IBillingService>();
        var financeExt = sp.GetRequiredService<IFinanceExtendedService>();
        var reports = sp.GetRequiredService<IReportsService>();
        var excelImport = sp.GetRequiredService<IExcelImportService>();
        var dashboard = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p14-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P14", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;
        const string deviceStr = "dev-activate";

        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "Phase 14 Clinic A", "UTC", "EGP", "None", deviceStr, null, null);
        Guid clinicA = clinicARes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicA, $"p14-a-{clinicA:N}"[..16], ["dental"]);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "Phase 14 Clinic B", "UTC", "EGP", "None", deviceStr, null, null);
        Guid clinicB = clinicBRes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicB, $"p14-b-{clinicB:N}"[..16], ["dental"]);

        string[] modules = ["patients", "scheduling", "clinical", "billing", "inventory", "finance", "reports", "excel-import"];
        var enabledInput = modules.Select(m => new ModuleToggleInput(m, true, "Phase14")).ToArray();
        (await adminService.UpdateClinicModulesAsync(admin.Id, clinicA, enabledInput, "SuperStrongPass#2026", null, null)).IsSuccess.Should().BeTrue();
        (await adminService.UpdateClinicModulesAsync(admin.Id, clinicB, enabledInput, "SuperStrongPass#2026", null, null)).IsSuccess.Should().BeTrue();

        var roomA = (await structureService.CreateRoomAsync(ownerId, clinicA, "P14-R1", "غرفة 14", "Room 14", "dental", null, deviceStr, null, null)).Value;
        var docA = (await structureService.CreateDoctorAsync(ownerId, clinicA, null, "Dr. Tariq Adel", "أخصائي", "Specialist", ["dental"], [roomA.Id], 30, deviceStr, null, null)).Value;
        var catA = (await schedulingService.CreateCategoryAsync(ownerId, clinicA, "dental", "كشف", "Consultation", 30, 500m, "EGP", true, deviceStr, null, null)).Value;
        var patARes = await patientService.CreatePatientAsync(
            ownerId, clinicA,
            new CreatePatientInput(null, "سامي نصر", "01055550001", null, new DateOnly(1988, 3, 12), null, "Male", null, null, null, "dental", false, null),
            deviceStr, null, null);
        patARes.IsSuccess.Should().BeTrue();

        var visitRes = await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicA,
            new StartVisitInput(null, null, patARes.Value.Id, docA.Id, "dental", roomA.Id, 30, true, GuidV7.NewGuid(), catA.Id),
            deviceStr, null, null);
        visitRes.IsSuccess.Should().BeTrue();
        await clinicalService.CompleteVisitAsync(
            ownerId, clinicA, visitRes.Value.Id,
            new CompleteVisitInput(false, null, GuidV7.NewGuid(), null),
            deviceStr, null, null);

        var chargeRes = await billingService.CreateChargeAsync(
            ownerId, clinicA,
            new CreateChargeInput(null, patARes.Value.Id, visitRes.Value.Id, null, docA.Id, 2000m, false, null, null, null, null, null, null, null, null, null, "Package", true, GuidV7.NewGuid()),
            deviceStr, null, null);
        chargeRes.IsSuccess.Should().BeTrue();

        var payRes = await billingService.RecordPaymentAsync(
            ownerId, clinicA,
            new RecordPaymentInput(null, patARes.Value.Id, chargeRes.Value.Id, false, [new PaymentSplitInput(PaymentMethod.Cash, 2000m, null)], GuidV7.NewGuid()),
            deviceStr, null, null);
        payRes.IsSuccess.Should().BeTrue();

        var refRes = await billingService.CreateRefundAsync(
            ownerId, clinicA,
            new CreateRefundInput(null, patARes.Value.Id, payRes.Value[0].Id, chargeRes.Value.Id, PaymentMethod.Cash, 250m, "Partial discount adjustment", GuidV7.NewGuid()),
            deviceStr, null, null);
        refRes.IsSuccess.Should().BeTrue();

        var expRes = await billingService.CreateExpenseAsync(
            ownerId, clinicA,
            new CreateExpenseInput(null, "Utilities", "Electricity bill", PaymentMethod.Cash, 400m, null, true, clock.UtcNow, GuidV7.NewGuid()),
            deviceStr, null, null);
        expRes.IsSuccess.Should().BeTrue();

        var fcRes = await financeExt.UpsertFixedCostAsync(
            ownerId, clinicA,
            new UpsertFixedCostInput(null, FixedCostCategory.Rent, "إيجار أكتوبر", "October Rent", 1000m, FixedCostRecurrence.Monthly, PaymentMethod.Cash, new DateOnly(2026, 10, 5), null, true, null, GuidV7.NewGuid()),
            deviceStr, null, null);
        fcRes.IsSuccess.Should().BeTrue();

        var empRes = await financeExt.UpsertEmployeeAsync(
            ownerId, clinicA,
            new UpsertEmployeeInput(null, "Dr. Tariq Adel", "Doctor", docA.Id, "01022220000", 1000m, PaymentMethod.InstaPay, new DateOnly(2026, 1, 1), true, GuidV7.NewGuid()),
            deviceStr, null, null);
        empRes.IsSuccess.Should().BeTrue();

        (await financeExt.RecordPayrollAdjustmentAsync(
            ownerId, clinicA,
            new RecordPayrollAdjustmentInput(empRes.Value.Id, 2026, 10, PayrollAdjustmentType.Bonus, 300m, "On-call coverage", new DateOnly(2026, 10, 10), GuidV7.NewGuid()),
            deviceStr, null, null)).IsSuccess.Should().BeTrue();

        (await financeExt.RecordPayrollAdjustmentAsync(
            ownerId, clinicA,
            new RecordPayrollAdjustmentInput(empRes.Value.Id, 2026, 10, PayrollAdjustmentType.Advance, 100m, "Advance draw", new DateOnly(2026, 10, 12), GuidV7.NewGuid()),
            deviceStr, null, null)).IsSuccess.Should().BeTrue();

        var runRes = await financeExt.CreatePayrollRunAsync(
            ownerId, clinicA,
            new CreatePayrollRunInput(2026, 10, PaymentMethod.InstaPay, "October payroll", GuidV7.NewGuid()),
            deviceStr, null, null);
        runRes.IsSuccess.Should().BeTrue();

        var finRes = await financeExt.FinalizePayrollRunAsync(
            ownerId, clinicA, runRes.Value.Id, GuidV7.NewGuid(), deviceStr, null, null);
        finRes.IsSuccess.Should().BeTrue();
        finRes.Value.TotalNetPay.Should().Be(1200m);

        var fromDate = new DateOnly(2026, 10, 1);
        var toDate = new DateOnly(2026, 10, 31);
        var reportRes = await reports.GetIncomeVsExpensesReportAsync(ownerId, clinicA, fromDate, toDate, deviceStr);
        reportRes.IsSuccess.Should().BeTrue();
        reportRes.Value.NetIncome.Should().Be(1750m);
        reportRes.Value.OperatingExpenses.Should().Be(400m);
        reportRes.Value.FixedCostsTotal.Should().Be(1000m);
        reportRes.Value.PayrollTotal.Should().Be(1200m);
        reportRes.Value.TotalExpenses.Should().Be(2600m);
        reportRes.Value.NetProfit.Should().Be(-850m);

        var reportB = await reports.GetIncomeVsExpensesReportAsync(ownerId, clinicB, fromDate, toDate, deviceStr);
        reportB.IsSuccess.Should().BeTrue();
        reportB.Value.NetIncome.Should().Be(0m);
        reportB.Value.TotalExpenses.Should().Be(0m);

        var docReport = await reports.GetDoctorSummaryReportAsync(ownerId, clinicA, fromDate, toDate, docA.Id, deviceStr);
        docReport.IsSuccess.Should().BeTrue();
        docReport.Value.Should().ContainSingle();
        docReport.Value[0].SessionsCount.Should().Be(1);
        docReport.Value[0].PayrollCost.Should().Be(1200m);

        var xlRes = await reports.ExportReportAsync(ownerId, clinicA, ReportExportKind.IncomeVsExpenses, ReportExportFormat.Excel, fromDate, toDate, null, deviceStr, null, null);
        xlRes.IsSuccess.Should().BeTrue();
        using (var xlStream = new MemoryStream(xlRes.Value.Content))
        using (var wb = new XLWorkbook(xlStream))
        {
            var ws = wb.Worksheet(1);
            ws.Cell(4, 2).GetValue<decimal>().Should().Be(1750m);
            ws.Cell(10, 2).GetValue<decimal>().Should().Be(2600m);
            ws.Cell(11, 2).GetValue<decimal>().Should().Be(-850m);
        }

        var pdfRes = await reports.ExportReportAsync(ownerId, clinicA, ReportExportKind.IncomeVsExpenses, ReportExportFormat.Pdf, fromDate, toDate, null, deviceStr, null, null);
        pdfRes.IsSuccess.Should().BeTrue();
        string pdfText = Encoding.ASCII.GetString(pdfRes.Value.Content);
        pdfText.Should().Contain("NetIncome: 1750.00");
        pdfText.Should().Contain("TotalExpenses: 2600.00");
        pdfText.Should().Contain("NetProfit: -850.00");

        byte[] badWorkbook;
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Patients");
            ws.Cell(1, 1).Value = "FullName";
            ws.Cell(1, 2).Value = "Phone";
            ws.Cell(2, 1).Value = "Valid Person";
            ws.Cell(2, 2).Value = "01090000001";
            ws.Cell(3, 1).Value = "";
            ws.Cell(3, 2).Value = "12";
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            badWorkbook = ms.ToArray();
        }

        var commitBad = await excelImport.CommitImportAsync(ownerId, clinicA, ImportTargetKind.Patients, "bad.xlsx", badWorkbook, GuidV7.NewGuid(), deviceStr, null, null);
        commitBad.IsSuccess.Should().BeTrue();
        commitBad.Value.Committed.Should().BeFalse();
        commitBad.Value.RejectedRows.Should().BeGreaterThanOrEqualTo(2);

        currentClinic.SetTenant(clinicA);
        int patCountAfterBad = await db.Patients.CountAsync(p => p.ClinicId == clinicA && !p.IsVoided);
        patCountAfterBad.Should().Be(1);

        var tmplRes = await excelImport.DownloadTemplateAsync(ownerId, clinicA, ImportTargetKind.Patients, deviceStr);
        tmplRes.IsSuccess.Should().BeTrue();
        var commitGood = await excelImport.CommitImportAsync(ownerId, clinicA, ImportTargetKind.Patients, "good.xlsx", tmplRes.Value.Content, GuidV7.NewGuid(), deviceStr, null, null);
        commitGood.IsSuccess.Should().BeTrue();
        commitGood.Value.Committed.Should().BeTrue();
        commitGood.Value.ValidRows.Should().Be(1);

        int patCountAfterGood = await db.Patients.CountAsync(p => p.ClinicId == clinicA && !p.IsVoided);
        patCountAfterGood.Should().Be(2);

        DateTimeOffset fromUtc = new(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset toUtc = new(toDate.Year, toDate.Month, toDate.Day, 23, 59, 59, TimeSpan.Zero);

        var wIncome = await dashboard.QueryWidgetAsync(ownerId, clinicA, "finance.income-vs-expenses", fromUtc, toUtc, null, deviceStr);
        wIncome.IsSuccess.Should().BeTrue();
        wIncome.Value.PrimaryValue.Should().Be(-850m);

        var wExports = await dashboard.QueryWidgetAsync(ownerId, clinicA, "reports.exports-run", fromUtc, toUtc, null, deviceStr);
        wExports.IsSuccess.Should().BeTrue();
        wExports.Value.PrimaryValue.Should().Be(2m);

        var wRejected = await dashboard.QueryWidgetAsync(ownerId, clinicA, "excel-import.rejected-rows", fromUtc, toUtc, null, deviceStr);
        wRejected.IsSuccess.Should().BeTrue();
        wRejected.Value.PrimaryValue.Should().BeGreaterThanOrEqualTo(1m);

        var crossRes = await reports.GetPlatformCrossClinicReportAsync(admin.Id, fromDate, toDate);
        crossRes.IsSuccess.Should().BeTrue();
        crossRes.Value.Clinics.Should().Contain(c => c.ClinicId == clinicA && c.NetProfit == -850m);
        crossRes.Value.Clinics.Should().Contain(c => c.ClinicId == clinicB && c.NetProfit == 0m);
    }

    private static async Task OnboardClinicWithSlugAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        string slug,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة الاختبار", "12 شارع النيل", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, specialties.Select(s => new SpecialtySelectionInput(s, null, null)).ToList(), null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, slug, null, null);
        (await clinicsService.CompleteOnboardingAsync(ownerId, clinicId, null, null)).IsSuccess.Should().BeTrue();
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
}
