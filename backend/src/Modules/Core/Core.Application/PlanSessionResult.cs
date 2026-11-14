using Core.Domain;

namespace Core.Application;

public sealed record PlanSessionResult(
    Guid Id,
    int SequenceNumber,
    string Title,
    int IntervalDaysFromPrevious,
    int WindowMinusDays,
    int WindowPlusDays,
    DateOnly TargetDate,
    DateOnly EarliestAllowedDate,
    DateOnly LatestAllowedDate,
    DateOnly? ActualOrSlippedDate,
    decimal SessionPriceAmount,
    decimal MaterialsCostAmount,
    Guid? LinkedAppointmentId,
    Guid? LinkedVisitId,
    Guid? LinkedChargeId,
    PlanSessionStatus Status,
    string? Notes);

