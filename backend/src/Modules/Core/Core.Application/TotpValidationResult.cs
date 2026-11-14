namespace Core.Application;

/// <summary>
/// Result of validating an hourly (3600-second period) TOTP code.
/// </summary>
public sealed record TotpValidationResult(bool IsValid, long? AcceptedTimeStep, string? FailureCode);

