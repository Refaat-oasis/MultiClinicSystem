namespace Core.Domain;

/// <summary>
/// Pure domain financial calculators for fixed cost recurrence expansion, payroll totals, and income vs expenses.
/// </summary>
public static class FinanceCalculator
{
    public static IReadOnlyList<FixedCostOccurrence> ExpandFixedCosts(
        IEnumerable<FixedCost> definitions,
        DateOnly fromDate,
        DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            return Array.Empty<FixedCostOccurrence>();
        }

        var occurrences = new List<FixedCostOccurrence>();
        foreach (var cost in definitions)
        {
            if (cost.IsVoided || !cost.IsActive)
            {
                continue;
            }

            DateOnly effectiveEnd = cost.EffectiveToDate.HasValue && cost.EffectiveToDate.Value < toDate
                ? cost.EffectiveToDate.Value
                : toDate;
            if (effectiveEnd < cost.EffectiveFromDate || effectiveEnd < fromDate)
            {
                continue;
            }

            switch (cost.Recurrence)
            {
                case FixedCostRecurrence.OneTime:
                    if (cost.EffectiveFromDate >= fromDate && cost.EffectiveFromDate <= effectiveEnd)
                    {
                        occurrences.Add(ToOccurrence(cost, cost.EffectiveFromDate));
                    }
                    break;

                case FixedCostRecurrence.Daily:
                    DateOnly dailyCursor = cost.EffectiveFromDate > fromDate ? cost.EffectiveFromDate : fromDate;
                    while (dailyCursor <= effectiveEnd)
                    {
                        occurrences.Add(ToOccurrence(cost, dailyCursor));
                        dailyCursor = dailyCursor.AddDays(1);
                    }
                    break;

                case FixedCostRecurrence.Weekly:
                    DateOnly weeklyCursor = cost.EffectiveFromDate;
                    if (weeklyCursor < fromDate)
                    {
                        int daysAhead = fromDate.DayNumber - weeklyCursor.DayNumber;
                        int weeksToSkip = (daysAhead + 6) / 7;
                        weeklyCursor = weeklyCursor.AddDays(weeksToSkip * 7);
                    }

                    while (weeklyCursor <= effectiveEnd)
                    {
                        occurrences.Add(ToOccurrence(cost, weeklyCursor));
                        weeklyCursor = weeklyCursor.AddDays(7);
                    }
                    break;

                case FixedCostRecurrence.Monthly:
                    int anchorDay = cost.EffectiveFromDate.Day;
                    int monthCursorYear = fromDate.Year;
                    int monthCursorMonth = fromDate.Month;
                    int endMonthIndex = (effectiveEnd.Year * 12) + effectiveEnd.Month;

                    while (((monthCursorYear * 12) + monthCursorMonth) <= endMonthIndex)
                    {
                        int clampedDay = Math.Min(anchorDay, DateTime.DaysInMonth(monthCursorYear, monthCursorMonth));
                        var occDate = new DateOnly(monthCursorYear, monthCursorMonth, clampedDay);
                        if (occDate >= cost.EffectiveFromDate && occDate >= fromDate && occDate <= effectiveEnd)
                        {
                            occurrences.Add(ToOccurrence(cost, occDate));
                        }

                        monthCursorMonth++;
                        if (monthCursorMonth > 12)
                        {
                            monthCursorMonth = 1;
                            monthCursorYear++;
                        }
                    }
                    break;

                case FixedCostRecurrence.Annual:
                    int annualMonth = cost.EffectiveFromDate.Month;
                    int annualDay = cost.EffectiveFromDate.Day;
                    for (int year = fromDate.Year; year <= effectiveEnd.Year; year++)
                    {
                        int clampedDay = Math.Min(annualDay, DateTime.DaysInMonth(year, annualMonth));
                        var occDate = new DateOnly(year, annualMonth, clampedDay);
                        if (occDate >= cost.EffectiveFromDate && occDate >= fromDate && occDate <= effectiveEnd)
                        {
                            occurrences.Add(ToOccurrence(cost, occDate));
                        }
                    }
                    break;
            }
        }

        return occurrences
            .OrderBy(o => o.OccurrenceDate)
            .ThenBy(o => o.FixedCostId)
            .ToList();
    }

    public static PayrollLineComputation ComputePayrollLine(
        Employee employee,
        IEnumerable<PayrollAdjustment> adjustments)
    {
        decimal bonuses = 0m;
        decimal deductions = 0m;
        decimal advances = 0m;

        foreach (var adj in adjustments)
        {
            if (adj.IsVoided || adj.EmployeeId != employee.Id)
            {
                continue;
            }

            switch (adj.AdjustmentType)
            {
                case PayrollAdjustmentType.Bonus:
                    bonuses += adj.Amount;
                    break;
                case PayrollAdjustmentType.Deduction:
                    deductions += adj.Amount;
                    break;
                case PayrollAdjustmentType.Advance:
                    advances += adj.Amount;
                    break;
            }
        }

        bonuses = RoundMoney(bonuses);
        deductions = RoundMoney(deductions);
        advances = RoundMoney(advances);
        decimal baseSalary = RoundMoney(employee.BaseSalaryAmount);
        decimal netPay = RoundMoney(baseSalary + bonuses - deductions - advances);

        return new PayrollLineComputation(
            employee.Id,
            employee.FullName,
            employee.DoctorId,
            baseSalary,
            bonuses,
            deductions,
            advances,
            netPay,
            employee.Currency);
    }

    public static IncomeVsExpensesSummary ComputeIncomeVsExpenses(
        decimal grossPayments,
        decimal refunds,
        decimal operatingExpenses,
        decimal supplierPayments,
        decimal supplierAccruals,
        decimal fixedCostsTotal,
        decimal payrollTotal)
    {
        decimal p = RoundMoney(grossPayments);
        decimal r = RoundMoney(refunds);
        decimal netIncome = RoundMoney(p - r);
        decimal opEx = RoundMoney(operatingExpenses);
        decimal supPay = RoundMoney(supplierPayments);
        decimal supAcc = RoundMoney(supplierAccruals);
        decimal fixedTotal = RoundMoney(fixedCostsTotal);
        decimal payroll = RoundMoney(payrollTotal);
        decimal totalExpenses = RoundMoney(opEx + supPay + fixedTotal + payroll);
        decimal netProfit = RoundMoney(netIncome - totalExpenses);

        return new IncomeVsExpensesSummary(
            p, r, netIncome, opEx, supPay, supAcc, fixedTotal, payroll, totalExpenses, netProfit);
    }

    private static FixedCostOccurrence ToOccurrence(FixedCost cost, DateOnly date) => new(
        cost.Id,
        cost.Category,
        cost.TitleAr,
        cost.TitleEn,
        date,
        cost.Amount,
        cost.Currency,
        cost.PaidFromMethod);

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

