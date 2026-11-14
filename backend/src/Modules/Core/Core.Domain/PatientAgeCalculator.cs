namespace Core.Domain;

/// <summary>
/// Computes a patient's display age from date of birth or stored approximate age.
/// </summary>
public static class PatientAgeCalculator
{
    public static int? CalculateAgeYears(DateOnly? dateOfBirth, int? approximateAgeYears, DateOnly today)
    {
        if (dateOfBirth.HasValue)
        {
            int age = today.Year - dateOfBirth.Value.Year;
            if (dateOfBirth.Value > today.AddYears(-age))
            {
                age--;
            }

            return Math.Max(0, age);
        }

        return approximateAgeYears.HasValue ? Math.Max(0, approximateAgeYears.Value) : null;
    }
}

