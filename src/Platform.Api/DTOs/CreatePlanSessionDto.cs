namespace Platform.Api.DTOs;

public sealed record CreatePlanSessionDto(
    int SequenceNumber,
    string Title,
    int IntervalDaysFromPrevious,
    int WindowMinusDays,
    int WindowPlusDays,
    decimal SessionPriceAmount,
    decimal MaterialsCostAmount,
    string? Notes);

