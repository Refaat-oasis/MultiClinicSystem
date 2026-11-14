namespace Platform.Api.ViewModels;

public sealed record PlanSuggestedSlotViewModel(
    Guid SessionId,
    int SequenceNumber,
    DateOnly TargetDate,
    DateOnly EarliestAllowedDate,
    DateOnly LatestAllowedDate,
    IReadOnlyList<DateOnly> SuggestedCandidateDates,
    AppointmentItemViewModel? BookedAppointment);

