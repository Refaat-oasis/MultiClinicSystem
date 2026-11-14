namespace Core.Domain;

/// <summary>
/// Output statistics computed by <see cref="TimeEngineCalculator"/>.
/// </summary>
public sealed record TimeEngineCalculationResult(
    int SampleCount,
    int ExcusedCount,
    decimal TrimmedAverageMinutes,
    int SuggestedDurationMinutes,
    int AppliedDurationMinutes,
    bool IsAdopted);

