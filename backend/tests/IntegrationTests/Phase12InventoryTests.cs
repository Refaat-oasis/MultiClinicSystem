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
public sealed class Phase12InventoryTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase12InventoryTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_InventoryModule_When_TestingPurchasesFefoConsumptionAlertsNegativePolicyStatementWidgetsAndModuleOff_Then_AllRulesEnforced()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero)
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
        var outbox = sp.GetRequiredService<OutboxService>();
        var clinicsService = sp.GetRequiredService<IClinicManagementService>();
        var structureService = sp.GetRequiredService<IClinicStructureService>();
        var patientService = sp.GetRequiredService<IPatientService>();
        var clinicalService = sp.GetRequiredService<IClinicalService>();
        var billingService = sp.GetRequiredService<IBillingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p12-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P12", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "عيادة الأمل", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicA = clinicARes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicA, $"hope-p12-{clinicA:N}"[..16], ["dental"]);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة النور", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicB = clinicBRes.Value.ClinicId;
        await OnboardClinicWithSlugAsync(clinicsService, ownerId, clinicB, $"noor-p12-{clinicB:N}"[..16], ["dental"]);

        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicA,
            new[]
            {
                new ModuleToggleInput("patients", true, "Phase12"),
                new ModuleToggleInput("scheduling", true, "Phase12"),
                new ModuleToggleInput("clinical", true, "Phase12"),
                new ModuleToggleInput("billing", true, "Phase12"),
                new ModuleToggleInput("finance", true, "Phase12"),
                new ModuleToggleInput("inventory", true, "Phase12")
            },
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        // Clinic B intentionally has inventory OFF, while patients/scheduling/clinical/billing are ON
        (await adminService.UpdateClinicModulesAsync(
            admin.Id,
            clinicB,
            new[]
            {
                new ModuleToggleInput("patients", true, "Phase12"),
                new ModuleToggleInput("scheduling", true, "Phase12"),
                new ModuleToggleInput("clinical", true, "Phase12"),
                new ModuleToggleInput("billing", true, "Phase12"),
                new ModuleToggleInput("inventory", false, "Phase12-Off")
            },
            "SuperStrongPass#2026",
            null,
            null)).IsSuccess.Should().BeTrue();

        // Structure & patient in Clinic A
        var roomA = await structureService.CreateRoomAsync(ownerId, clinicA, "P12-R1", "غرفة 1", "Room 1", "dental", null, "dev-owner", null, null);
        var docA = await structureService.CreateDoctorAsync(ownerId, clinicA, null, "د. ليلى فوزي", "استشاري", "Consultant", ["dental"], [roomA.Value.Id], 30, "dev-owner", null, null);
        var patA = await patientService.CreatePatientAsync(ownerId, clinicA, new CreatePatientInput(null, "سامي منصور", "01081000001", null, new DateOnly(1991, 4, 1), null, "Male", null, null, null, "dental", false, null), "dev-owner", null, null);

        // Supplier + Inventory Item (box of 30 default, 20% markup, threshold = 22)
        var supRes = await billingService.CreateSupplierAsync(ownerId, clinicA, new CreateSupplierInput(null, "مؤسسة الدلتا الطبية", SupplierType.MedicalSupplies, "01081000099", "Medical supplier", null), "dev-owner", null, null);
        supRes.IsSuccess.Should().BeTrue();
        Guid supplierId = supRes.Value.Id;

        var itemRes = await inventoryService.UpsertItemAsync(
            ownerId, clinicA,
            new UpsertInventoryItemInput(null, "GLV-01", "قفازات معقمة", "Sterile Gloves", "piece", 30, 10m, 20m, 22m, "dental", "Shelf A1", true, null),
            "dev-owner", null, null);
        itemRes.IsSuccess.Should().BeTrue();
        Guid itemId = itemRes.Value.Id;

        // Purchase 1 (pack size overridden from 30 -> 25, pack price = 300 => unit cost = 12.00; early expiry 2026-09-20 -> near-expiry alert; unpaid = 300)
        var inv1Res = await inventoryService.ReceivePurchaseInvoiceAsync(
            ownerId, clinicA,
            new ReceivePurchaseInvoiceInput(
                null, supplierId, "مؤسسة الدلتا الطبية", "INV-P12-1", new DateOnly(2026, 9, 1),
                0m, null, "Pack override purchase",
                [new PurchaseInvoiceLineInput(itemId, 25, 1, 300m, "BATCH-EARLY", new DateOnly(2026, 9, 20))],
                null),
            "dev-owner", null, null);
        inv1Res.IsSuccess.Should().BeTrue();
        inv1Res.Value.TotalAmount.Should().Be(300m);
        inv1Res.Value.Lines.Single().PackSize.Should().Be(25);
        inv1Res.Value.Lines.Single().TotalUnits.Should().Be(25);
        inv1Res.Value.Lines.Single().UnitCostAmount.Should().Be(12.00m);

        // Purchase 2 (pack size 30, pack price = 300 => unit cost = 10.00; later expiry 2027-06-01; partial payment 100 Cash)
        var inv2Res = await inventoryService.ReceivePurchaseInvoiceAsync(
            ownerId, clinicA,
            new ReceivePurchaseInvoiceInput(
                null, supplierId, "مؤسسة الدلتا الطبية", "INV-P12-2", new DateOnly(2026, 9, 1),
                100m, PaymentMethod.Cash, "Later batch",
                [new PurchaseInvoiceLineInput(itemId, 30, 1, 300m, "BATCH-LATE", new DateOnly(2027, 6, 1))],
                null),
            "dev-owner", null, null);
        inv2Res.IsSuccess.Should().BeTrue();
        inv2Res.Value.RemainingAmount.Should().Be(200m);

        // Supplier statement matches Finance ledger to the cent (total 600, paid 100, remaining 500)
        var stmtRes = await inventoryService.GetSupplierStatementAsync(ownerId, clinicA, supplierId, "dev-owner");
        stmtRes.IsSuccess.Should().BeTrue();
        stmtRes.Value.TotalPurchasedAmount.Should().Be(600m);
        stmtRes.Value.TotalPaidAmount.Should().Be(100m);
        stmtRes.Value.BalanceDueAmount.Should().Be(500m);
        stmtRes.Value.FinanceModuleEnabled.Should().BeTrue();
        stmtRes.Value.FinanceLedgerAccruedAmount.Should().Be(600m);
        stmtRes.Value.FinanceLedgerPaidAmount.Should().Be(100m);
        stmtRes.Value.FinanceLedgerBalanceDueAmount.Should().Be(500m);

        // Clinical Visit consumes 35 pieces -> FEFO must take all 25 from BATCH-EARLY first, then 10 from BATCH-LATE,
        // leaving 20 pieces (<= threshold 22 -> fires 1 LowStock alert!). Also item unit cost weighted/latest + 20% markup applied in Billing draft charge.
        var visitRes = await clinicalService.StartOrResumeVisitAsync(
            ownerId, clinicA,
            new StartVisitInput(null, null, patA.Value.Id, docA.Value.Id, "dental", roomA.Value.Id, 30, true, null),
            "dev-owner", null, null);
        visitRes.IsSuccess.Should().BeTrue();
        Guid visitId = visitRes.Value.Id;

        Guid completeOp = GuidV7.NewGuid();
        var completeRes = await clinicalService.CompleteVisitAsync(
            ownerId, clinicA, visitId,
            new CompleteVisitInput(false, null, completeOp, [new VisitToolUsageInput(itemId, 35m)]),
            "dev-owner", null, null);
        completeRes.IsSuccess.Should().BeTrue();

        // Process outbox to confirm idempotent event handling never double-deducts
        await outbox.ProcessPendingMessagesAsync();

        var batches = (await inventoryService.ListBatchesAsync(ownerId, clinicA, itemId, "dev-owner")).Value;
        batches.First(b => b.BatchNumber == "BATCH-EARLY").QuantityRemaining.Should().Be(0m);
        batches.First(b => b.BatchNumber == "BATCH-LATE").QuantityRemaining.Should().Be(20m);

        var sessionMovements = (await inventoryService.ListMovementsAsync(ownerId, clinicA, itemId, "dev-owner")).Value
            .Where(m => m.MovementType == nameof(StockMovementType.SessionUsed) && m.ReferenceVisitId == visitId)
            .ToList();
        sessionMovements.Should().HaveCount(2); // split across two batches once
        sessionMovements.Sum(m => Math.Abs(m.QuantityDelta)).Should().Be(35m);

        // Idempotent replay of ConsumeVisitToolsAsync with same operationId deducts nothing extra
        var replayConsume = await inventoryService.ConsumeVisitToolsAsync(
            clinicA, visitId, ownerId.ToString(), [new VisitToolUsageInput(itemId, 35m)], completeOp);
        replayConsume.IsSuccess.Should().BeTrue();
        (await inventoryService.ListItemsAsync(ownerId, clinicA, null, null, "dev-owner")).Value.Single().QuantityOnHand.Should().Be(20m);

        // Billing draft charge reflects FEFO weighted-average tool cost (25*12 + 10*10 = 400 / 35 => 11.43/unit) + 20% markup (13.72/unit)
        var charges = (await billingService.ListChargesAsync(ownerId, clinicA, patA.Value.Id, "dev-owner")).Value;
        charges.Should().ContainSingle(c => c.VisitId == visitId);
        var visitCharge = charges.Single(c => c.VisitId == visitId);
        visitCharge.ToolsTotalCostAmount.Should().Be(400.05m);
        visitCharge.ToolsTotalChargedAmount.Should().Be(480.20m);
        visitCharge.ToolsMarkupAmount.Should().Be(80.15m);

        // Low-stock + Near-expiry alerts exist (single low-stock alert)
        var alerts = (await inventoryService.ListAlertsAsync(ownerId, clinicA, false, "dev-owner")).Value;
        alerts.Count(a => a.Kind == nameof(InventoryAlertKind.LowStock)).Should().Be(1);
        alerts.Count(a => a.Kind == nameof(InventoryAlertKind.NearExpiry)).Should().Be(1);

        // Negative stock policy: default Block -> adjusting -25 fails INSUFFICIENT_STOCK
        var blockedAdj = await inventoryService.AdjustStockAsync(
            ownerId, clinicA, new AdjustStockInput(itemId, null, -25m, "Shrinkage check", null), "dev-owner", null, null);
        blockedAdj.IsFailure.Should().BeTrue();
        blockedAdj.Error.Code.Should().Be("INSUFFICIENT_STOCK");

        // Switch policy to AllowWithWarning -> adjustment succeeds, quantity = -5, NegativeStock alert created; LowStock not duplicated!
        (await inventoryService.UpdateSettingsAsync(
            ownerId, clinicA, new UpdateClinicInventorySettingInput(NegativeStockPolicy.AllowWithWarning, 30, null), "dev-owner", null, null)).IsSuccess.Should().BeTrue();

        var allowedAdj = await inventoryService.AdjustStockAsync(
            ownerId, clinicA, new AdjustStockInput(itemId, null, -25m, "Emergency override", null), "dev-owner", null, null);
        allowedAdj.IsSuccess.Should().BeTrue();
        allowedAdj.Value.BalanceAfter.Should().Be(-5m);

        var alertsAfterNeg = (await inventoryService.ListAlertsAsync(ownerId, clinicA, false, "dev-owner")).Value;
        alertsAfterNeg.Count(a => a.Kind == nameof(InventoryAlertKind.NegativeStock)).Should().Be(1);
        alertsAfterNeg.Count(a => a.Kind == nameof(InventoryAlertKind.LowStock)).Should().Be(1);

        // Dashboard widgets match live stock & usage data
        string[] invWidgets =
        [
            "inventory.low-stock",
            "inventory.near-expiry",
            "inventory.stock-value",
            "inventory.usage-by-item",
            "inventory.negative-stock-events"
        ];
        foreach (string key in invWidgets)
        {
            var w = await dashboardService.QueryWidgetAsync(ownerId, clinicA, key, null, null, null, "dev-owner");
            w.IsSuccess.Should().BeTrue($"widget {key} should succeed");
            if (key == "inventory.low-stock")
            {
                w.Value.PrimaryValue.Should().Be(1m);
            }
            if (key == "inventory.usage-by-item")
            {
                w.Value.PrimaryValue.Should().Be(35m);
            }
            if (key == "inventory.negative-stock-events")
            {
                w.Value.PrimaryValue.Should().BeGreaterThanOrEqualTo(1m);
            }
        }

        // Super Admin read-only inspect
        var inspect = await inventoryService.InspectClinicInventoryAsync(admin.Id, clinicA);
        inspect.IsSuccess.Should().BeTrue();
        inspect.Value.Items.Should().ContainSingle(i => i.Id == itemId);

        // Clinic B (inventory OFF): inventory endpoints return MODULE_DISABLED, yet visit completion + billing still work cleanly
        var offList = await inventoryService.ListItemsAsync(ownerId, clinicB, null, null, "dev-owner");
        offList.Error.Code.Should().Be("MODULE_DISABLED");

        var roomB = await structureService.CreateRoomAsync(ownerId, clinicB, "B-R1", "غرفة ب", "Room B", "dental", null, "dev-owner", null, null);
        var docB = await structureService.CreateDoctorAsync(ownerId, clinicB, null, "د. طارق", "أخصائي", "Specialist", ["dental"], [roomB.Value.Id], 30, "dev-owner", null, null);
        var patB = await patientService.CreatePatientAsync(ownerId, clinicB, new CreatePatientInput(null, "مها حسن", "01082000002", null, new DateOnly(1995, 2, 10), null, "Female", null, null, null, "dental", false, null), "dev-owner", null, null);

        var visitB = await clinicalService.StartOrResumeVisitAsync(ownerId, clinicB, new StartVisitInput(null, null, patB.Value.Id, docB.Value.Id, "dental", roomB.Value.Id, 30, true, null), "dev-owner", null, null);
        var compB = await clinicalService.CompleteVisitAsync(ownerId, clinicB, visitB.Value.Id, new CompleteVisitInput(false, null, null, null), "dev-owner", null, null);
        compB.IsSuccess.Should().BeTrue();
        await outbox.ProcessPendingMessagesAsync();
        (await billingService.ListChargesAsync(ownerId, clinicB, patB.Value.Id, "dev-owner")).Value.Should().ContainSingle(c => c.VisitId == visitB.Value.Id);

        // Switch inventory ON for Clinic B -> starts clean (0 items, 0 Clinic A data leaked)
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicB, [new ModuleToggleInput("inventory", true, "Enable later")], "SuperStrongPass#2026", null, null);
        var clinicBItems = await inventoryService.ListItemsAsync(ownerId, clinicB, null, null, "dev-owner");
        clinicBItems.IsSuccess.Should().BeTrue();
        clinicBItems.Value.Should().BeEmpty();
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
