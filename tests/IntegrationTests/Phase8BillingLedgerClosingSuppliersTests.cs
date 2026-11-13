using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Domain;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("SqlDatabase")]
public sealed class Phase8BillingLedgerClosingSuppliersTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase8BillingLedgerClosingSuppliersTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_BillingLedgerClosingAndSuppliers_When_RunningFullWorkflows_Then_ClaimsReceiptsClosingLockReopenAndModuleOffRulesHold()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero) // Monday
        };
        var ctx = new CurrentClinicContext();
        var (provider, emailSender) = TestServiceFactory.CreateServices(ctx, clock);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CoreDbContext>();
        var adminAuth = sp.GetRequiredService<IPlatformAdminAuthService>();
        var totp = sp.GetRequiredService<IHourlyTotpService>();
        var adminService = sp.GetRequiredService<IPlatformAdminService>();
        var authService = sp.GetRequiredService<IAuthService>();
        var authzService = sp.GetRequiredService<IClinicAuthorizationService>();
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var clinicalService = sp.GetRequiredService<IClinicalService>();
        var billingService = sp.GetRequiredService<IBillingService>();
        var billingPortal = sp.GetRequiredService<IBillingPortalQueries>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p8-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P8", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "عيادة الفواتير أ", "UTC", "EGP", "NearestPound", "dev-owner", null, null);
        Guid clinicAId = clinicARes.Value.ClinicId;
        await OnboardClinicAsync(clinicsService, ownerId, clinicAId, ["dental"]);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة الفواتير ب", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicBId = clinicBRes.Value.ClinicId;
        await OnboardClinicAsync(clinicsService, ownerId, clinicBId, ["dental"]);

        string[] modulesA = ["patients", "scheduling", "clinical", "billing", "finance"];
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicAId,
            modulesA.Select(m => new ModuleToggleInput(m, true, "P8")).ToList(),
            "SuperStrongPass#2026", null, null);

        // Clinic B: Billing ON, Clinical OFF, Finance OFF, Inventory OFF
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicBId,
            [new ModuleToggleInput("patients", true, "P8"), new ModuleToggleInput("billing", true, "P8")],
            "SuperStrongPass#2026", null, null);

        var patientA = await patientService.CreatePatientAsync(
            ownerId, clinicAId,
            new CreatePatientInput(null, "حسن محمود", "01080000001", null, null, 32, "Male", null, null, null, null, false, null),
            "dev-owner", null, null);
        var patientB = await patientService.CreatePatientAsync(
            ownerId, clinicBId,
            new CreatePatientInput(null, "منى عادل", "01080000002", null, null, 28, "Female", null, null, null, null, false, null),
            "dev-owner", null, null);

        // Staff role with billing.read + billing.write ONLY (no discount, free_session, custom_price, extra_charge, refund, reopen_day)
        var basicRole = await authzService.CreateRoleAsync(
            ownerId, clinicAId, "BasicCashier", "أمين خزينة", "Basic Cashier",
            ["billing.read", "billing.write", "patients.read", "dashboard.view"],
            "dev-owner", null, null);
        basicRole.IsSuccess.Should().BeTrue();

        string cashierEmail = $"p8-cashier-{Guid.NewGuid():N}@clinic.local";
        var cashierInvite = await authzService.InviteStaffAsync(ownerId, clinicAId, "Basic Cashier", cashierEmail, "BasicCashier", "dev-owner", null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, cashierEmail, "CashPass#2026");
        Guid cashierId = cashierInvite.Value.UserId;
        await ApproveAllPendingDevicesAsync(authzService, ownerId, clinicAId);

        // Claim checks: special billing actions rejected for BasicCashier without claims
        var noFreeClaim = await billingService.CreateChargeAsync(
            cashierId, clinicAId,
            new CreateChargeInput(null, patientA.Value.Id, null, null, null, 300m, true, "VIP", null, null, null, null, null, null, null, null, null, true, null),
            "dev-activate", null, null);
        noFreeClaim.IsFailure.Should().BeTrue();
        noFreeClaim.Error.Code.Should().Be("INSUFFICIENT_PERMISSION_FREE_SESSION");

        var noCustomClaim = await billingService.CreateChargeAsync(
            cashierId, clinicAId,
            new CreateChargeInput(null, patientA.Value.Id, null, null, null, 300m, false, null, 200m, "Override", null, null, null, null, null, null, null, true, null),
            "dev-activate", null, null);
        noCustomClaim.IsFailure.Should().BeTrue();
        noCustomClaim.Error.Code.Should().Be("INSUFFICIENT_PERMISSION_CUSTOM_PRICE");

        var noDiscountClaim = await billingService.CreateChargeAsync(
            cashierId, clinicAId,
            new CreateChargeInput(null, patientA.Value.Id, null, null, null, 300m, false, null, null, null, 10m, null, "Promo", null, null, null, null, true, null),
            "dev-activate", null, null);
        noDiscountClaim.IsFailure.Should().BeTrue();
        noDiscountClaim.Error.Code.Should().Be("INSUFFICIENT_PERMISSION_DISCOUNT");

        // Owner has claim -> reason must still be required
        var missingDiscountReason = await billingService.CreateChargeAsync(
            ownerId, clinicAId,
            new CreateChargeInput(null, patientA.Value.Id, null, null, null, 300m, false, null, null, null, 10m, null, " ", null, null, null, null, true, null),
            "dev-owner", null, null);
        missingDiscountReason.IsFailure.Should().BeTrue();
        missingDiscountReason.Error.Code.Should().Be("DISCOUNT_REASON_REQUIRED");

        // Upsert price list item + Session charge with tool markup (cost 50, charged 60 -> markup 10)
        var priceItem = await billingService.UpsertPriceListItemAsync(
            ownerId, clinicAId,
            new UpsertPriceListItemInput(null, "COMP-01", "حشو كومبوزيت", "Composite Filling", ChargeLineKind.Tool, 50m, 60m, true, null),
            "dev-owner", null, null);
        priceItem.IsSuccess.Should().BeTrue();

        var chargeRes = await billingService.CreateChargeAsync(
            ownerId, clinicAId,
            new CreateChargeInput(
                null, patientA.Value.Id, null, null, null,
                StandardSessionPriceAmount: 400m,
                IsFreeSession: false,
                FreeSessionReason: null,
                CustomSessionPriceAmount: null,
                CustomPriceReason: null,
                DiscountPercent: 10m,
                DiscountFixedAmount: null,
                DiscountReason: "Family discount",
                ExtraChargeAmount: 40m,
                ExtraChargeReason: "Emergency after hours",
                Lines: [new ChargeLineInput(ChargeLineKind.Tool, priceItem.Value.Id, null, "Composite Kit", 1m, 50m, 60m)],
                Notes: "Dental Session #1",
                PostImmediately: true,
                OperationId: null),
            "dev-owner", null, null);
        chargeRes.IsSuccess.Should().BeTrue();
        // Gross: 400 + 60 + 40 = 500; Discount 10% = 50; Net = 450; Markup = 10
        chargeRes.Value.GrossSubtotalAmount.Should().Be(500m);
        chargeRes.Value.DiscountAmount.Should().Be(50m);
        chargeRes.Value.ToolsMarkupAmount.Should().Be(10m);
        chargeRes.Value.NetTotalAmount.Should().Be(450m);

        // First financial entry locks clinic currency/timezone/rounding
        var clinicEntity = await db.Clinics.AsNoTracking().FirstAsync(c => c.Id == clinicAId);
        clinicEntity.HasFinancialEntries.Should().BeTrue();

        // Skipped receipt number R-000001 recorded before payments -> next payments get R-000002 & R-000003
        var skipRes = await billingService.RecordSkippedReceiptAsync(
            ownerId, clinicAId, new RecordSkippedReceiptInput("R-000001", "Printer paper jam", null), "dev-owner", null, null);
        skipRes.IsSuccess.Should().BeTrue();

        // Multi-method partial payment: Cash 200 + Visa 100 = 300 paid, remaining due = 150
        var payRes = await billingService.RecordPaymentAsync(
            ownerId, clinicAId,
            new RecordPaymentInput(
                null, patientA.Value.Id, chargeRes.Value.Id, false,
                [new PaymentSplitInput(PaymentMethod.Cash, 200m, "Front desk cash"), new PaymentSplitInput(PaymentMethod.Visa, 100m, "POS #1")],
                null),
            "dev-owner", null, null);
        payRes.IsSuccess.Should().BeTrue();
        payRes.Value.Should().HaveCount(2);
        payRes.Value.Select(p => p.ReceiptNumber).Should().BeEquivalentTo(["R-000002", "R-000003"]);

        // Receipt PDF generation
        var pdfRes = await billingService.GenerateReceiptPdfAsync(ownerId, clinicAId, payRes.Value[0].Id, "dev-owner");
        pdfRes.IsSuccess.Should().BeTrue();
        Encoding.ASCII.GetString(pdfRes.Value.Bytes).Should().StartWith("%PDF-1.4");
        Encoding.ASCII.GetString(pdfRes.Value.Bytes).Should().Contain("R-000002");

        // Refund rejected for BasicCashier without billing.refund claim
        Guid cashPaymentId = payRes.Value.First(p => p.Method == PaymentMethod.Cash).Id;
        var noRefundClaim = await billingService.CreateRefundAsync(
            cashierId, clinicAId,
            new CreateRefundInput(null, patientA.Value.Id, cashPaymentId, chargeRes.Value.Id, PaymentMethod.Cash, 50m, "Adjustment", null),
            "dev-activate", null, null);
        noRefundClaim.IsFailure.Should().BeTrue();
        noRefundClaim.Error.Code.Should().Be("INSUFFICIENT_PERMISSION_REFUND");

        // Owner issues 50 Cash refund -> original payment intact, ledger balance = 450 - 300 + 50 = 200
        var refundRes = await billingService.CreateRefundAsync(
            ownerId, clinicAId,
            new CreateRefundInput(null, patientA.Value.Id, cashPaymentId, chargeRes.Value.Id, PaymentMethod.Cash, 50m, "Overcharge correction", null),
            "dev-owner", null, null);
        refundRes.IsSuccess.Should().BeTrue();

        var ledgerRes = await billingService.GetPatientLedgerAsync(ownerId, clinicAId, patientA.Value.Id, "dev-owner");
        ledgerRes.IsSuccess.Should().BeTrue();
        ledgerRes.Value.Payments.Should().HaveCount(2);
        ledgerRes.Value.Refunds.Should().HaveCount(1);
        ledgerRes.Value.CurrentBalance.Should().Be(200m);
        BillingCalculator.DeriveLedgerBalance(ledgerRes.Value.Entries.Select(e => e.SignedAmount)).Should().Be(200m);

        // Supplier + Lab Work Order: Patient charged 1200 (Posted), Lab Cost 400 accrued to Lab Supplier, Supplier paid 150 Cash
        var labSupplier = await billingService.CreateSupplierAsync(
            ownerId, clinicAId, new CreateSupplierInput(null, "معمل النور للأسنان", SupplierType.Lab, "01100001111", null, null), "dev-owner", null, null);
        labSupplier.IsSuccess.Should().BeTrue();

        var labWork = await billingService.CreateLabWorkAsync(
            ownerId, clinicAId,
            new CreateLabWorkBillingInput(null, patientA.Value.Id, null, null, labSupplier.Value.Id, "معمل النور", "Zirconia Crown #16", "#16", 400m, 1200m, null, null),
            "dev-owner", null, null);
        labWork.IsSuccess.Should().BeTrue();
        labWork.Value.ExpenseId.Should().NotBeNull();
        labWork.Value.ChargeId.Should().NotBeNull();

        var supPay = await billingService.RecordSupplierPaymentAsync(
            ownerId, clinicAId, new RecordSupplierPaymentInput(null, labSupplier.Value.Id, PaymentMethod.Cash, 150m, "Installment 1", null), "dev-owner", null, null);
        supPay.IsSuccess.Should().BeTrue();

        var suppliersList = await billingService.ListSuppliersAsync(ownerId, clinicAId, "dev-owner");
        suppliersList.IsSuccess.Should().BeTrue();
        var updatedSupplier = suppliersList.Value.Single(s => s.Id == labSupplier.Value.Id);
        updatedSupplier.TotalAccruedAmount.Should().Be(400m);
        updatedSupplier.TotalPaidAmount.Should().Be(150m);
        updatedSupplier.BalanceDueAmount.Should().Be(250m);

        // Operating cash expense: 20 Cash
        var expenseRes = await billingService.CreateExpenseAsync(
            ownerId, clinicAId, new CreateExpenseInput(null, "Supplies", "Cleaning liquid", PaymentMethod.Cash, 20m, null, true, clock.UtcNow, null),
            "dev-owner", null, null);
        expenseRes.IsSuccess.Should().BeTrue();

        // Portal billing query exposes ONLY patient-facing amounts (no tool cost, markup, or lab cost!)
        var portalSummary = await billingPortal.GetPatientBillingSummaryAsync(clinicAId, patientA.Value.Id);
        portalSummary.IsSuccess.Should().BeTrue();
        portalSummary.Value.TotalChargedAmount.Should().Be(1650m); // 450 + 1200
        portalSummary.Value.TotalPaidAmount.Should().Be(300m);
        portalSummary.Value.TotalRefundedAmount.Should().Be(50m);
        portalSummary.Value.CurrentBalanceDue.Should().Be(1400m); // 1650 - 300 + 50
        portalSummary.Value.ReceiptNumbers.Should().BeEquivalentTo(["R-000002", "R-000003"]);

        // Daily closing on 2026-06-08:
        // Cash: payments 200 - refund 50 - (supplierPay 150 + cashExpense 20 = 170) = -20 expected
        // Visa: payments 100 - 0 - 0 = 100 expected
        DateOnly closingDate = new(2026, 6, 8);
        var counted = new Dictionary<PaymentMethod, decimal>
        {
            [PaymentMethod.Cash] = -20m,
            [PaymentMethod.Visa] = 100m,
            [PaymentMethod.InstaPay] = 0m,
            [PaymentMethod.Wallet] = 0m
        };
        var closeRes = await billingService.CloseDayAsync(
            ownerId, clinicAId, new CloseDayInput(closingDate, counted, "End of day EOD", null), "dev-owner", null, null);
        closeRes.IsSuccess.Should().BeTrue();
        closeRes.Value.Status.Should().Be(DailyClosingStatus.Closed);
        closeRes.Value.FinanceExpensesExcluded.Should().BeFalse();
        closeRes.Value.TotalExpectedAmount.Should().Be(80m);
        closeRes.Value.TotalVarianceAmount.Should().Be(0m);

        // Closed day lock: new payment rejected with DAY_CLOSED_LOCKED
        var lockedPay = await billingService.RecordPaymentAsync(
            ownerId, clinicAId,
            new RecordPaymentInput(null, patientA.Value.Id, chargeRes.Value.Id, false, [new PaymentSplitInput(PaymentMethod.Cash, 10m, null)], null),
            "dev-owner", null, null);
        lockedPay.IsFailure.Should().BeTrue();
        lockedPay.Error.Code.Should().Be("DAY_CLOSED_LOCKED");

        // Reopen without claim rejected, with owner claim + reason succeeds and unlocks
        var noReopenClaim = await billingService.ReopenDayAsync(
            cashierId, clinicAId, new ReopenDayInput(closingDate, "Need correction", null), "dev-activate", null, null);
        noReopenClaim.IsFailure.Should().BeTrue();

        var reopenRes = await billingService.ReopenDayAsync(
            ownerId, clinicAId, new ReopenDayInput(closingDate, "Forgot evening Visa receipt", null), "dev-owner", null, null);
        reopenRes.IsSuccess.Should().BeTrue();
        reopenRes.Value.Status.Should().Be(DailyClosingStatus.Reopened);

        var unlockedPay = await billingService.RecordPaymentAsync(
            ownerId, clinicAId,
            new RecordPaymentInput(null, patientA.Value.Id, chargeRes.Value.Id, false, [new PaymentSplitInput(PaymentMethod.Visa, 10m, null)], null),
            "dev-owner", null, null);
        unlockedPay.IsSuccess.Should().BeTrue();
        unlockedPay.Value[0].ReceiptNumber.Should().Be("R-000004");

        // Clinical completed visit auto-creates Draft charge when Billing is ON
        var roomA = await structureService.CreateRoomAsync(ownerId, clinicAId, "B1", "غرفة", "Room B1", "dental", null, "dev-owner", null, null);
        var docA = await structureService.CreateDoctorAsync(ownerId, clinicAId, ownerId, "د. بلال", "Dr Bilal", "Spec", ["dental"], [roomA.Value.Id], 20, "dev-owner", null, null);
        var visitA = await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicAId, new StartVisitInput(null, null, patientA.Value.Id, docA.Value.Id, "dental", roomA.Value.Id, 20, true, null),
            "dev-owner", null, null);
        visitA.IsSuccess.Should().BeTrue();
        var completeVisit = await clinicalService.CompleteVisitAsync(
            ownerId, clinicAId, visitA.Value.Id, new CompleteVisitInput(false, null, null), "dev-owner", null, null);
        completeVisit.IsSuccess.Should().BeTrue();

        var visitCharges = await billingService.ListChargesAsync(ownerId, clinicAId, patientA.Value.Id, "dev-owner");
        visitCharges.Value.Should().Contain(c => c.VisitId == visitA.Value.Id && c.Status == ChargeStatus.Draft);

        // Module-off independence (Clinic B has clinical OFF, finance OFF, inventory OFF):
        // Manual charge with free-text tool lines works, lab work skips finance expense, closing excludes expenses
        var clinicBCharge = await billingService.CreateChargeAsync(
            ownerId, clinicBId,
            new CreateChargeInput(
                null, patientB.Value.Id, null, null, null,
                250m, false, null, null, null, null, null, null, null, null,
                [new ChargeLineInput(ChargeLineKind.Tool, null, null, "Free-text Suture Pack", 2m, 10m, 25m)],
                "Manual charge with clinical/inventory/finance off",
                true,
                null),
            "dev-owner", null, null);
        clinicBCharge.IsSuccess.Should().BeTrue();
        clinicBCharge.Value.NetTotalAmount.Should().Be(300m);

        var clinicBLab = await billingService.CreateLabWorkAsync(
            ownerId, clinicBId,
            new CreateLabWorkBillingInput(null, patientB.Value.Id, null, null, null, "External Lab", "Night Guard", null, 150m, 500m, null, null),
            "dev-owner", null, null);
        clinicBLab.IsSuccess.Should().BeTrue();
        clinicBLab.Value.ExpenseId.Should().BeNull();

        var clinicBPay = await billingService.RecordPaymentAsync(
            ownerId, clinicBId,
            new RecordPaymentInput(null, patientB.Value.Id, clinicBCharge.Value.Id, false, [new PaymentSplitInput(PaymentMethod.Cash, 300m, null)], null),
            "dev-owner", null, null);
        clinicBPay.IsSuccess.Should().BeTrue();

        var clinicBClose = await billingService.CloseDayAsync(
            ownerId, clinicBId, new CloseDayInput(closingDate, null, "Clinic B Closing without finance", null), "dev-owner", null, null);
        clinicBClose.IsSuccess.Should().BeTrue();
        clinicBClose.Value.FinanceExpensesExcluded.Should().BeTrue();
        clinicBClose.Value.TotalExpectedAmount.Should().Be(300m);

        // Cross-clinic isolation: Clinic A user reading Clinic B patient returns 404
        var crossClinic = await billingService.GetPatientLedgerAsync(ownerId, clinicAId, patientB.Value.Id, "dev-owner");
        crossClinic.IsFailure.Should().BeTrue();
        crossClinic.Error.Code.Should().Be("PATIENT_NOT_FOUND");

        // Dashboard widgets verify real aggregates
        var revWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicAId, "billing.revenue-summary", clock.UtcNow.AddDays(-1), clock.UtcNow.AddDays(1), null, "dev-owner");
        revWidget.IsSuccess.Should().BeTrue();
        revWidget.Value.PrimaryValue.Should().Be(310m);
    }

    private static async Task OnboardClinicAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة اختبار", "شارع 10", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, specialties.Select(s => new SpecialtySelectionInput(s, null, null)).ToList(), null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, $"p8-{clinicId:N}"[..18], null, null);
        (await clinicsService.CompleteOnboardingAsync(ownerId, clinicId, null, null)).IsSuccess.Should().BeTrue();
    }

    private static async Task ApproveAllPendingDevicesAsync(IClinicAuthorizationService authzService, Guid ownerId, Guid clinicId)
    {
        var devices = await authzService.ListClinicDevicesAsync(ownerId, clinicId, "dev-owner");
        foreach (var d in devices.Value.Where(x => !x.IsApproved))
        {
            await authzService.ApproveClinicDeviceAsync(ownerId, clinicId, d.DeviceId, "dev-owner", null, null);
        }
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
