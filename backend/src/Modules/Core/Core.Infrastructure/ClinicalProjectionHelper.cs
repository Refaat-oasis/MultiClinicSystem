using System.Text.Json;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Helper projecting Visit aggregates, patient allergies, forms, and derived dental chart state into Application read models.
/// </summary>
internal static class ClinicalProjectionHelper
{
    internal static async Task<VisitSessionResult> BuildVisitSessionAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        Visit visit,
        bool supportsDentalChart,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Patient? patient = await dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == visit.PatientId, cancellationToken);

        ClinicDoctor? doctor = await dbContext.ClinicDoctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.Id == visit.DoctorId, cancellationToken);

        List<PatientAllergyItemResult> allergies = await dbContext.PatientAllergies
            .AsNoTracking()
            .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == visit.PatientId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new PatientAllergyItemResult(
                a.Id, a.PatientId, a.Allergen, a.Severity, a.Reaction, a.RecordedByUserId, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        List<VisitVitalResult> vitals = await dbContext.VisitVitals
            .AsNoTracking()
            .Where(v => v.ClinicId == clinicId && !v.IsVoided && v.VisitId == visit.Id)
            .OrderByDescending(v => v.RecordedAtUtc)
            .Select(v => new VisitVitalResult(
                v.Id, v.VisitId, v.BloodPressureSystolic, v.BloodPressureDiastolic,
                v.PulseBpm, v.TemperatureCelsius, v.WeightKg, v.HeightCm,
                v.Bmi, v.SpO2Percent, v.GlucoseMgDl, v.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        List<VisitDiagnosisResult> diagnoses = await dbContext.VisitDiagnoses
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && !d.IsVoided && d.VisitId == visit.Id)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.CreatedAtUtc)
            .Select(d => new VisitDiagnosisResult(
                d.Id, d.VisitId, d.Icd10Code, d.TitleAr, d.TitleEn, d.IsPrimary, d.Notes, d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        List<VisitProcedureResult> procedures = await dbContext.VisitProcedures
            .AsNoTracking()
            .Where(p => p.ClinicId == clinicId && !p.IsVoided && p.VisitId == visit.Id)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => new VisitProcedureResult(
                p.Id, p.VisitId, p.ProcedureCode, p.NameAr, p.NameEn, p.ToothOrSite, p.Notes, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        List<Prescription> rxHeaders = await dbContext.Prescriptions
            .AsNoTracking()
            .Where(r => r.ClinicId == clinicId && !r.IsVoided && r.VisitId == visit.Id)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        List<Guid> rxIds = rxHeaders.Select(r => r.Id).ToList();
        List<PrescriptionItem> rxItems = rxIds.Count == 0
            ? []
            : await dbContext.PrescriptionItems
                .AsNoTracking()
                .Where(i => i.ClinicId == clinicId && !i.IsVoided && rxIds.Contains(i.PrescriptionId))
                .ToListAsync(cancellationToken);

        var prescriptions = rxHeaders.Select(r => new PrescriptionResult(
            r.Id,
            r.VisitId,
            r.PatientId,
            r.DoctorId,
            r.Notes,
            r.HasAllergyWarning,
            r.AllergyWarningDetails,
            rxItems.Where(i => i.PrescriptionId == r.Id)
                .Select(i => new PrescriptionItemResult(
                    i.Id, i.DrugName, i.ActiveIngredient, i.Dosage, i.Frequency, i.Duration, i.Instructions, i.TriggeredAllergyWarning))
                .ToList(),
            r.CreatedAtUtc)).ToList();

        List<LabOrderResult> labOrders = await dbContext.LabOrders
            .AsNoTracking()
            .Where(l => l.ClinicId == clinicId && !l.IsVoided && l.VisitId == visit.Id)
            .OrderBy(l => l.CreatedAtUtc)
            .Select(l => new LabOrderResult(
                l.Id, l.VisitId, l.PatientId, l.DoctorId, l.LabName, l.TestOrWorkName,
                l.ToothOrSite, l.Instructions, l.Status, l.ResultSummary, l.ResultRecordedAtUtc, l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        List<VisitFormDataResult> formSubmissions = await dbContext.VisitFormData
            .AsNoTracking()
            .Where(f => f.ClinicId == clinicId && !f.IsVoided && f.VisitId == visit.Id)
            .OrderBy(f => f.SavedAtUtc)
            .Select(f => new VisitFormDataResult(
                f.Id, f.VisitId, f.ClinicalFormId, f.SpecialtyCode, f.FormVersion, f.AnswersJson, f.SavedAtUtc))
            .ToListAsync(cancellationToken);

        List<DerivedToothStateResult> chart = [];
        if (supportsDentalChart)
        {
            List<ToothRecord> toothRecords = await dbContext.ToothRecords
                .AsNoTracking()
                .Where(t => t.ClinicId == clinicId && !t.IsVoided && t.PatientId == visit.PatientId)
                .ToListAsync(cancellationToken);

            chart = DentalNotationConverter.DeriveCurrentChartState(toothRecords)
                .Select(s => new DerivedToothStateResult(
                    s.FdiToothNumber,
                    s.UniversalToothCode,
                    s.IsPrimaryTooth,
                    s.Condition.ToString(),
                    s.Surfaces,
                    s.Notes,
                    s.LastVisitId,
                    s.RecordedAtUtc))
                .ToList();
        }

        return new VisitSessionResult(
            visit.Id,
            visit.ClinicId,
            visit.AppointmentId,
            visit.PatientId,
            patient?.FullName ?? string.Empty,
            visit.DoctorId,
            doctor?.FullName ?? string.Empty,
            visit.SpecialtyCode,
            visit.RoomId,
            visit.ChiefComplaint,
            visit.HistoryOfPresentIllness,
            visit.Examination,
            visit.DiagnosisSummary,
            visit.Advice,
            visit.PrivateDoctorNotes,
            visit.FollowUpDate,
            visit.StartedAtUtc,
            visit.EndedAtUtc,
            visit.PlannedDurationMinutes,
            visit.ComputeElapsedMinutes(nowUtc),
            visit.Status == VisitStatus.Completed ? visit.OvertimeMinutes : visit.ComputeOvertimeMinutes(nowUtc),
            visit.IsOvertimeExcused,
            visit.OvertimeExcuseReason,
            visit.Status.ToString(),
            visit.IsVisibleToPatient,
            supportsDentalChart,
            allergies,
            vitals,
            diagnoses,
            procedures,
            prescriptions,
            labOrders,
            formSubmissions,
            chart);
    }

    internal static List<string> DeserializeSurfaces(string? surfacesJson)
    {
        if (string.IsNullOrWhiteSpace(surfacesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(surfacesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
