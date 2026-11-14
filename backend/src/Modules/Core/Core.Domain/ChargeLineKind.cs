namespace Core.Domain;

/// <summary>
/// Line item classification within a clinical or manual charge.
/// </summary>
public enum ChargeLineKind
{
    Session = 0,
    Tool = 1,
    Service = 2,
    LabWork = 3,
    Extra = 4
}

