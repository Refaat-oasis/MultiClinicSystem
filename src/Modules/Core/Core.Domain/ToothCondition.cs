namespace Core.Domain;

/// <summary>
/// Clinical dental chart conditions tracked per tooth across visits.
/// </summary>
public enum ToothCondition
{
    Healthy = 0,
    Caries = 1,
    Filled = 2,
    Crown = 3,
    Missing = 4,
    Implant = 5,
    RootCanal = 6,
    Extracted = 7,
    Bridge = 8
}

