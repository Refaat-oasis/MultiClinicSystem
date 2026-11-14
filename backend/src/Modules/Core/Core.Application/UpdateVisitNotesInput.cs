namespace Core.Application;

/// <summary>
/// Input model to update clinical SOAP notes and portal visibility during an active visit.
/// </summary>
public sealed record UpdateVisitNotesInput(
    string? ChiefComplaint,
    string? HistoryOfPresentIllness,
    string? Examination,
    string? DiagnosisSummary,
    string? Advice,
    string? PrivateDoctorNotes,
    DateOnly? FollowUpDate,
    bool IsVisibleToPatient);

