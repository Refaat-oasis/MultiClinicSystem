namespace Core.Domain;

/// <summary>
/// Pure deterministic domain engine for average-time learning with minimum sample thresholds,
/// two-sided outlier trimming, bounded step-change clamping, and excused-session exclusion.
/// </summary>
public static class TimeEngineCalculator
{
    public static TimeEngineCalculationResult Calculate(TimeEngineCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        int defaultDuration = Math.Clamp(input.DefaultDurationMinutes, 5, 480);
        int currentApplied = input.CurrentAppliedDurationMinutes >= 5
            ? Math.Clamp(input.CurrentAppliedDurationMinutes, 5, 480)
            : defaultDuration;
        int minSampleSize = Math.Max(2, input.MinSampleSize);
        decimal trimFraction = Math.Clamp(input.TrimFraction, 0m, 0.40m);
        decimal maxStepPercent = Math.Clamp(input.MaxStepChangePercent, 1m, 100m);

        var nonExcused = new List<int>();
        int excusedCount = 0;

        foreach (TimeEngineSample sample in input.Samples)
        {
            if (sample.IsExcused)
            {
                excusedCount++;
                continue;
            }

            if (sample.RawMinutes > 0)
            {
                nonExcused.Add(sample.RawMinutes);
            }
        }

        int sampleCount = nonExcused.Count;
        if (sampleCount == 0)
        {
            return new TimeEngineCalculationResult(
                0,
                excusedCount,
                0m,
                defaultDuration,
                defaultDuration,
                false);
        }

        nonExcused.Sort();

        int trimEachSide = 0;
        if (trimFraction > 0m && sampleCount >= 5)
        {
            trimEachSide = Math.Max(1, (int)Math.Floor(sampleCount * trimFraction));
            while (trimEachSide > 0 && sampleCount - (2 * trimEachSide) < 1)
            {
                trimEachSide--;
            }
        }

        int keptCount = sampleCount - (2 * trimEachSide);
        long keptSum = 0;
        for (int i = trimEachSide; i < sampleCount - trimEachSide; i++)
        {
            keptSum += nonExcused[i];
        }

        decimal trimmedAverage = decimal.Round(keptSum / (decimal)keptCount, 2, MidpointRounding.AwayFromZero);

        if (sampleCount < minSampleSize)
        {
            int fallbackApplied = input.IsDoctorEngineEnabled ? currentApplied : defaultDuration;
            return new TimeEngineCalculationResult(
                sampleCount,
                excusedCount,
                trimmedAverage,
                currentApplied,
                fallbackApplied,
                false);
        }

        int targetRounded = Math.Clamp((int)Math.Round(trimmedAverage, MidpointRounding.AwayFromZero), 5, 480);
        decimal stepRatio = maxStepPercent / 100m;
        int minAllowed = Math.Max(5, (int)Math.Ceiling(currentApplied * (1m - stepRatio)));
        int maxAllowed = Math.Min(480, (int)Math.Floor(currentApplied * (1m + stepRatio)));

        // Guarantee at least 1 minute step when threshold > 0 and baseline differs cleanly
        if (maxAllowed < minAllowed)
        {
            minAllowed = currentApplied;
            maxAllowed = currentApplied;
        }

        int suggested = Math.Clamp(targetRounded, minAllowed, maxAllowed);
        int applied = input.IsDoctorEngineEnabled ? suggested : defaultDuration;
        bool isAdopted = input.IsDoctorEngineEnabled && applied != defaultDuration;

        return new TimeEngineCalculationResult(
            sampleCount,
            excusedCount,
            trimmedAverage,
            suggested,
            applied,
            isAdopted);
    }
}

