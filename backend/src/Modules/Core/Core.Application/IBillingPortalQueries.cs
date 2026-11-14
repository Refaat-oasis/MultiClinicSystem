using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Read-only Contracts interface used by the future Patient Portal (Phase 10+) to expose patient-facing charges and receipts
/// without exposing internal tool costs, markup, or lab costs.
/// </summary>
public interface IBillingPortalQueries
{
    Task<Result<PortalBillingSummaryResult>> GetPatientBillingSummaryAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default);
}

