namespace Platform.Api.ViewModels;

public sealed record PortalPlanSessionItemViewModel(
    Guid SessionId,
    int SequenceNumber,
    string Title,
    DateOnly TargetDate,
    DateOnly EarliestAllowedDate,
    DateOnly LatestAllowedDate,
    string Status,
    Guid? LinkedAppointmentId,
    decimal? SessionPriceAmount);

