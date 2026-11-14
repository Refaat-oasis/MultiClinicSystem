using Core.Domain;
using FluentAssertions;

namespace UnitTests;

public class TreatmentPlanDomainTests
{
    [Fact]
    public void Given_StartDateAndIntervals_When_CalculatingSessionWindows_Then_ComputesSequentialTargetsAndWindows()
    {
        var startDate = new DateOnly(2026, 6, 1);
        var specs = new (int, int, int, int)[]
        {
            (1, 0, 1, 2),
            (2, 7, 2, 3),
            (3, 14, 3, 4),
            (4, 14, 2, 5)
        };

        var windows = TreatmentPlanCalculator.CalculateSessionWindows(startDate, specs);

        windows.Should().HaveCount(4);
        windows[0].TargetDate.Should().Be(new DateOnly(2026, 6, 1));
        windows[0].EarliestAllowedDate.Should().Be(new DateOnly(2026, 5, 31));
        windows[0].LatestAllowedDate.Should().Be(new DateOnly(2026, 6, 3));

        windows[1].TargetDate.Should().Be(new DateOnly(2026, 6, 8));
        windows[1].EarliestAllowedDate.Should().Be(new DateOnly(2026, 6, 6));
        windows[1].LatestAllowedDate.Should().Be(new DateOnly(2026, 6, 11));

        windows[2].TargetDate.Should().Be(new DateOnly(2026, 6, 22));
        windows[3].TargetDate.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void Given_SlippedSession_When_RecalculatingWindows_Then_SubsequentSessionsShiftPreservingIntervals()
    {
        var initial = TreatmentPlanCalculator.CalculateSessionWindows(
            new DateOnly(2026, 6, 1),
            new (int, int, int, int)[]
            {
                (1, 0, 1, 2),
                (2, 7, 2, 3),
                (3, 7, 2, 3),
                (4, 14, 2, 4)
            });

        // Session 2 slips from 2026-06-08 to 2026-06-12 (+4 days)
        var shifted = TreatmentPlanCalculator.RecalculateAfterSlip(initial, 2, new DateOnly(2026, 6, 12));

        shifted[0].TargetDate.Should().Be(new DateOnly(2026, 6, 1)); // unchanged
        shifted[1].TargetDate.Should().Be(new DateOnly(2026, 6, 12));
        shifted[2].TargetDate.Should().Be(new DateOnly(2026, 6, 19));
        shifted[2].EarliestAllowedDate.Should().Be(new DateOnly(2026, 6, 17));
        shifted[2].LatestAllowedDate.Should().Be(new DateOnly(2026, 6, 22));
        shifted[3].TargetDate.Should().Be(new DateOnly(2026, 7, 3));
    }

    [Fact]
    public void Given_FixedTotalAndPerSessionModes_When_ComputingBalance_Then_AppliesCorrectFormulaIncludingRefunds()
    {
        var fixedSummary = TreatmentPlanCalculator.ComputePlanBalance(
            TreatmentPlanMode.FixedTotal,
            fixedTotalAmount: 4000m,
            postedPerSessionAmounts: [0m, 0m],
            totalPaidAmount: 1500m,
            totalRefundedAmount: 300m);

        fixedSummary.TotalBilledOrContractedAmount.Should().Be(4000m);
        fixedSummary.NetPaidAmount.Should().Be(1200m);
        fixedSummary.RemainingBalanceAmount.Should().Be(2800m);

        var perSessionSummary = TreatmentPlanCalculator.ComputePlanBalance(
            TreatmentPlanMode.PerSession,
            fixedTotalAmount: 4000m,
            postedPerSessionAmounts: [500m, 650m],
            totalPaidAmount: 500m,
            totalRefundedAmount: 0m);

        perSessionSummary.TotalBilledOrContractedAmount.Should().Be(1150m);
        perSessionSummary.NetPaidAmount.Should().Be(500m);
        perSessionSummary.RemainingBalanceAmount.Should().Be(650m);
    }

    [Fact]
    public void Given_InstallmentScheduleItem_When_ApplyingPaymentRefundingAndPassingDueDate_Then_StatusesTransitionAccurately()
    {
        var today = new DateOnly(2026, 6, 10);
        var nowUtc = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var overdueItem = new PlanPaymentScheduleItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, false,
            dueDate: new DateOnly(2026, 6, 5), amount: 1000m, "EGP", today, nowUtc);

        overdueItem.Status.Should().Be(PlanInstallmentStatus.Overdue);

        overdueItem.ApplyPayment(400m, Guid.NewGuid(), today, nowUtc);
        overdueItem.Status.Should().Be(PlanInstallmentStatus.PartiallyPaid);
        overdueItem.GetRemainingAmount().Should().Be(600m);

        overdueItem.ApplyPayment(600m, Guid.NewGuid(), today, nowUtc);
        overdueItem.Status.Should().Be(PlanInstallmentStatus.Paid);
        overdueItem.GetRemainingAmount().Should().Be(0m);

        overdueItem.ReversePayment(1000m, today, nowUtc);
        overdueItem.Status.Should().Be(PlanInstallmentStatus.Refunded);
    }
}

