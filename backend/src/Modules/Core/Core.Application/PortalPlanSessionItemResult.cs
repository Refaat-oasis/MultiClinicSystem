using Core.Domain;

namespace Core.Application;

public sealed record PortalPlanSessionItemResult(
    Guid SessionId,
    int SequenceNumber,
    string Title,
    DateOnly TargetDate,
    DateOnly EarliestAllowedDate,
    DateOnly LatestAllowedDate,
    PlanSessionStatus Status,
    Guid? LinkedAppointmentId,
    decimal? SessionPriceAmount);

