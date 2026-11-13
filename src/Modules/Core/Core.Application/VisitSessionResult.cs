namespace Core.Application;

/// <summary>
/// Full read model for a clinical visit session including vitals, diagnoses, procedures, prescriptions, lab orders, forms, and dental chart state.
/// </summary>
public sealed record VisitSessionResult(
    Guid Id,
    Guid ClinicId,
    Guid? AppointmentId,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    string SpecialtyCode,
    Guid? RoomId,
    string? ChiefComplaint,
    string? HistoryOfPresentIllness,
    string? Examination,
    string? DiagnosisSummary,
    string? Advice,
    string? PrivateDoctorNotes,
    DateOnly? FollowUpDate,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int PlannedDurationMinutes,
    int ElapsedMinutes,
    int OvertimeMinutes,
    bool IsOvertimeExcused,
    string? OvertimeExcuseReason,
    string Status,
    bool IsVisibleToPatient,
    bool SupportsDentalChart,
    IReadOnlyList<PatientAllergyItemResult> PatientAllergies,
    IReadOnlyList<VisitVitalResult> Vitals,
    IReadOnlyList<VisitDiagnosisResult> Diagnoses,
    IReadOnlyList<VisitProcedureResult> Procedures,
    IReadOnlyList<PrescriptionResult> Prescriptions,
    IReadOnlyList<LabOrderResult> LabOrders,
    IReadOnlyList<VisitFormDataResult> FormSubmissions,
    IReadOnlyList<DerivedToothStateResult> DentalChart);

