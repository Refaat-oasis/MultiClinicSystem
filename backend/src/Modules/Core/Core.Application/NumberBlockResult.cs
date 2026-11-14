namespace Core.Application;

/// <summary>
/// Read model for a reserved Receipt or PatientFile number block.
/// </summary>
public sealed record NumberBlockResult(
    Guid Id,
    Guid ClinicId,
    string DeviceId,
    string Kind,
    long FromNumber,
    long ToNumber,
    long NextNumber,
    string Status,
    DateTimeOffset CreatedAtUtc);

