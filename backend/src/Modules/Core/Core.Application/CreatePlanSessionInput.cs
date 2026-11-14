namespace Core.Application;

public sealed record CreatePlanSessionInput(
    int SequenceNumber,
    string Title,
    int IntervalDaysFromPrevious,
    int WindowMinusDays,
    int WindowPlusDays,
    decimal SessionPriceAmount,
    decimal MaterialsCostAmount,
    string? Notes);

