using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Application service managing clinical visits (server-side timer, overtime), vitals, ICD-10 diagnoses,
/// procedures, allergy-checked prescriptions, printable PDFs, lab orders, JSON-Schema forms, and dental charts.
/// </summary>
public interface IClinicalService
{
    IReadOnlyList<Icd10CatalogEntryResult> SearchIcd10Catalog(string? query);

    Task<Result<VisitSessionResult>> StartOrResumeVisitAsync(
        Guid userId,
        Guid clinicId,
        StartVisitInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitSessionResult>> GetVisitAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<VisitSessionResult>>> ListPatientVisitsAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitSessionResult>> UpdateVisitNotesAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        UpdateVisitNotesInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitVitalResult>> RecordVitalsAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        RecordVisitVitalsInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitDiagnosisResult>> AddDiagnosisAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        AddVisitDiagnosisInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitProcedureResult>> AddProcedureAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        AddVisitProcedureInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PrescriptionResult>> CreatePrescriptionAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        CreatePrescriptionInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PrescriptionPdfResult>> GetPrescriptionPdfAsync(
        Guid userId,
        Guid clinicId,
        Guid prescriptionId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<LabOrderResult>> CreateLabOrderAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        CreateLabOrderInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<LabOrderResult>> RecordLabResultAsync(
        Guid userId,
        Guid clinicId,
        Guid labOrderId,
        string resultSummary,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicalFormResult>>> ListSpecialtyFormsAsync(
        Guid userId,
        Guid clinicId,
        string? specialtyCode,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicalFormResult>> CreateSpecialtyFormAsync(
        Guid userId,
        Guid clinicId,
        string specialtyCode,
        string titleAr,
        string titleEn,
        string jsonSchema,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitFormDataResult>> SaveVisitFormDataAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        SaveVisitFormDataInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ToothRecordResult>> RecordToothStateAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        RecordToothStateInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DerivedToothStateResult>>> GetPatientDentalChartAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitSessionResult>> CompleteVisitAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        CompleteVisitInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicClinicalInspectResult>> InspectClinicClinicalAsync(
        Guid adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<VisitSessionResult>> SetVisitPortalVisibilityAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        bool isVisibleToPatient,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);
}

