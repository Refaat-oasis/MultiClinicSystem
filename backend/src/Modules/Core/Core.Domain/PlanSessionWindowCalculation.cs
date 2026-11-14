namespace Core.Domain;

public sealed record PlanSessionWindowCalculation(
    int SequenceNumber,
    int IntervalDaysFromPrevious,
    int WindowMinusDays,
    int WindowPlusDays,
    DateOnly TargetDate,
    DateOnly EarliestAllowedDate,
    DateOnly LatestAllowedDate);

