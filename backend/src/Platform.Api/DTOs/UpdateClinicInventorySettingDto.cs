using Core.Domain;

namespace Platform.Api.DTOs;

/// <summary>
/// Request DTO for updating clinic inventory policy and expiry alert lead days.
/// </summary>
public sealed class UpdateClinicInventorySettingDto
{
    public NegativeStockPolicy NegativeStockPolicy { get; set; } = NegativeStockPolicy.Block;
    public int NearExpiryWarningDays { get; set; } = 30;
    public Guid? OperationId { get; set; }
}

