namespace Platform.Api.ViewModels;

public sealed record PlanSessionViewModel(
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
    string Status,
    string? Notes);

