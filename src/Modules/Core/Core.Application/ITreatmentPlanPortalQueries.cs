using BuildingBlocks.Domain;

namespace Core.Application;

public interface ITreatmentPlanPortalQueries
{
    Task<Result<IReadOnlyList<PortalTreatmentPlanItemResult>>> GetPatientTreatmentPlansForPortalAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default);
}

