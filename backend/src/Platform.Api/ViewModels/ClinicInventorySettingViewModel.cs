namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for clinic-level inventory configuration.
/// </summary>
public sealed record ClinicInventorySettingViewModel(
    Guid Id,
    Guid ClinicId,
    string NegativeStockPolicy,
    int NearExpiryWarningDays,
    DateTimeOffset UpdatedAtUtc);

