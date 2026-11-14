namespace Core.Domain;

/// <summary>
/// Priority tier for an appointment or waiting queue entry.
/// </summary>
public enum AppointmentPriority
{
    Normal = 0,
    Vip = 1,
    Rush = 2
}

