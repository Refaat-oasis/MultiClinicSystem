using BuildingBlocks.Domain;
using Core.Domain;
using Xunit;

namespace UnitTests;

public sealed class BillingDomainTests
{
    [Fact]
    public void Given_SessionAndToolCostVsCharged_When_Calculated_Then_MarkupIncomeAndGrossMatch()
    {
        var tools = new[]
        {
            new ChargeToolLineInput(ChargeLineKind.Tool, "Composite Resin", 2m, 50m, 60m)
        };

        ChargeCalculationResult calc = BillingCalculator.CalculateCharge(
            standardSessionPrice: 300m,
            isFreeSession: false,
            customSessionPrice: null,
            lines: tools,
            discountPercent: null,
            discountFixed: null,
            extraCharge: null,
            roundingRule: "NearestPound");

        Assert.Equal(300m, calc.BaseSessionPrice);
        Assert.Equal(100m, calc.ToolsTotalCost);
        Assert.Equal(120m, calc.ToolsTotalCharged);
        Assert.Equal(20m, calc.ToolsMarkupIncome);
        Assert.Equal(420m, calc.GrossSubtotal);
        Assert.Equal(420m, calc.RoundedNetTotal);
    }

    [Fact]
    public void Given_SingleToolCost50Charged60_When_ComputeMarkupCalled_Then_IncomeIs10()
    {
        decimal markup = BillingCalculator.ComputeMarkup(50m, 60m, 1m);

        Assert.Equal(10m, markup);
    }

    [Fact]
    public void Given_FreeSession_When_Calculated_Then_SessionPriceIsZeroButToolsAndExtrasRemain()
    {
        var tools = new[]
        {
            new ChargeToolLineInput(ChargeLineKind.Tool, "Gloves Kit", 1m, 15m, 25m)
        };

        ChargeCalculationResult calc = BillingCalculator.CalculateCharge(
            standardSessionPrice: 500m,
            isFreeSession: true,
            customSessionPrice: null,
            lines: tools,
            discountPercent: null,
            discountFixed: null,
            extraCharge: 30m,
            roundingRule: "None");

        Assert.Equal(0m, calc.BaseSessionPrice);
        Assert.Equal(25m, calc.ToolsTotalCharged);
        Assert.Equal(30m, calc.ExtraChargesTotal);
        Assert.Equal(55m, calc.RoundedNetTotal);
    }

    [Theory]
    [InlineData(200, true, 120)]
    [InlineData(200, false, 350)]
    public void Given_CustomSessionPriceLowerOrHigher_When_Calculated_Then_UsesCustomPrice(
        decimal standardPrice,
        bool isLower,
        decimal customPrice)
    {
        ChargeCalculationResult calc = BillingCalculator.CalculateCharge(
            standardSessionPrice: standardPrice,
            isFreeSession: false,
            customSessionPrice: customPrice,
            lines: null,
            discountPercent: null,
            discountFixed: null,
            extraCharge: null,
            roundingRule: "None");

        Assert.Equal(isLower ? customPrice : customPrice, calc.BaseSessionPrice);
        Assert.Equal(customPrice, calc.RoundedNetTotal);
    }

    [Fact]
    public void Given_PercentAndFixedDiscountsAndRoundingModes_When_Calculated_Then_AppliesDiscountsAndClinicRounding()
    {
        ChargeCalculationResult noneRounded = BillingCalculator.CalculateCharge(
            standardSessionPrice: 201m,
            isFreeSession: false,
            customSessionPrice: null,
            lines: null,
            discountPercent: 10m,
            discountFixed: 5.20m,
            extraCharge: null,
            roundingRule: "None");

        // 201 - 20.10 - 5.20 = 175.70
        Assert.Equal(25.30m, noneRounded.DiscountAmount);
        Assert.Equal(175.70m, noneRounded.UnroundedNetTotal);
        Assert.Equal(175.70m, noneRounded.RoundedNetTotal);

        decimal halfRounded = BillingCalculator.ApplyRounding(175.70m, "NearestHalf");
        decimal poundRounded = BillingCalculator.ApplyRounding(175.70m, "NearestPound");

        Assert.Equal(175.50m, halfRounded);
        Assert.Equal(176m, poundRounded);
    }

    [Fact]
    public void Given_PartialPaymentsAndRefundReversal_When_DeriveLedgerBalance_Then_BalanceEqualsSumAndChargeTracksRemaining()
    {
        Guid clinicId = Guid.CreateVersion7();
        Guid patientId = Guid.CreateVersion7();
        DateTimeOffset now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

        ChargeCalculationResult calc = BillingCalculator.CalculateCharge(500m, false, null, null, null, null, null, "NearestPound");
        var charge = new Charge(
            Guid.CreateVersion7(), clinicId, patientId, null, null, null,
            ChargeStatus.Posted, 500m, false, null, null, null, null, null, null, null, null,
            calc, "EGP", "NearestPound", null, "staff-1", null, now);

        charge.ApplyAllocation(200m, now.AddMinutes(5));
        charge.ApplyAllocation(100m, now.AddMinutes(6));
        Assert.Equal(200m, charge.GetRemainingDueAmount());

        charge.RecordRefund(50m, now.AddMinutes(10));
        Assert.Equal(250m, charge.GetRemainingDueAmount());

        // Ledger signed amounts: +500 charge, -200 cash, -100 visa, +50 refund reversal = +250 balance owed
        decimal ledgerBalance = BillingCalculator.DeriveLedgerBalance(new[] { 500m, -200m, -100m, 50m });
        Assert.Equal(250m, ledgerBalance);
    }

    [Fact]
    public void Given_DailyClosingWithFinanceOnVsOff_When_Computed_Then_MatchesPaymentsMinusRefundsMinusIncludedExpenses()
    {
        var payments = new Dictionary<PaymentMethod, decimal>
        {
            [PaymentMethod.Cash] = 1000m,
            [PaymentMethod.Visa] = 500m
        };
        var refunds = new Dictionary<PaymentMethod, decimal>
        {
            [PaymentMethod.Cash] = 100m
        };
        var expenses = new Dictionary<PaymentMethod, decimal>
        {
            [PaymentMethod.Cash] = 250m,
            [PaymentMethod.Visa] = 50m
        };
        var counted = new Dictionary<PaymentMethod, decimal>
        {
            [PaymentMethod.Cash] = 640m,
            [PaymentMethod.Visa] = 450m
        };

        IReadOnlyList<ClosingMethodSummary> withFinance = BillingCalculator.ComputeClosingBreakdown(
            payments, refunds, expenses, counted, includeFinanceExpenses: true);

        ClosingMethodSummary cashWith = withFinance.Single(x => x.Method == PaymentMethod.Cash);
        Assert.Equal(650m, cashWith.ExpectedAmount);
        Assert.Equal(640m, cashWith.CountedAmount);
        Assert.Equal(-10m, cashWith.Variance);

        IReadOnlyList<ClosingMethodSummary> withoutFinance = BillingCalculator.ComputeClosingBreakdown(
            payments, refunds, expenses, counted, includeFinanceExpenses: false);

        ClosingMethodSummary cashWithout = withoutFinance.Single(x => x.Method == PaymentMethod.Cash);
        Assert.Equal(0m, cashWithout.ExpensesTotal);
        Assert.Equal(900m, cashWithout.ExpectedAmount);
        Assert.Equal(-260m, cashWithout.Variance);
    }

    [Fact]
    public void Given_ClosedDay_When_ReopenedWithoutReason_Then_ThrowsAndWithReasonUnlocksWithAuditFields()
    {
        DateTimeOffset now = new(2026, 11, 13, 18, 0, 0, TimeSpan.Zero);
        var closing = new DailyClosing(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 11, 13),
            financeExpensesExcluded: false, totalExpectedAmount: 1000m, totalCountedAmount: 1000m,
            currency: "EGP", closedByUserId: "user-1", closedAtUtc: now, closeNotes: null, operationId: null);

        Assert.True(closing.IsLocked);
        Assert.Throws<InvalidOperationException>(() => closing.Reopen("owner-1", "", now.AddMinutes(5)));

        closing.Reopen("owner-1", "Late receipt entry", now.AddMinutes(5));

        Assert.False(closing.IsLocked);
        Assert.Equal(DailyClosingStatus.Reopened, closing.Status);
        Assert.Equal("Late receipt entry", closing.ReopenReason);
    }

    [Fact]
    public void Given_ChargeExceptionWithoutReason_When_Constructed_Then_ThrowsInvalidOperation()
    {
        Guid clinicId = Guid.CreateVersion7();
        Guid patientId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ChargeCalculationResult calcFree = BillingCalculator.CalculateCharge(200m, true, null, null, null, null, null, "None");
        Assert.Throws<InvalidOperationException>(() => new Charge(
            Guid.CreateVersion7(), clinicId, patientId, null, null, null, ChargeStatus.Posted,
            200m, true, null, null, null, null, null, null, null, null, calcFree,
            "EGP", "None", null, "u1", null, now));

        ChargeCalculationResult calcDiscount = BillingCalculator.CalculateCharge(200m, false, null, null, 10m, null, null, "None");
        Assert.Throws<InvalidOperationException>(() => new Charge(
            Guid.CreateVersion7(), clinicId, patientId, null, null, null, ChargeStatus.Posted,
            200m, false, null, null, null, 10m, null, "   ", null, null, calcDiscount,
            "EGP", "None", null, "u1", null, now));
    }
}
