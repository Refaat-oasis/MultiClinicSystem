namespace Core.Domain;

/// <summary>
/// Pure domain calculator enforcing all billing markup, discount, rounding, ledger balance, and closing math rules.
/// </summary>
public static class BillingCalculator
{
    public static decimal ComputeMarkup(decimal unitCost, decimal unitChargedPrice, decimal quantity = 1m)
    {
        if (quantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        }

        return Math.Round((unitChargedPrice - unitCost) * quantity, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ApplyRounding(decimal amount, string? roundingRule)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return (roundingRule?.Trim()) switch
        {
            "None" => Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            "NearestHalf" => Math.Round(amount * 2m, 0, MidpointRounding.AwayFromZero) / 2m,
            _ => Math.Round(amount, 0, MidpointRounding.AwayFromZero) // NearestPound default
        };
    }

    public static ChargeCalculationResult CalculateCharge(
        decimal standardSessionPrice,
        bool isFreeSession,
        decimal? customSessionPrice,
        IReadOnlyList<ChargeToolLineInput>? lines,
        decimal? discountPercent,
        decimal? discountFixed,
        decimal? extraCharge,
        string? roundingRule)
    {
        if (standardSessionPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardSessionPrice), "Standard price cannot be negative.");
        }

        if (customSessionPrice.HasValue && customSessionPrice.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(customSessionPrice), "Custom price cannot be negative.");
        }

        if (discountPercent.HasValue && (discountPercent.Value < 0m || discountPercent.Value > 100m))
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount percent must be between 0 and 100.");
        }

        if (discountFixed.HasValue && discountFixed.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(discountFixed), "Fixed discount cannot be negative.");
        }

        if (extraCharge.HasValue && extraCharge.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(extraCharge), "Extra charge cannot be negative.");
        }

        decimal effectiveSessionPrice = isFreeSession
            ? 0m
            : Math.Round(customSessionPrice ?? standardSessionPrice, 2, MidpointRounding.AwayFromZero);

        decimal toolsCost = 0m;
        decimal toolsCharged = 0m;
        decimal otherLinesCharged = 0m;

        if (lines != null)
        {
            foreach (ChargeToolLineInput line in lines)
            {
                if (line.Quantity <= 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(lines), "Line quantity must be positive.");
                }

                if (line.UnitCost < 0m || line.UnitChargedPrice < 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(lines), "Line amounts cannot be negative.");
                }

                decimal lineCost = Math.Round(line.UnitCost * line.Quantity, 2, MidpointRounding.AwayFromZero);
                decimal lineCharged = Math.Round(line.UnitChargedPrice * line.Quantity, 2, MidpointRounding.AwayFromZero);

                if (line.Kind == ChargeLineKind.Tool)
                {
                    toolsCost += lineCost;
                    toolsCharged += lineCharged;
                }
                else
                {
                    toolsCost += lineCost;
                    otherLinesCharged += lineCharged;
                }
            }
        }

        decimal extras = Math.Round(extraCharge ?? 0m, 2, MidpointRounding.AwayFromZero);
        decimal gross = Math.Round(effectiveSessionPrice + toolsCharged + otherLinesCharged + extras, 2, MidpointRounding.AwayFromZero);

        decimal pctDiscount = discountPercent.HasValue && discountPercent.Value > 0m
            ? Math.Round(gross * (discountPercent.Value / 100m), 2, MidpointRounding.AwayFromZero)
            : 0m;
        decimal fixedDisc = discountFixed.HasValue && discountFixed.Value > 0m
            ? Math.Round(discountFixed.Value, 2, MidpointRounding.AwayFromZero)
            : 0m;

        decimal totalDiscount = Math.Min(gross, Math.Round(pctDiscount + fixedDisc, 2, MidpointRounding.AwayFromZero));
        decimal unroundedNet = Math.Max(0m, Math.Round(gross - totalDiscount, 2, MidpointRounding.AwayFromZero));
        decimal roundedNet = ApplyRounding(unroundedNet, roundingRule);

        return new ChargeCalculationResult(
            BaseSessionPrice: effectiveSessionPrice,
            ToolsTotalCost: Math.Round(toolsCost, 2, MidpointRounding.AwayFromZero),
            ToolsTotalCharged: Math.Round(toolsCharged, 2, MidpointRounding.AwayFromZero),
            ToolsMarkupIncome: Math.Round(toolsCharged - toolsCost, 2, MidpointRounding.AwayFromZero),
            ExtraChargesTotal: Math.Round(extras + otherLinesCharged, 2, MidpointRounding.AwayFromZero),
            GrossSubtotal: gross,
            DiscountAmount: totalDiscount,
            UnroundedNetTotal: unroundedNet,
            RoundedNetTotal: roundedNet);
    }

    public static decimal DeriveLedgerBalance(IEnumerable<decimal> signedEntryAmounts)
    {
        return Math.Round(signedEntryAmounts.Sum(), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeExpectedClosingForMethod(
        decimal paymentsAmount,
        decimal refundsAmount,
        decimal expensesAndSupplierPaymentsAmount,
        bool includeFinanceExpenses)
    {
        decimal expenses = includeFinanceExpenses ? expensesAndSupplierPaymentsAmount : 0m;
        return Math.Round(paymentsAmount - refundsAmount - expenses, 2, MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<ClosingMethodSummary> ComputeClosingBreakdown(
        IReadOnlyDictionary<PaymentMethod, decimal> paymentsByMethod,
        IReadOnlyDictionary<PaymentMethod, decimal> refundsByMethod,
        IReadOnlyDictionary<PaymentMethod, decimal> expensesByMethod,
        IReadOnlyDictionary<PaymentMethod, decimal> countedByMethod,
        bool includeFinanceExpenses)
    {
        var results = new List<ClosingMethodSummary>(4);
        foreach (PaymentMethod method in Enum.GetValues<PaymentMethod>())
        {
            decimal paid = paymentsByMethod.GetValueOrDefault(method, 0m);
            decimal refunded = refundsByMethod.GetValueOrDefault(method, 0m);
            decimal exp = includeFinanceExpenses ? expensesByMethod.GetValueOrDefault(method, 0m) : 0m;
            decimal expected = ComputeExpectedClosingForMethod(paid, refunded, exp, includeFinanceExpenses);
            decimal counted = Math.Round(countedByMethod.GetValueOrDefault(method, expected), 2, MidpointRounding.AwayFromZero);
            decimal variance = Math.Round(counted - expected, 2, MidpointRounding.AwayFromZero);
            results.Add(new ClosingMethodSummary(method, paid, refunded, exp, expected, counted, variance));
        }

        return results;
    }
}

