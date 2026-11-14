namespace Platform.Api.ViewModels;

/// <summary>
/// Read-only Super Admin ViewModel for inspecting a clinic's scheduling and queue state.
/// </summary>
public sealed record ClinicScheduleInspectViewModel(
    Guid ClinicId,
    string Date,
    IReadOnlyList<AppointmentCategoryViewModel> Categories,
    IReadOnlyList<AppointmentItemViewModel> Appointments,
    IReadOnlyList<QueueEntryItemViewModel> QueueEntries,
    IReadOnlyList<WaitingListItemViewModel> WaitingList);

