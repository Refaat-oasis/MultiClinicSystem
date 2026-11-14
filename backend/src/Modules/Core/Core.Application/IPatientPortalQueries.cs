using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Anti-corruption query interface implemented by the Patients module for the Patient Portal.
/// </summary>
public interface IPatientPortalQueries
{
    Task<Result<PortalPatientSummaryResult>> GetPatientSummaryForPortalAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalMedicalBackgroundResult>> GetPatientMedicalBackgroundForPortalAsync(
        Guid clinicId,
        Guid patientId,
        bool includeMedicalBackground,
        CancellationToken cancellationToken = default);

    Task<Result<PortalContactSnapshotResult>> GetPatientPortalContactAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalContactSnapshotResult>> FindPatientByFileNumberAndDobForPortalAsync(
        Guid clinicId,
        string fileNumber,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default);

    Task<Result<PatientAttachmentDownloadResult>> DownloadSharedAttachmentForPortalAsync(
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}

