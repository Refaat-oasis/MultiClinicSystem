namespace Core.Application;

/// <summary>
/// Allocated number block returned to a clinic device for offline receipt/file numbering.
/// </summary>
public sealed record AllocatedNumberBlockResult(
    Guid Id,
    Guid ClinicId,
    string DeviceId,
    string Kind,
    long FromNumber,
    long ToNumber,
    long NextNumber,
    long RemainingCount,
    string Status,
    DateTimeOffset IssuedAtUtc);

