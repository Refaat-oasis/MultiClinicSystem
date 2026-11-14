namespace Core.Application;

/// <summary>
/// Read projection for per-clinic inventory settings.
/// </summary>
public sealed record ClinicInventorySettingResult(
    Guid Id,
    Guid ClinicId,
    string NegativeStockPolicy,
    int NearExpiryWarningDays,
    DateTimeOffset UpdatedAtUtc);

