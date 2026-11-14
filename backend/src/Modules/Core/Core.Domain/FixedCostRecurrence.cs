namespace Core.Domain;

/// <summary>
/// Recurrence schedule for clinic fixed operating costs.
/// </summary>
public enum FixedCostRecurrence
{
    Monthly = 0,
    Weekly = 1,
    Daily = 2,
    Annual = 3,
    OneTime = 4
}

