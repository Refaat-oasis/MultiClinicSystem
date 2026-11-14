namespace Core.Application;

/// <summary>
/// Read-only Super Admin inspection snapshot of a clinic's scheduling and queue state.
/// </summary>
public sealed record ClinicScheduleInspectResult(
    Guid ClinicId,
    string Date,
    IReadOnlyList<AppointmentCategoryResult> Categories,
    IReadOnlyList<AppointmentItemResult> Appointments,
    IReadOnlyList<QueueEntryItemResult> QueueEntries,
    IReadOnlyList<WaitingListItemResult> WaitingList);

