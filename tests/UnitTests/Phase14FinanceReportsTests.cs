using BuildingBlocks.Domain;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class Phase14FinanceReportsTests
{
    [Fact]
    public void Given_MonthlyFixedCostOn31st_When_ExpandingAcrossFebAndMarch_Then_ClampsFebToLastDayAndKeepsMarch31()
    {
        Guid clinicId = GuidV7.NewGuid();
        var rent = new FixedCost(
            GuidV7.NewGuid(),
            clinicId,
            FixedCostCategory.Rent,
            "إيجار العيادة",
            "Clinic Rent",
            new Money(15000m, "EGP"),
            FixedCostRecurrence.Monthly,
            PaymentMethod.Cash,
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 4, 30),
            notes: null,
            operationId: null);

        var occurrences = FinanceCalculator.ExpandFixedCosts(
            [rent],
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 31));

        occurrences.Select(o => o.OccurrenceDate).Should().Equal(
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 31));
        occurrences.Sum(o => o.Amount).Should().Be(30000m);
    }

    [Fact]
    public void Given_WeeklyAndDailyAndOneTimeFixedCosts_When_ExpandingWindow_Then_CountsExactOccurrences()
    {
        Guid clinicId = GuidV7.NewGuid();
        var weeklyCleaning = new FixedCost(
            GuidV7.NewGuid(),
            clinicId,
            FixedCostCategory.Other,
            "نظافة أسبوعية",
            "Weekly Cleaning",
            new Money(500m, "EGP"),
            FixedCostRecurrence.Weekly,
            PaymentMethod.Cash,
            new DateOnly(2026, 9, 1),
            effectiveToDate: null,
            notes: null,
            operationId: null);

        var dailySecurity = new FixedCost(
            GuidV7.NewGuid(),
            clinicId,
            FixedCostCategory.Other,
            "حراسة يومية",
            "Daily Security",
            new Money(100m, "EGP"),
            FixedCostRecurrence.Daily,
            PaymentMethod.Cash,
            new DateOnly(2026, 9, 10),
            new DateOnly(2026, 9, 12),
            notes: null,
            operationId: null);

        var oneTimePermit = new FixedCost(
            GuidV7.NewGuid(),
            clinicId,
            FixedCostCategory.Maintenance,
            "صيانة جهاز",
            "Device Maintenance",
            new Money(1200m, "EGP"),
            FixedCostRecurrence.OneTime,
            PaymentMethod.Visa,
            new DateOnly(2026, 9, 5),
            effectiveToDate: null,
            notes: null,
            operationId: null);

        var inactiveCost = new FixedCost(
            GuidV7.NewGuid(),
            clinicId,
            FixedCostCategory.Water,
            "مياه",
            "Water",
            new Money(999m, "EGP"),
            FixedCostRecurrence.Monthly,
            PaymentMethod.Cash,
            new DateOnly(2026, 9, 1),
            effectiveToDate: null,
            notes: null,
            operationId: null);
        inactiveCost.SetActive(false);

        var occurrences = FinanceCalculator.ExpandFixedCosts(
            [weeklyCleaning, dailySecurity, oneTimePermit, inactiveCost],
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 15));

        // Weekly: Sept 1, 8, 15 => 3 * 500 = 1500
        // Daily: Sept 10, 11, 12 => 3 * 100 = 300
        // OneTime: Sept 5 => 1200
        // Total = 3000
        occurrences.Sum(o => o.Amount).Should().Be(3000m);
        occurrences.Where(o => o.PaidFromMethod == PaymentMethod.Visa).Sum(o => o.Amount).Should().Be(1200m);
    }

    [Fact]
    public void Given_EmployeeAndPayrollAdjustments_When_ComputingPayrollLine_Then_SumsBonusesSubtractsDeductionsAndAdvancesWithoutCommission()
    {
        Guid clinicId = GuidV7.NewGuid();
        var employee = new Employee(
            GuidV7.NewGuid(),
            clinicId,
            "أحمد محمود",
            "Senior Nurse",
            doctorId: null,
            phone: "01012345678",
            new Money(8500.50m, "EGP"),
            PaymentMethod.InstaPay,
            new DateOnly(2025, 1, 1),
            operationId: null);

        var adjustments = new List<PayrollAdjustment>
        {
            new(GuidV7.NewGuid(), clinicId, employee.Id, 2026, 9, PayrollAdjustmentType.Bonus, new Money(1250.25m, "EGP"), "Overtime", new DateOnly(2026, 9, 10), "owner", null),
            new(GuidV7.NewGuid(), clinicId, employee.Id, 2026, 9, PayrollAdjustmentType.Deduction, new Money(200.10m, "EGP"), "Late", new DateOnly(2026, 9, 12), "owner", null),
            new(GuidV7.NewGuid(), clinicId, employee.Id, 2026, 9, PayrollAdjustmentType.Advance, new Money(1500.00m, "EGP"), "Cash advance", new DateOnly(2026, 9, 15), "owner", null)
        };

        var result = FinanceCalculator.ComputePayrollLine(employee, adjustments);

        result.BaseSalaryAmount.Should().Be(8500.50m);
        result.BonusTotalAmount.Should().Be(1250.25m);
        result.DeductionTotalAmount.Should().Be(200.10m);
        result.AdvanceTotalAmount.Should().Be(1500.00m);
        result.NetPayAmount.Should().Be(8050.65m);
    }

    [Fact]
    public void Given_LedgerIncomeAndAllExpenseBuckets_When_ComputingIncomeVsExpenses_Then_MatchesExactCentTotals()
    {
        var summary = FinanceCalculator.ComputeIncomeVsExpenses(
            grossPayments: 52450.75m,
            refunds: 1200.25m,
            operatingExpenses: 4300.50m,
            supplierPayments: 6000.00m,
            supplierAccruals: 2500.00m,
            fixedCostsTotal: 15000.00m,
            payrollTotal: 18500.00m);

        summary.NetIncome.Should().Be(51250.50m);
        summary.TotalExpenses.Should().Be(43800.50m);
        summary.NetProfit.Should().Be(7450.00m);
    }
}

