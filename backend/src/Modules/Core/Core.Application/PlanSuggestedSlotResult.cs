namespace Core.Application;

public sealed record PlanSuggestedSlotResult(
    Guid SessionId,
    int SequenceNumber,
    DateOnly TargetDate,
    DateOnly EarliestAllowedDate,
    DateOnly LatestAllowedDate,
    IReadOnlyList<DateOnly> SuggestedCandidateDates,
    AppointmentItemResult? BookedAppointment);

