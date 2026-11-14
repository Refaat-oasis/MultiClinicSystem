namespace Core.Domain;

/// <summary>
/// One completed clinical session duration sample passed to <see cref="TimeEngineCalculator"/>.
/// </summary>
public sealed record TimeEngineSample(int RawMinutes, bool IsExcused);

