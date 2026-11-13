using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Manages clinic patient registry, Arabic search, duplicate detection, medical history, notes, attachments, and Super Admin inspection.
/// </summary>
public interface IPatientService
{
    Task<Result<ClinicPatientScopingResult>> GetPatientScopingAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPatientScopingResult>> ConfigurePatientScopingAsync(
        Guid userId,
        Guid clinicId,
        bool scopeByDoctor,
        bool scopeBySpecialty,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientSearchResult>> SearchPatientsAsync(
        Guid userId,
        Guid clinicId,
        string? query,
        int limit,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PatientDuplicateWarningResult>>> CheckDuplicatesAsync(
        Guid userId,
        Guid clinicId,
        string fullName,
        string phone,
        Guid? excludePatientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientDetailResult>> CreatePatientAsync(
        Guid userId,
        Guid clinicId,
        CreatePatientInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientDetailResult>> GetPatientAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientDetailResult>> UpdatePatientAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        CreatePatientInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidPatientAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientAllergyItemResult>> AddAllergyAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string allergen,
        string severity,
        string? reaction,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidAllergyAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid allergyId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientDetailResult>> UpdateMedicalHistoryAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        UpdatePatientMedicalHistoryInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientNoteItemResult>> AddNoteAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string noteText,
        bool isPrivate,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidNoteAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid noteId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientAttachmentItemResult>> UploadAttachmentAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string category,
        string fileName,
        string contentType,
        byte[] content,
        string? notes,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientAttachmentDownloadResult>> DownloadAttachmentAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        bool thumbnail,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidAttachmentAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientDetailResult>> InspectPatientAsSuperAdminAsync(
        string adminId,
        Guid clinicId,
        Guid patientId,
        string reason,
        CancellationToken cancellationToken = default);
}

