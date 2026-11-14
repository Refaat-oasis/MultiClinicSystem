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
public sealed class Phase9TreatmentPlansTests : IClassFixture<SqlDatabaseFixture>
{
    private readonly SqlDatabaseFixture _fixture;

    public Phase9TreatmentPlansTests(SqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Given_TreatmentPlans_When_RunningFixedPerSessionSlipClosingRefundAndPortal_Then_AllRulesAndIsolationHold()
    {
        var clock = new IntegrationTestClock
        {
            UtcNow = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero)
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
        var schedulingService = sp.GetRequiredService<ISchedulingService>();
        var billingService = sp.GetRequiredService<IBillingService>();
        var planService = sp.GetRequiredService<ITreatmentPlanService>();
        var planPortal = sp.GetRequiredService<ITreatmentPlanPortalQueries>();
        var dashboardService = sp.GetRequiredService<IDashboardService>();

        var admin = await EnsureEnrolledAdminAsync(db, adminAuth, totp, clock);
        string ownerEmail = $"p9-owner-{Guid.NewGuid():N}@clinic.local";
        var ownerRes = await adminService.CreateOwnerAsync(admin.Id, "Owner P9", ownerEmail, null, 3, null, null);
        await ActivateUserFromOutboxAsync(outbox, emailSender, authService, ownerEmail, "OwnerPass#2026");
        Guid ownerId = ownerRes.Value.UserId;

        var clinicARes = await clinicsService.CreateClinicAsync(ownerId, "عيادة خطط العلاج أ", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicAId = clinicARes.Value.ClinicId;
        await OnboardClinicAsync(clinicsService, ownerId, clinicAId, ["dental"]);

        var clinicBRes = await clinicsService.CreateClinicAsync(ownerId, "عيادة بدون خطط ب", "UTC", "EGP", "None", "dev-owner", null, null);
        Guid clinicBId = clinicBRes.Value.ClinicId;
        await OnboardClinicAsync(clinicsService, ownerId, clinicBId, ["dental"]);

        // Clinic A: patients, scheduling, billing, treatment-plans ON
        string[] modulesA = ["patients", "scheduling", "clinical", "billing", "finance", "treatment-plans"];
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicAId,
            modulesA.Select(m => new ModuleToggleInput(m, true, "P9")).ToList(),
            "SuperStrongPass#2026", null, null);

        // Clinic B: patients, scheduling, billing ON; treatment-plans OFF
        string[] modulesB = ["patients", "scheduling", "billing"];
        await adminService.UpdateClinicModulesAsync(
            admin.Id, clinicBId,
            modulesB.Select(m => new ModuleToggleInput(m, true, "P9")).ToList(),
            "SuperStrongPass#2026", null, null);

        var patientA = await patientService.CreatePatientAsync(
            ownerId, clinicAId,
            new CreatePatientInput(null, "وليد سامي", "01090000001", null, null, 35, "Male", null, null, null, null, false, null),
            "dev-owner", null, null);
        var patientB = await patientService.CreatePatientAsync(
            ownerId, clinicBId,
            new CreatePatientInput(null, "منى رشاد", "01090000002", null, null, 29, "Female", null, null, null, null, false, null),
            "dev-owner", null, null);

        var roomA = await structureService.CreateRoomAsync(ownerId, clinicAId, "R-P9", "غرفة خطط", "Plan Room", "dental", null, "dev-owner", null, null);
        var docA = await structureService.CreateDoctorAsync(ownerId, clinicAId, ownerId, "د. طارق", "Dr Tarek", "Ortho", ["dental"], [roomA.Value.Id], 30, "dev-owner", null, null);
        var catA = await schedulingService.CreateCategoryAsync(ownerId, clinicAId, "dental", "جلسة خطة", "Plan Session", 30, 500m, "EGP", true, "dev-owner", null, null);

        // Ensure open working hour for clinic A (default Mon-Sun/Sat-Thu logic or add Monday open)
        await structureService.SaveWorkingHoursAsync(
            ownerId, clinicAId, null,
            [new WorkingHourInput((int)DayOfWeek.Monday, "09:00", "17:00", false)],
            "dev-owner", null, null);

        // 1. Create a 4-session FixedTotal plan with upfront payment + installments
        DateOnly startDate = new(2026, 6, 8); // Monday
        var fixedPlanRes = await planService.CreatePlanAsync(
            ownerId,
            clinicAId,
            new CreateTreatmentPlanInput(
                null,
                patientA.Value.Id,
                docA.Value.Id,
                "تقويم أسنان شامل",
                TreatmentPlanMode.FixedTotal,
                FixedTotalAmount: 4000m,
                IncludedMaterialsCostAmount: 300m,
                UpfrontAmount: 1000m,
                StartDate: startDate,
                ShowCostToPatient: true,
                Notes: "4 sessions spread across weeks",
                Sessions:
                [
                    new CreatePlanSessionInput(1, "التقييم والتركيب المبدئي", 0, 1, 2, 0m, 100m, null),
                    new CreatePlanSessionInput(2, "الشد الأول", 7, 2, 3, 0m, 80m, null),
                    new CreatePlanSessionInput(3, "الشد الثاني", 14, 2, 3, 0m, 60m, null),
                    new CreatePlanSessionInput(4, "المتابعة والتثبيت", 14, 2, 4, 0m, 60m, null)
                ],
                Installments:
                [
                    new CreatePlanInstallmentInput(0, true, new DateOnly(2026, 6, 8), 1000m),
                    new CreatePlanInstallmentInput(1, false, new DateOnly(2026, 6, 22), 1500m),
                    new CreatePlanInstallmentInput(2, false, new DateOnly(2026, 7, 6), 1500m)
                ],
                OperationId: GuidV7.NewGuid()),
            "dev-owner", null, null);

        fixedPlanRes.IsSuccess.Should().BeTrue();
        var plan = fixedPlanRes.Value;
        plan.Sessions.Should().HaveCount(4);
        plan.Installments.Should().HaveCount(3);
        plan.TotalBilledOrContractedAmount.Should().Be(4000m);
        plan.RemainingBalanceAmount.Should().Be(4000m);

        // 2. Suggest slot & book appointment from Session 1 window
        var suggestRes = await planService.SuggestOrBookSessionAppointmentAsync(
            ownerId,
            clinicAId,
            plan.Id,
            plan.Sessions[0].Id,
            new SchedulePlanSessionInput(catA.Value.Id, roomA.Value.Id, new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero), GuidV7.NewGuid()),
            "dev-owner", null, null);
        suggestRes.IsSuccess.Should().BeTrue();
        suggestRes.Value.SuggestedCandidateDates.Should().Contain(new DateOnly(2026, 6, 8));
        suggestRes.Value.BookedAppointment.Should().NotBeNull();
        suggestRes.Value.BookedAppointment!.PlanSessionId.Should().Be(plan.Sessions[0].Id);

        // 3. Pay upfront deposit (1000m Cash) -> receipt allocated via NumberBlock, balance drops to 3000m
        var payUpfront = await planService.PayInstallmentAsync(
            ownerId, clinicAId, plan.Id, plan.Installments[0].Id,
            new PayPlanInstallmentInput(PaymentMethod.Cash, 1000m, "Upfront deposit", GuidV7.NewGuid()),
            "dev-owner", null, null);
        payUpfront.IsSuccess.Should().BeTrue();
        payUpfront.Value.PaidAmount.Should().Be(1000m);
        payUpfront.Value.RemainingBalanceAmount.Should().Be(3000m);
        payUpfront.Value.Installments[0].Status.Should().Be(PlanInstallmentStatus.Paid);

        // 4. Complete Session 1 & Session 2 -> FixedTotal posts NO Charges, tracks extra materials cost internally
        var chargesBefore = (await billingService.ListChargesAsync(ownerId, clinicAId, patientA.Value.Id, "dev-owner")).Value.Count;

        var comp1 = await planService.CompletePlanSessionAsync(
            ownerId, clinicAId, plan.Id, plan.Sessions[0].Id,
            new CompletePlanSessionInput(new DateOnly(2026, 6, 8), null, 50m, GuidV7.NewGuid()),
            "dev-owner", null, null);
        comp1.IsSuccess.Should().BeTrue();
        comp1.Value.CompletedSessionsCount.Should().Be(1);
        comp1.Value.CompletionPercentage.Should().Be(25m);
        comp1.Value.IncludedMaterialsCostAmount.Should().Be(350m);
        comp1.Value.Sessions[0].LinkedChargeId.Should().BeNull();

        var chargesAfterFixed = (await billingService.ListChargesAsync(ownerId, clinicAId, patientA.Value.Id, "dev-owner")).Value.Count;
        chargesAfterFixed.Should().Be(chargesBefore);

        // 5. Slip Session 2 from 2026-06-15 to 2026-06-19 (+4 days) -> Session 3 & 4 targets shift cleanly
        var slipRes = await planService.RecordSessionSlipAsync(
            ownerId, clinicAId, plan.Id, plan.Sessions[1].Id,
            new RecordPlanSessionSlipInput(new DateOnly(2026, 6, 19), "Patient travel"),
            "dev-owner", null, null);
        slipRes.IsSuccess.Should().BeTrue();
        slipRes.Value.Sessions[1].Status.Should().Be(PlanSessionStatus.Delayed);
        slipRes.Value.Sessions[1].TargetDate.Should().Be(new DateOnly(2026, 6, 19));
        slipRes.Value.Sessions[2].TargetDate.Should().Be(new DateOnly(2026, 7, 3)); // +14 days from 2026-06-19
        slipRes.Value.Sessions[3].TargetDate.Should().Be(new DateOnly(2026, 7, 17)); // +14 days from 2026-07-03

        // 6. Pay installment 1 (1500m Visa) + Refund part of upfront deposit (200m Cash) + verify DailyClosing
        await planService.PayInstallmentAsync(
            ownerId, clinicAId, plan.Id, plan.Installments[1].Id,
            new PayPlanInstallmentInput(PaymentMethod.Visa, 1500m, "First installment", GuidV7.NewGuid()),
            "dev-owner", null, null);

        var refundDep = await planService.RefundInstallmentAsync(
            ownerId, clinicAId, plan.Id, plan.Installments[0].Id,
            new RefundPlanDepositInput(200m, "Partial deposit refund", GuidV7.NewGuid()),
            "dev-owner", null, null);
        refundDep.IsSuccess.Should().BeTrue();
        refundDep.Value.RefundedAmount.Should().Be(200m);
        refundDep.Value.PaidAmount.Should().Be(2500m);
        refundDep.Value.RemainingBalanceAmount.Should().Be(1700m); // 4000 - (2500 - 200)

        var closingPreview = await billingService.GetDailyClosingPreviewAsync(ownerId, clinicAId, new DateOnly(2026, 6, 8), "dev-owner");
        closingPreview.IsSuccess.Should().BeTrue();
        var cashRow = closingPreview.Value.Methods.First(m => m.Method == PaymentMethod.Cash);
        cashRow.PaymentsTotal.Should().Be(1000m);
        cashRow.RefundsTotal.Should().Be(200m);
        cashRow.ExpectedTotal.Should().Be(800m);
        closingPreview.Value.Methods.First(m => m.Method == PaymentMethod.Visa).PaymentsTotal.Should().Be(1500m);

        // 7. Per-Session plan posts its own Charge when a session is completed
        var perSessionPlan = await planService.CreatePlanAsync(
            ownerId,
            clinicAId,
            new CreateTreatmentPlanInput(
                null, patientA.Value.Id, docA.Value.Id, "جلسات تبييض", TreatmentPlanMode.PerSession,
                0m, 0m, 0m, new DateOnly(2026, 6, 8), true, null,
                [
                    new CreatePlanSessionInput(1, "تبييض 1", 0, 1, 2, 650m, 90m, null),
                    new CreatePlanSessionInput(2, "تبييض 2", 7, 1, 2, 650m, 90m, null)
                ],
                [], GuidV7.NewGuid()),
            "dev-owner", null, null);
        perSessionPlan.IsSuccess.Should().BeTrue();

        var compPerSession = await planService.CompletePlanSessionAsync(
            ownerId, clinicAId, perSessionPlan.Value.Id, perSessionPlan.Value.Sessions[0].Id,
            new CompletePlanSessionInput(new DateOnly(2026, 6, 8), null, null, GuidV7.NewGuid()),
            "dev-owner", null, null);
        compPerSession.IsSuccess.Should().BeTrue();
        compPerSession.Value.Sessions[0].LinkedChargeId.Should().NotBeNull();
        compPerSession.Value.TotalBilledOrContractedAmount.Should().Be(650m);
        compPerSession.Value.RemainingBalanceAmount.Should().Be(650m);

        // 8. Portal queries respect ShowCostToPatient, never expose internal material costs, and log PatientAccessLog
        var portalVisible = await planPortal.GetPatientTreatmentPlansForPortalAsync(clinicAId, patientA.Value.Id);
        portalVisible.IsSuccess.Should().BeTrue();
        portalVisible.Value.First(p => p.PlanId == plan.Id).TotalCostToPatient.Should().Be(4000m);

        await planService.SetPortalCostVisibilityAsync(ownerId, clinicAId, plan.Id, false, "dev-owner", null, null);
        var portalHidden = await planPortal.GetPatientTreatmentPlansForPortalAsync(clinicAId, patientA.Value.Id);
        portalHidden.Value.First(p => p.PlanId == plan.Id).TotalCostToPatient.Should().BeNull();
        portalHidden.Value.First(p => p.PlanId == plan.Id).RemainingBalanceToPatient.Should().BeNull();

        (await db.PatientAccessLogs.AnyAsync(l => l.ClinicId == clinicAId && l.ActorType == "Patient" && l.Reason == "PortalViewTreatmentPlans"))
            .Should().BeTrue();

        // 9. Dashboard widgets match real source data
        var activeWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicAId, "treatment-plans.active-plans", null, null, null, "dev-owner");
        activeWidget.IsSuccess.Should().BeTrue();
        activeWidget.Value.PrimaryValue.Should().Be(2m);

        var revenueWidget = await dashboardService.QueryWidgetAsync(ownerId, clinicAId, "treatment-plans.plan-revenue", null, null, null, "dev-owner");
        revenueWidget.IsSuccess.Should().BeTrue();
        revenueWidget.Value.PrimaryValue.Should().Be(2300m); // 2500 paid - 200 refunded

        // 10. Module-off independence: Clinic B has treatment-plans OFF, but scheduling & billing still work
        var offPlans = await planService.ListPlansAsync(ownerId, clinicBId, null, "dev-owner");
        offPlans.IsFailure.Should().BeTrue();
        offPlans.Error.Code.Should().Be("MODULE_DISABLED");

        var clinicBCharge = await billingService.CreateChargeAsync(
            ownerId, clinicBId,
            new CreateChargeInput(null, patientB.Value.Id, null, null, null, 200m, false, null, null, null, null, null, null, null, null, null, "Clinic B charge works", true, null),
            "dev-owner", null, null);
        clinicBCharge.IsSuccess.Should().BeTrue();

        var clinicBCat = await schedulingService.CreateCategoryAsync(ownerId, clinicBId, "dental", "كشف", "Exam", 20, 200m, "EGP", false, "dev-owner", null, null);
        clinicBCat.IsSuccess.Should().BeTrue();
    }

    private static async Task OnboardClinicAsync(
        IClinicManagementService clinicsService,
        Guid ownerId,
        Guid clinicId,
        IReadOnlyList<string> specialties)
    {
        await clinicsService.CompleteWelcomeStepAsync(ownerId, clinicId, null, null);
        await clinicsService.SaveOnboardingDetailsAsync(ownerId, clinicId, "عيادة خطط", "شارع التحرير", "Cairo", "Cairo", "01000000000", null, null, null, null, null);
        await clinicsService.SaveOnboardingTypesAsync(ownerId, clinicId, specialties.Select(s => new SpecialtySelectionInput(s, null, null)).ToList(), null, null);
        await clinicsService.SaveOnboardingSlugAsync(ownerId, clinicId, $"p9-{clinicId:N}"[..18], null, null);
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
