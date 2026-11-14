namespace Core.Domain;

/// <summary>
/// Payroll adjustment classifications (strictly no doctor commission).
/// </summary>
public enum PayrollAdjustmentType
{
    Bonus = 0,
    Deduction = 1,
    Advance = 2
}

