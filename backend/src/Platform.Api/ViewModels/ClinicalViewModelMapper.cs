using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Maps clinical application read models into API ViewModels.
/// </summary>
public static class ClinicalViewModelMapper
{
    public static Icd10CatalogEntryViewModel ToViewModel(Icd10CatalogEntryResult x) => new(
        x.Code, x.TitleAr, x.TitleEn, x.SpecialtyCategory);

    public static VisitVitalViewModel ToViewModel(VisitVitalResult v) => new(
        v.Id, v.VisitId, v.BloodPressureSystolic, v.BloodPressureDiastolic, v.PulseBpm,
        v.TemperatureCelsius, v.WeightKg, v.HeightCm, v.Bmi, v.SpO2Percent, v.GlucoseMgDl, v.RecordedAtUtc);

    public static VisitDiagnosisViewModel ToViewModel(VisitDiagnosisResult d) => new(
        d.Id, d.VisitId, d.Icd10Code, d.TitleAr, d.TitleEn, d.IsPrimary, d.Notes, d.CreatedAtUtc);

    public static VisitProcedureViewModel ToViewModel(VisitProcedureResult p) => new(
        p.Id, p.VisitId, p.ProcedureCode, p.NameAr, p.NameEn, p.ToothOrSite, p.Notes, p.CreatedAtUtc);

    public static PrescriptionItemViewModel ToViewModel(PrescriptionItemResult i) => new(
        i.Id, i.DrugName, i.ActiveIngredient, i.Dosage, i.Frequency, i.Duration, i.Instructions, i.TriggeredAllergyWarning);

    public static PrescriptionViewModel ToViewModel(PrescriptionResult p) => new(
        p.Id, p.VisitId, p.PatientId, p.DoctorId, p.Notes, p.HasAllergyWarning, p.AllergyWarningDetails,
        p.Items.Select(ToViewModel).ToList(), p.CreatedAtUtc);

    public static LabOrderViewModel ToViewModel(LabOrderResult l) => new(
        l.Id, l.VisitId, l.PatientId, l.DoctorId, l.LabName, l.TestOrWorkName, l.ToothOrSite, l.Instructions,
        l.Status, l.ResultSummary, l.ResultRecordedAtUtc, l.CreatedAtUtc);

    public static ClinicalFormViewModel ToViewModel(ClinicalFormResult f) => new(
        f.Id, f.SpecialtyCode, f.TitleAr, f.TitleEn, f.JsonSchema, f.Version, f.IsActive, f.CreatedAtUtc);

    public static VisitFormDataViewModel ToViewModel(VisitFormDataResult d) => new(
        d.Id, d.VisitId, d.ClinicalFormId, d.SpecialtyCode, d.FormVersion, d.AnswersJson, d.SavedAtUtc);

    public static ToothRecordViewModel ToViewModel(ToothRecordResult r) => new(
        r.Id, r.PatientId, r.VisitId, r.FdiToothNumber, r.UniversalToothCode, r.IsPrimaryTooth,
        r.Condition, r.Surfaces, r.Notes, r.RecordedAtUtc);

    public static DerivedToothStateViewModel ToViewModel(DerivedToothStateResult d) => new(
        d.FdiToothNumber, d.UniversalToothCode, d.IsPrimaryTooth, d.Condition, d.Surfaces, d.Notes, d.LastVisitId, d.RecordedAtUtc);

    public static VisitSessionViewModel ToViewModel(VisitSessionResult v) => new(
        v.Id, v.ClinicId, v.AppointmentId, v.PatientId, v.PatientName, v.DoctorId, v.DoctorName,
        v.SpecialtyCode, v.RoomId, v.ChiefComplaint, v.HistoryOfPresentIllness, v.Examination,
        v.DiagnosisSummary, v.Advice, v.PrivateDoctorNotes, v.FollowUpDate,
        v.StartedAtUtc, v.EndedAtUtc, v.PlannedDurationMinutes, v.ElapsedMinutes, v.OvertimeMinutes,
        v.IsOvertimeExcused, v.OvertimeExcuseReason, v.Status, v.IsVisibleToPatient, v.SupportsDentalChart,
        v.PatientAllergies.Select(a => new PatientAllergyItemViewModel(
            a.Id, a.PatientId, a.Allergen, a.Severity, a.Reaction, a.RecordedByUserId, a.CreatedAtUtc)).ToList(),
        v.Vitals.Select(ToViewModel).ToList(),
        v.Diagnoses.Select(ToViewModel).ToList(),
        v.Procedures.Select(ToViewModel).ToList(),
        v.Prescriptions.Select(ToViewModel).ToList(),
        v.LabOrders.Select(ToViewModel).ToList(),
        v.FormSubmissions.Select(ToViewModel).ToList(),
        v.DentalChart.Select(ToViewModel).ToList());

    public static PortalVisitSummaryViewModel ToViewModel(PortalVisitSummaryResult p) => new(
        p.VisitId, p.StartedAtUtc, p.EndedAtUtc, p.DoctorName, p.SpecialtyCode, p.DiagnosisSummary,
        p.Advice, p.FollowUpDate,
        p.Diagnoses.Select(ToViewModel).ToList(),
        p.Procedures.Select(ToViewModel).ToList(),
        p.Prescriptions.Select(ToViewModel).ToList());

    public static ClinicClinicalInspectViewModel ToViewModel(ClinicClinicalInspectResult r) => new(
        r.ClinicId,
        r.Visits.Select(ToViewModel).ToList(),
        r.Forms.Select(ToViewModel).ToList());
}

