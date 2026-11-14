namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to update clinical visit notes, diagnosis summary, patient advice, follow-up date, and portal visibility.
/// </summary>
public sealed class UpdateVisitNotesDto
{
    public string? ChiefComplaint { get; set; }
    public string? HistoryOfPresentIllness { get; set; }
    public string? Examination { get; set; }
    public string? DiagnosisSummary { get; set; }
    public string? Advice { get; set; }
    public string? PrivateDoctorNotes { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public bool IsVisibleToPatient { get; set; } = true;
}

