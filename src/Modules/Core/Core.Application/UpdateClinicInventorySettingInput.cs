using Core.Domain;

namespace Core.Application;

/// <summary>
/// Input model for updating per-clinic negative stock policy and near-expiry warning days.
/// </summary>
public sealed record UpdateClinicInventorySettingInput(
    NegativeStockPolicy NegativeStockPolicy,
    int NearExpiryWarningDays,
    Guid? OperationId);

