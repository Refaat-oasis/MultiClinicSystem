using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Anti-corruption query interface returning patient-safe clinical visit summaries and prescription PDFs for the patient portal.
/// </summary>
public interface IClinicalPortalQueries
{
    Task<Result<IReadOnlyList<PortalVisitSummaryResult>>> ListPatientVisibleVisitsAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<Result<PrescriptionPdfResult>> GetPatientPrescriptionPdfAsync(
        Guid clinicId,
        Guid patientId,
        Guid prescriptionId,
        CancellationToken cancellationToken = default);
}

