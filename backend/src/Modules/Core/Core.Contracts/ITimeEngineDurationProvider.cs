namespace Core.Contracts;

/// <summary>
/// Cross-module contract allowing Scheduling and Availability queries to read learned doctor/category
/// applied session durations from the optional time-engine module.
/// </summary>
public interface ITimeEngineDurationProvider
{
    /// <summary>
    /// Returns the learned applied session duration in minutes for a doctor and category when the
    /// clinic's time-engine module is enabled, the doctor has the engine turned on, and enough samples exist;
    /// otherwise returns null so Scheduling falls back to the category default duration.
    /// </summary>
    Task<int?> GetAppliedDurationMinutesAsync(
        Guid clinicId,
        Guid doctorId,
        Guid categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns learned applied durations keyed by doctorId for a clinic and category.
    /// Empty dictionary when time-engine is disabled or no doctors have adopted durations.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAppliedDurationsByDoctorAsync(
        Guid clinicId,
        Guid categoryId,
        CancellationToken cancellationToken = default);
}

