namespace Core.Domain;

public static class TreatmentPlanCalculator
{
    public static IReadOnlyList<PlanSessionWindowCalculation> CalculateSessionWindows(
        DateOnly startDate,
        IReadOnlyList<(int SequenceNumber, int IntervalDaysFromPrevious, int WindowMinusDays, int WindowPlusDays)> specs)
    {
        if (specs.Count == 0)
        {
            return Array.Empty<PlanSessionWindowCalculation>();
        }

        List<(int SequenceNumber, int IntervalDaysFromPrevious, int WindowMinusDays, int WindowPlusDays)> ordered =
            specs.OrderBy(s => s.SequenceNumber).ToList();

        var results = new List<PlanSessionWindowCalculation>(ordered.Count);
        DateOnly prevDate = startDate;

        for (int i = 0; i < ordered.Count; i++)
        {
            var spec = ordered[i];
            int interval = Math.Max(0, spec.IntervalDaysFromPrevious);
            int minusDays = Math.Max(0, spec.WindowMinusDays);
            int plusDays = Math.Max(0, spec.WindowPlusDays);

            DateOnly target = i == 0 && interval == 0
                ? startDate
                : prevDate.AddDays(interval);

            DateOnly earliest = target.AddDays(-minusDays);
            DateOnly latest = target.AddDays(plusDays);

            results.Add(new PlanSessionWindowCalculation(
                spec.SequenceNumber,
                interval,
                minusDays,
                plusDays,
                target,
                earliest,
                latest));

            prevDate = target;
        }

        return results;
    }

    public static IReadOnlyList<PlanSessionWindowCalculation> RecalculateAfterSlip(
        IReadOnlyList<PlanSessionWindowCalculation> sessions,
        int slippedSequenceNumber,
        DateOnly actualOrNewDate)
    {
        if (sessions.Count == 0)
        {
            return Array.Empty<PlanSessionWindowCalculation>();
        }

        List<PlanSessionWindowCalculation> ordered = sessions.OrderBy(s => s.SequenceNumber).ToList();
        var results = new List<PlanSessionWindowCalculation>(ordered.Count);
        DateOnly? anchorDate = null;

        foreach (PlanSessionWindowCalculation s in ordered)
        {
            if (s.SequenceNumber < slippedSequenceNumber)
            {
                results.Add(s);
                continue;
            }

            if (s.SequenceNumber == slippedSequenceNumber)
            {
                DateOnly earliest = actualOrNewDate.AddDays(-s.WindowMinusDays);
                DateOnly latest = actualOrNewDate.AddDays(s.WindowPlusDays);
                results.Add(s with
                {
                    TargetDate = actualOrNewDate,
                    EarliestAllowedDate = earliest,
                    LatestAllowedDate = latest
                });
                anchorDate = actualOrNewDate;
                continue;
            }

            DateOnly basePrev = anchorDate ?? s.TargetDate;
            DateOnly shiftedTarget = basePrev.AddDays(Math.Max(0, s.IntervalDaysFromPrevious));
            DateOnly shiftedEarliest = shiftedTarget.AddDays(-s.WindowMinusDays);
            DateOnly shiftedLatest = shiftedTarget.AddDays(s.WindowPlusDays);

            results.Add(s with
            {
                TargetDate = shiftedTarget,
                EarliestAllowedDate = shiftedEarliest,
                LatestAllowedDate = shiftedLatest
            });
            anchorDate = shiftedTarget;
        }

        return results;
    }

    public static PlanBalanceSummary ComputePlanBalance(
        TreatmentPlanMode mode,
        decimal fixedTotalAmount,
        IReadOnlyList<decimal> postedPerSessionAmounts,
        decimal totalPaidAmount,
        decimal totalRefundedAmount)
    {
        decimal contracted = mode == TreatmentPlanMode.FixedTotal
            ? RoundMoney(Math.Max(0m, fixedTotalAmount))
            : RoundMoney(postedPerSessionAmounts.Sum(x => Math.Max(0m, x)));

        decimal netPaid = RoundMoney(totalPaidAmount - totalRefundedAmount);
        decimal remaining = RoundMoney(Math.Max(0m, contracted - netPaid));

        return new PlanBalanceSummary(contracted, netPaid, remaining);
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

