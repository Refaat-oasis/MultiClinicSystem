namespace Core.Domain;

/// <summary>
/// Lifecycle status of a patient's portal account inside a clinic.
/// </summary>
public enum PatientAccountStatus
{
    Invited = 0,
    PendingEmailConfirmation = 1,
    Active = 2,
    Disabled = 3
}

