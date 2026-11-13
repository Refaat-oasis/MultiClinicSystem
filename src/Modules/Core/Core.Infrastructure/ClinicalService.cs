using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements clinical visit workflows, server-side session timing, allergy-checked prescriptions,
/// printable PDFs, specialty JSON-Schema forms, dental charting, and portal-safe clinical queries.
/// </summary>
public sealed class ClinicalService : IClinicalService, IClinicalPortalQueries
{
    private static readonly Icd10CatalogEntryResult[] Icd10Catalog =
    [
        new("K02.9", "تسوس الأسنان غير محدد", "Dental caries, unspecified", "dental"),
        new("K04.0", "التهاب لب السن", "Pulpitis", "dental"),
        new("K05.3", "التهاب دواعم السن المزمن", "Chronic periodontitis", "dental"),
        new("H52.1", "قصر النظر", "Myopia", "ophthalmology"),
        new("H40.1", "الجلوكوما مفتوحة الزاوية", "Open-angle glaucoma", "ophthalmology"),
        new("H25.9", "إعتام عدسة العين", "Age-related cataract, unspecified", "ophthalmology"),
        new("I10", "ارتفاع ضغط الدم الأساسي", "Essential (primary) hypertension", "general"),
        new("E11.9", "السكري من النوع الثاني", "Type 2 diabetes mellitus without complications", "general"),
        new("J06.9", "عدوى الجهاز التنفسي العلوي الحادة", "Acute upper respiratory infection, unspecified", "general")
    ];

    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;
    private readonly IBillingService? _billingService;
    private readonly Dictionary<string, ISpecialtyPlugin> _pluginsByCode;

    public ClinicalService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IAuditService auditService,
        IClock clock,
        IEnumerable<ISpecialtyPlugin> specialtyPlugins,
        IBillingService? billingService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _billingService = billingService;
        _pluginsByCode = (specialtyPlugins ?? Array.Empty<ISpecialtyPlugin>())
            .ToDictionary(p => p.SpecialtyCode, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Icd10CatalogEntryResult> SearchIcd10Catalog(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Icd10Catalog;
        }

        string q = query.Trim();
        return Icd10Catalog
            .Where(e => e.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.TitleEn.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || e.TitleAr.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<Result<VisitSessionResult>> StartOrResumeVisitAsync(
        Guid userId,
        Guid clinicId,
        StartVisitInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitSessionResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                Guid? processedVisitId = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .Where(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value && o.EntityName == "VisitStart")
                    .Select(o => (Guid?)o.ResultEntityId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (processedVisitId.HasValue)
                {
                    Visit? opVisit = await _dbContext.Visits
                        .FirstOrDefaultAsync(v => v.ClinicId == clinicId && !v.IsVoided && v.Id == processedVisitId.Value, cancellationToken);
                    if (opVisit != null)
                    {
                        return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, opVisit, cancellationToken));
                    }
                }
            }

            if (input.AppointmentId.HasValue && input.AppointmentId.Value != Guid.Empty)
            {
                Visit? existingVisit = await _dbContext.Visits
                    .FirstOrDefaultAsync(
                        v => v.ClinicId == clinicId && !v.IsVoided && v.AppointmentId == input.AppointmentId.Value && v.Status == VisitStatus.InProgress,
                        cancellationToken);

                if (existingVisit != null)
                {
                    if (scopedDoctorId.HasValue && existingVisit.DoctorId != scopedDoctorId.Value)
                    {
                        return Result<VisitSessionResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
                    }

                    return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, existingVisit, cancellationToken));
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            Guid patientId;
            Guid doctorId;
            Guid? roomId = input.RoomId;
            string specialtyCode = string.IsNullOrWhiteSpace(input.SpecialtyCode) ? "general" : input.SpecialtyCode.Trim().ToLowerInvariant();
            int plannedMinutes = input.PlannedDurationMinutes ?? 20;

            if (input.AppointmentId.HasValue && input.AppointmentId.Value != Guid.Empty)
            {
                Appointment? appointment = await _dbContext.Appointments
                    .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.Id == input.AppointmentId.Value, cancellationToken);
                if (appointment == null)
                {
                    return Result<VisitSessionResult>.Failure(Error.NotFound("APPOINTMENT_NOT_FOUND", "Appointment was not found."));
                }

                if (scopedDoctorId.HasValue && appointment.DoctorId != scopedDoctorId.Value)
                {
                    return Result<VisitSessionResult>.Failure(Error.NotFound("APPOINTMENT_NOT_FOUND", "Appointment was not found."));
                }

                patientId = appointment.PatientId;
                doctorId = appointment.DoctorId;
                roomId ??= appointment.RoomId;
                plannedMinutes = Math.Max(5, (int)Math.Round((appointment.EndUtc - appointment.StartUtc).TotalMinutes));

                AppointmentCategory? cat = await _dbContext.AppointmentCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.Id == appointment.CategoryId, cancellationToken);
                if (cat != null && string.IsNullOrWhiteSpace(input.SpecialtyCode))
                {
                    specialtyCode = cat.SpecialtyCode;
                }

                if (appointment.Status is AppointmentStatus.Booked or AppointmentStatus.Arrived or AppointmentStatus.Late)
                {
                    appointment.TryTransitionTo(AppointmentStatus.InSession, now);
                }
            }
            else
            {
                if (!input.PatientId.HasValue || input.PatientId.Value == Guid.Empty)
                {
                    return Result<VisitSessionResult>.Failure(Error.Validation("PATIENT_REQUIRED", "PatientId or AppointmentId is required."));
                }

                patientId = input.PatientId.Value;
                doctorId = scopedDoctorId ?? input.DoctorId ?? Guid.Empty;
                if (doctorId == Guid.Empty)
                {
                    return Result<VisitSessionResult>.Failure(Error.Validation("DOCTOR_REQUIRED", "DoctorId is required for a walk-in visit."));
                }
            }

            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == patientId, cancellationToken);
            if (!patientExists)
            {
                return Result<VisitSessionResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            ClinicDoctor? doc = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.Id == doctorId, cancellationToken);
            if (doc == null)
            {
                return Result<VisitSessionResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor was not found."));
            }

            Guid visitId = input.ClientVisitId.HasValue && input.ClientVisitId.Value != Guid.Empty
                ? input.ClientVisitId.Value
                : GuidV7.NewGuid();

            var visit = new Visit(
                visitId,
                clinicId,
                input.AppointmentId,
                patientId,
                doctorId,
                specialtyCode,
                roomId,
                plannedMinutes,
                now,
                input.IsVisibleToPatient,
                input.OperationId,
                now);

            _dbContext.Visits.Add(visit);

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "VisitStart", visit.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "VisitStarted",
                "Visit",
                visit.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { visit.Id, visit.AppointmentId, visit.PatientId, visit.DoctorId, visit.SpecialtyCode }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, visit, cancellationToken));
        });
    }

    public async Task<Result<VisitSessionResult>> GetVisitAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.read", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitSessionResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitSessionResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, visit, cancellationToken));
        });
    }

    public async Task<Result<IReadOnlyList<VisitSessionResult>>> ListPatientVisitsAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.read", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<VisitSessionResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);
            IQueryable<Visit> query = _dbContext.Visits
                .AsNoTracking()
                .Where(v => v.ClinicId == clinicId && !v.IsVoided && v.PatientId == patientId);

            if (scopedDoctorId.HasValue)
            {
                query = query.Where(v => v.DoctorId == scopedDoctorId.Value);
            }

            List<Visit> rows = await query.OrderByDescending(v => v.StartedAtUtc).Take(50).ToListAsync(cancellationToken);
            var list = new List<VisitSessionResult>(rows.Count);
            foreach (Visit row in rows)
            {
                list.Add(await ProjectVisitAsync(clinicId, row, cancellationToken));
            }

            return Result<IReadOnlyList<VisitSessionResult>>.Success(list);
        });
    }

    public async Task<Result<VisitSessionResult>> UpdateVisitNotesAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        UpdateVisitNotesInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitSessionResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitSessionResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            visit.UpdateClinicalNotes(
                input.ChiefComplaint,
                input.HistoryOfPresentIllness,
                input.Examination,
                input.DiagnosisSummary,
                input.Advice,
                input.PrivateDoctorNotes,
                input.FollowUpDate,
                input.IsVisibleToPatient,
                _clock.UtcNow);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, visit, cancellationToken));
        });
    }

    public async Task<Result<VisitVitalResult>> RecordVitalsAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        RecordVisitVitalsInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitVitalResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitVitalResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            var vital = new VisitVital(
                GuidV7.NewGuid(),
                clinicId,
                visit.Id,
                input.BloodPressureSystolic,
                input.BloodPressureDiastolic,
                input.PulseBpm,
                input.TemperatureCelsius,
                input.WeightKg,
                input.HeightCm,
                input.SpO2Percent,
                input.GlucoseMgDl,
                _clock.UtcNow);

            _dbContext.VisitVitals.Add(vital);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<VisitVitalResult>.Success(new VisitVitalResult(
                vital.Id, vital.VisitId, vital.BloodPressureSystolic, vital.BloodPressureDiastolic,
                vital.PulseBpm, vital.TemperatureCelsius, vital.WeightKg, vital.HeightCm,
                vital.Bmi, vital.SpO2Percent, vital.GlucoseMgDl, vital.RecordedAtUtc));
        });
    }

    public async Task<Result<VisitDiagnosisResult>> AddDiagnosisAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        AddVisitDiagnosisInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitDiagnosisResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Icd10Code) || string.IsNullOrWhiteSpace(input.TitleEn))
        {
            return Result<VisitDiagnosisResult>.Failure(Error.Validation("DIAGNOSIS_INVALID", "ICD-10 code and title are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitDiagnosisResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            string titleAr = string.IsNullOrWhiteSpace(input.TitleAr) ? input.TitleEn : input.TitleAr;
            var diagnosis = new VisitDiagnosis(
                GuidV7.NewGuid(), clinicId, visit.Id, input.Icd10Code, titleAr, input.TitleEn, input.IsPrimary, input.Notes, _clock.UtcNow);
            _dbContext.VisitDiagnoses.Add(diagnosis);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<VisitDiagnosisResult>.Success(new VisitDiagnosisResult(
                diagnosis.Id, diagnosis.VisitId, diagnosis.Icd10Code, diagnosis.TitleAr, diagnosis.TitleEn,
                diagnosis.IsPrimary, diagnosis.Notes, diagnosis.CreatedAtUtc));
        });
    }

    public async Task<Result<VisitProcedureResult>> AddProcedureAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        AddVisitProcedureInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitProcedureResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.ProcedureCode) || string.IsNullOrWhiteSpace(input.NameEn))
        {
            return Result<VisitProcedureResult>.Failure(Error.Validation("PROCEDURE_INVALID", "ProcedureCode and NameEn are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitProcedureResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            string nameAr = string.IsNullOrWhiteSpace(input.NameAr) ? input.NameEn : input.NameAr;
            var proc = new VisitProcedure(
                GuidV7.NewGuid(), clinicId, visit.Id, input.ProcedureCode, nameAr, input.NameEn, input.ToothOrSite, input.Notes, _clock.UtcNow);
            _dbContext.VisitProcedures.Add(proc);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<VisitProcedureResult>.Success(new VisitProcedureResult(
                proc.Id, proc.VisitId, proc.ProcedureCode, proc.NameAr, proc.NameEn, proc.ToothOrSite, proc.Notes, proc.CreatedAtUtc));
        });
    }

    public async Task<Result<PrescriptionResult>> CreatePrescriptionAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        CreatePrescriptionInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PrescriptionResult>.Failure(gate.Error);
        }

        if (input.Items == null || input.Items.Count == 0)
        {
            return Result<PrescriptionResult>.Failure(Error.Validation("PRESCRIPTION_EMPTY", "At least one medication item is required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<PrescriptionResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            List<PatientAllergy> patientAllergies = await _dbContext.PatientAllergies
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == visit.PatientId)
                .ToListAsync(cancellationToken);

            var warnings = new List<string>();
            var itemFlags = new List<bool>(input.Items.Count);
            foreach (PrescriptionItemInput item in input.Items)
            {
                List<PatientAllergy> matched = patientAllergies
                    .Where(a => item.DrugName.Contains(a.Allergen, StringComparison.OrdinalIgnoreCase)
                                || (!string.IsNullOrWhiteSpace(item.ActiveIngredient) && item.ActiveIngredient.Contains(a.Allergen, StringComparison.OrdinalIgnoreCase))
                                || a.Allergen.Contains(item.DrugName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                bool itemMatched = matched.Count > 0;
                itemFlags.Add(itemMatched);
                foreach (PatientAllergy alg in matched)
                {
                    warnings.Add($"{item.DrugName} matches allergy '{alg.Allergen}' ({alg.Severity})");
                }
            }

            bool hasWarning = warnings.Count > 0;
            string? warningDetails = hasWarning ? string.Join("; ", warnings.Distinct()) : null;

            if (hasWarning && !input.AcknowledgeAllergyWarning)
            {
                return Result<PrescriptionResult>.Failure(Error.Conflict(
                    "ALLERGY_WARNING_CONFIRMATION_REQUIRED",
                    $"Allergy conflict detected: {warningDetails}. Pass AcknowledgeAllergyWarning=true to confirm."));
            }

            DateTimeOffset now = _clock.UtcNow;
            var rx = new Prescription(
                GuidV7.NewGuid(), clinicId, visit.Id, visit.PatientId, visit.DoctorId, input.Notes, hasWarning, warningDetails, now);
            _dbContext.Prescriptions.Add(rx);

            var itemEntities = new List<PrescriptionItem>(input.Items.Count);
            for (int i = 0; i < input.Items.Count; i++)
            {
                PrescriptionItemInput item = input.Items[i];
                var entity = new PrescriptionItem(
                    GuidV7.NewGuid(),
                    clinicId,
                    rx.Id,
                    item.DrugName,
                    item.ActiveIngredient,
                    item.Dosage,
                    item.Frequency,
                    item.Duration,
                    item.Instructions,
                    itemFlags[i]);
                itemEntities.Add(entity);
                _dbContext.PrescriptionItems.Add(entity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (hasWarning)
            {
                await _auditService.LogAsync(
                    "PrescriptionAllergyWarningAcknowledged",
                    "Prescription",
                    rx.Id.ToString(),
                    null,
                    warningDetails,
                    userId,
                    clinicId,
                    ipAddress,
                    requestId,
                    cancellationToken);
            }

            return Result<PrescriptionResult>.Success(new PrescriptionResult(
                rx.Id,
                rx.VisitId,
                rx.PatientId,
                rx.DoctorId,
                rx.Notes,
                rx.HasAllergyWarning,
                rx.AllergyWarningDetails,
                itemEntities.Select(i => new PrescriptionItemResult(
                    i.Id, i.DrugName, i.ActiveIngredient, i.Dosage, i.Frequency, i.Duration, i.Instructions, i.TriggeredAllergyWarning)).ToList(),
                rx.CreatedAtUtc));
        });
    }

    public async Task<Result<PrescriptionPdfResult>> GetPrescriptionPdfAsync(
        Guid userId,
        Guid clinicId,
        Guid prescriptionId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.read", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PrescriptionPdfResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Prescription? rx = await _dbContext.Prescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == prescriptionId, cancellationToken);
            if (rx == null)
            {
                return Result<PrescriptionPdfResult>.Failure(Error.NotFound("PRESCRIPTION_NOT_FOUND", "Prescription was not found."));
            }

            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, rx.VisitId, cancellationToken);
            if (visit == null)
            {
                return Result<PrescriptionPdfResult>.Failure(Error.NotFound("PRESCRIPTION_NOT_FOUND", "Prescription was not found."));
            }

            return Result<PrescriptionPdfResult>.Success(await BuildPrescriptionPdfInternalAsync(clinicId, rx, visit, cancellationToken));
        });
    }

    public async Task<Result<LabOrderResult>> CreateLabOrderAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        CreateLabOrderInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<LabOrderResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.LabName) || string.IsNullOrWhiteSpace(input.TestOrWorkName))
        {
            return Result<LabOrderResult>.Failure(Error.Validation("LAB_ORDER_INVALID", "LabName and TestOrWorkName are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<LabOrderResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            var order = new LabOrder(
                GuidV7.NewGuid(), clinicId, visit.Id, visit.PatientId, visit.DoctorId,
                input.LabName, input.TestOrWorkName, input.ToothOrSite, input.Instructions, _clock.UtcNow);
            _dbContext.LabOrders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<LabOrderResult>.Success(new LabOrderResult(
                order.Id, order.VisitId, order.PatientId, order.DoctorId, order.LabName, order.TestOrWorkName,
                order.ToothOrSite, order.Instructions, order.Status, order.ResultSummary, order.ResultRecordedAtUtc, order.CreatedAtUtc));
        });
    }

    public async Task<Result<LabOrderResult>> RecordLabResultAsync(
        Guid userId,
        Guid clinicId,
        Guid labOrderId,
        string resultSummary,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<LabOrderResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            LabOrder? order = await _dbContext.LabOrders
                .FirstOrDefaultAsync(l => l.ClinicId == clinicId && !l.IsVoided && l.Id == labOrderId, cancellationToken);
            if (order == null)
            {
                return Result<LabOrderResult>.Failure(Error.NotFound("LAB_ORDER_NOT_FOUND", "Lab order was not found."));
            }

            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, order.VisitId, cancellationToken);
            if (visit == null)
            {
                return Result<LabOrderResult>.Failure(Error.NotFound("LAB_ORDER_NOT_FOUND", "Lab order was not found."));
            }

            order.RecordResult(resultSummary, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<LabOrderResult>.Success(new LabOrderResult(
                order.Id, order.VisitId, order.PatientId, order.DoctorId, order.LabName, order.TestOrWorkName,
                order.ToothOrSite, order.Instructions, order.Status, order.ResultSummary, order.ResultRecordedAtUtc, order.CreatedAtUtc));
        });
    }

    public async Task<Result<IReadOnlyList<ClinicalFormResult>>> ListSpecialtyFormsAsync(
        Guid userId,
        Guid clinicId,
        string? specialtyCode,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.read", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicalFormResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            string? normCode = string.IsNullOrWhiteSpace(specialtyCode) ? null : specialtyCode.Trim().ToLowerInvariant();
            if (normCode != null && _pluginsByCode.TryGetValue(normCode, out ISpecialtyPlugin? plugin))
            {
                bool hasExisting = await _dbContext.ClinicalForms
                    .AsNoTracking()
                    .AnyAsync(f => f.ClinicId == clinicId && !f.IsVoided && f.SpecialtyCode == normCode, cancellationToken);
                if (!hasExisting)
                {
                    var seeded = new ClinicalForm(
                        GuidV7.NewGuid(), clinicId, plugin.SpecialtyCode, plugin.DefaultFormTitleAr, plugin.DefaultFormTitleEn, plugin.DefaultFormJsonSchema, 1, _clock.UtcNow);
                    _dbContext.ClinicalForms.Add(seeded);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            IQueryable<ClinicalForm> query = _dbContext.ClinicalForms
                .AsNoTracking()
                .Where(f => f.ClinicId == clinicId && !f.IsVoided && f.IsActive);

            if (normCode != null)
            {
                query = query.Where(f => f.SpecialtyCode == normCode);
            }

            List<ClinicalFormResult> forms = await query
                .OrderByDescending(f => f.Version)
                .Select(f => new ClinicalFormResult(f.Id, f.SpecialtyCode, f.TitleAr, f.TitleEn, f.JsonSchema, f.Version, f.IsActive, f.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<ClinicalFormResult>>.Success(forms);
        });
    }

    public async Task<Result<ClinicalFormResult>> CreateSpecialtyFormAsync(
        Guid userId,
        Guid clinicId,
        string specialtyCode,
        string titleAr,
        string titleEn,
        string jsonSchema,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicalFormResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(specialtyCode) || string.IsNullOrWhiteSpace(jsonSchema))
        {
            return Result<ClinicalFormResult>.Failure(Error.Validation("FORM_SCHEMA_INVALID", "SpecialtyCode and JsonSchema are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            string normCode = specialtyCode.Trim().ToLowerInvariant();
            List<ClinicalForm> existing = await _dbContext.ClinicalForms
                .Where(f => f.ClinicId == clinicId && !f.IsVoided && f.SpecialtyCode == normCode && f.IsActive)
                .ToListAsync(cancellationToken);

            int nextVersion = existing.Count == 0 ? 1 : existing.Max(f => f.Version) + 1;
            foreach (ClinicalForm prev in existing)
            {
                prev.Deactivate();
            }

            var created = new ClinicalForm(
                GuidV7.NewGuid(), clinicId, normCode, titleAr, titleEn, jsonSchema, nextVersion, _clock.UtcNow);
            _dbContext.ClinicalForms.Add(created);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ClinicalFormResult>.Success(new ClinicalFormResult(
                created.Id, created.SpecialtyCode, created.TitleAr, created.TitleEn, created.JsonSchema, created.Version, created.IsActive, created.CreatedAtUtc));
        });
    }

    public async Task<Result<VisitFormDataResult>> SaveVisitFormDataAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        SaveVisitFormDataInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitFormDataResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitFormDataResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            ClinicalForm? form = await _dbContext.ClinicalForms
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.ClinicId == clinicId && !f.IsVoided && f.Id == input.ClinicalFormId, cancellationToken);
            if (form == null)
            {
                return Result<VisitFormDataResult>.Failure(Error.NotFound("CLINICAL_FORM_NOT_FOUND", "Clinical form was not found."));
            }

            ClinicalFormValidationResult validation = ClinicalFormSchemaValidator.Validate(form.JsonSchema, input.AnswersJson);
            if (!validation.IsValid)
            {
                return Result<VisitFormDataResult>.Failure(Error.Validation("FORM_VALIDATION_FAILED", string.Join("; ", validation.Errors)));
            }

            DateTimeOffset now = _clock.UtcNow;
            VisitFormData? existingData = await _dbContext.VisitFormData
                .FirstOrDefaultAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.VisitId == visit.Id && d.ClinicalFormId == form.Id, cancellationToken);

            if (existingData == null)
            {
                existingData = new VisitFormData(
                    GuidV7.NewGuid(), clinicId, visit.Id, form.Id, form.SpecialtyCode, form.Version, input.AnswersJson, now);
                _dbContext.VisitFormData.Add(existingData);
            }
            else
            {
                existingData.UpdateAnswers(input.AnswersJson, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<VisitFormDataResult>.Success(new VisitFormDataResult(
                existingData.Id, existingData.VisitId, existingData.ClinicalFormId, existingData.SpecialtyCode, existingData.FormVersion, existingData.AnswersJson, existingData.SavedAtUtc));
        });
    }

    public async Task<Result<ToothRecordResult>> RecordToothStateAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        RecordToothStateInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ToothRecordResult>.Failure(gate.Error);
        }

        DentalNumberingSystem? systemHint = null;
        if (!string.IsNullOrWhiteSpace(input.NumberingSystem) && Enum.TryParse<DentalNumberingSystem>(input.NumberingSystem.Trim(), true, out DentalNumberingSystem parsedSys))
        {
            systemHint = parsedSys;
        }

        if (!DentalNotationConverter.TryResolveTooth(input.ToothCode, systemHint, out string fdi, out string universal, out bool isPrimary))
        {
            return Result<ToothRecordResult>.Failure(Error.Validation("INVALID_TOOTH_CODE", $"Tooth code '{input.ToothCode}' is not a valid FDI or Universal tooth identifier."));
        }

        if (!Enum.TryParse<ToothCondition>(input.Condition, true, out ToothCondition condition))
        {
            return Result<ToothRecordResult>.Failure(Error.Validation("INVALID_TOOTH_CONDITION", $"Tooth condition '{input.Condition}' is invalid."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<ToothRecordResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            List<string> normalizedSurfaces = (input.Surfaces ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var record = new ToothRecord(
                GuidV7.NewGuid(),
                clinicId,
                visit.PatientId,
                visit.Id,
                fdi,
                universal,
                isPrimary,
                condition,
                JsonSerializer.Serialize(normalizedSurfaces),
                input.Notes,
                _clock.UtcNow);

            _dbContext.ToothRecords.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ToothRecordResult>.Success(new ToothRecordResult(
                record.Id, record.PatientId, record.VisitId, record.FdiToothNumber, record.UniversalToothCode,
                record.IsPrimaryTooth, record.Condition.ToString(), normalizedSurfaces, record.Notes, record.RecordedAtUtc));
        });
    }

    public async Task<Result<IReadOnlyList<DerivedToothStateResult>>> GetPatientDentalChartAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.read", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<DerivedToothStateResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == patientId, cancellationToken);
            if (!patientExists)
            {
                return Result<IReadOnlyList<DerivedToothStateResult>>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            List<ToothRecord> records = await _dbContext.ToothRecords
                .AsNoTracking()
                .Where(t => t.ClinicId == clinicId && !t.IsVoided && t.PatientId == patientId)
                .ToListAsync(cancellationToken);

            List<DerivedToothStateResult> derived = DentalNotationConverter.DeriveCurrentChartState(records)
                .Select(s => new DerivedToothStateResult(
                    s.FdiToothNumber, s.UniversalToothCode, s.IsPrimaryTooth, s.Condition.ToString(), s.Surfaces, s.Notes, s.LastVisitId, s.RecordedAtUtc))
                .ToList();

            return Result<IReadOnlyList<DerivedToothStateResult>>.Success(derived);
        });
    }

    public async Task<Result<VisitSessionResult>> CompleteVisitAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        CompleteVisitInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinical.write", "clinical", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<VisitSessionResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Visit? visit = await LoadAuthorizedVisitAsync(userId, clinicId, visitId, cancellationToken);
            if (visit == null)
            {
                return Result<VisitSessionResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
            }

            if (visit.Status == VisitStatus.Completed)
            {
                return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, visit, cancellationToken));
            }

            DateTimeOffset now = _clock.UtcNow;
            visit.Complete(now, input.IsOvertimeExcused, input.OvertimeExcuseReason, now);

            if (visit.AppointmentId.HasValue)
            {
                Appointment? appointment = await _dbContext.Appointments
                    .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.Id == visit.AppointmentId.Value, cancellationToken);
                if (appointment != null && appointment.Status != AppointmentStatus.Completed)
                {
                    appointment.TryTransitionTo(AppointmentStatus.Completed, now);
                }

                QueueEntry? queueEntry = await _dbContext.QueueEntries
                    .FirstOrDefaultAsync(q => q.ClinicId == clinicId && !q.IsVoided && q.AppointmentId == visit.AppointmentId.Value && q.Status != QueueEntryStatus.Served, cancellationToken);
                if (queueEntry != null)
                {
                    queueEntry.MarkServed(now);
                }
            }

            // Guarantee SessionCompleted appears exactly once in the transactional outbox
            string visitIdToken = visit.Id.ToString();
            bool alreadyOutboxed = await _dbContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(m => m.Type == "SessionCompleted" && m.Content.Contains(visitIdToken), cancellationToken);

            if (!alreadyOutboxed)
            {
                string payload = JsonSerializer.Serialize(new
                {
                    VisitId = visit.Id,
                    visit.ClinicId,
                    visit.AppointmentId,
                    visit.PatientId,
                    visit.DoctorId,
                    visit.SpecialtyCode,
                    visit.OvertimeMinutes,
                    CompletedAtUtc = now
                });
                _dbContext.OutboxMessages.Add(new OutboxMessage(GuidV7.NewGuid(), "SessionCompleted", payload, now));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "VisitComplete", visit.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_billingService != null)
            {
                await _billingService.CreateDraftChargeForCompletedVisitAsync(
                    clinicId,
                    visit.Id,
                    visit.PatientId,
                    visit.AppointmentId,
                    visit.DoctorId,
                    userId.ToString(),
                    cancellationToken);
            }

            await _auditService.LogAsync(
                "VisitCompleted",
                "Visit",
                visit.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { visit.OvertimeMinutes, visit.IsOvertimeExcused }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<VisitSessionResult>.Success(await ProjectVisitAsync(clinicId, visit, cancellationToken));
        });
    }

    public async Task<Result<ClinicClinicalInspectResult>> InspectClinicClinicalAsync(
        Guid adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<Visit> visits = await _dbContext.Visits
                .AsNoTracking()
                .Where(v => v.ClinicId == clinicId && !v.IsVoided)
                .OrderByDescending(v => v.StartedAtUtc)
                .Take(25)
                .ToListAsync(cancellationToken);

            var projected = new List<VisitSessionResult>(visits.Count);
            foreach (Visit v in visits)
            {
                projected.Add(await ProjectVisitAsync(clinicId, v, cancellationToken));
            }

            List<ClinicalFormResult> forms = await _dbContext.ClinicalForms
                .AsNoTracking()
                .Where(f => f.ClinicId == clinicId && !f.IsVoided)
                .OrderBy(f => f.SpecialtyCode)
                .Select(f => new ClinicalFormResult(f.Id, f.SpecialtyCode, f.TitleAr, f.TitleEn, f.JsonSchema, f.Version, f.IsActive, f.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                adminId.ToString(),
                "InspectClinical",
                _clock.UtcNow,
                clinicId,
                null,
                "SuperAdmin"));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ClinicClinicalInspectResult>.Success(new ClinicClinicalInspectResult(clinicId, projected, forms));
        });
    }

    public async Task<Result<IReadOnlyList<PortalVisitSummaryResult>>> ListPatientVisibleVisitsAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<Visit> visits = await _dbContext.Visits
                .AsNoTracking()
                .Where(v => v.ClinicId == clinicId && !v.IsVoided && v.PatientId == patientId && v.IsVisibleToPatient)
                .OrderByDescending(v => v.StartedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            var summaries = new List<PortalVisitSummaryResult>(visits.Count);
            foreach (Visit visit in visits)
            {
                VisitSessionResult full = await ProjectVisitAsync(clinicId, visit, cancellationToken);
                // Purpose-built portal DTO never exposes PrivateDoctorNotes
                summaries.Add(new PortalVisitSummaryResult(
                    full.Id,
                    full.StartedAtUtc,
                    full.EndedAtUtc,
                    full.DoctorName,
                    full.SpecialtyCode,
                    full.DiagnosisSummary,
                    full.Advice,
                    full.FollowUpDate,
                    full.Diagnoses,
                    full.Procedures,
                    full.Prescriptions));
            }

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                patientId.ToString(),
                "PortalListVisits",
                _clock.UtcNow,
                clinicId,
                patientId.ToString(),
                "Patient"));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<IReadOnlyList<PortalVisitSummaryResult>>.Success(summaries);
        });
    }

    public async Task<Result<PrescriptionPdfResult>> GetPatientPrescriptionPdfAsync(
        Guid clinicId,
        Guid patientId,
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Prescription? rx = await _dbContext.Prescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == prescriptionId && p.PatientId == patientId, cancellationToken);
            if (rx == null)
            {
                return Result<PrescriptionPdfResult>.Failure(Error.NotFound("PRESCRIPTION_NOT_FOUND", "Prescription was not found."));
            }

            Visit? visit = await _dbContext.Visits
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.ClinicId == clinicId && !v.IsVoided && v.Id == rx.VisitId && v.PatientId == patientId && v.IsVisibleToPatient, cancellationToken);
            if (visit == null)
            {
                return Result<PrescriptionPdfResult>.Failure(Error.NotFound("PRESCRIPTION_NOT_FOUND", "Prescription was not found."));
            }

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                patientId.ToString(),
                "PortalDownloadPrescriptionPdf",
                _clock.UtcNow,
                clinicId,
                prescriptionId.ToString(),
                "Patient"));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<PrescriptionPdfResult>.Success(await BuildPrescriptionPdfInternalAsync(clinicId, rx, visit, cancellationToken));
        });
    }

    private async Task<PrescriptionPdfResult> BuildPrescriptionPdfInternalAsync(
        Guid clinicId,
        Prescription rx,
        Visit visit,
        CancellationToken cancellationToken)
    {
        Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        Patient? patient = await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == rx.PatientId, cancellationToken);
        ClinicDoctor? doctor = await _dbContext.ClinicDoctors.AsNoTracking().FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.Id == rx.DoctorId, cancellationToken);

        List<PrescriptionItem> items = await _dbContext.PrescriptionItems
            .AsNoTracking()
            .Where(i => i.ClinicId == clinicId && !i.IsVoided && i.PrescriptionId == rx.Id)
            .ToListAsync(cancellationToken);

        var rxResult = new PrescriptionResult(
            rx.Id, rx.VisitId, rx.PatientId, rx.DoctorId, rx.Notes, rx.HasAllergyWarning, rx.AllergyWarningDetails,
            items.Select(i => new PrescriptionItemResult(
                i.Id, i.DrugName, i.ActiveIngredient, i.Dosage, i.Frequency, i.Duration, i.Instructions, i.TriggeredAllergyWarning)).ToList(),
            rx.CreatedAtUtc);

        byte[] pdfBytes = ClinicalPdfGenerator.GeneratePrescriptionPdf(
            clinic?.Name ?? "Clinic",
            patient?.FileNumber ?? "-",
            patient?.FullName ?? "Patient",
            doctor?.FullName ?? "Doctor",
            visit.SpecialtyCode,
            rxResult);

        return new PrescriptionPdfResult($"rx-{rx.Id:N}.pdf", "application/pdf", pdfBytes);
    }

    private Task<VisitSessionResult> ProjectVisitAsync(Guid clinicId, Visit visit, CancellationToken cancellationToken)
    {
        bool supportsDental = _pluginsByCode.TryGetValue(visit.SpecialtyCode, out ISpecialtyPlugin? plugin)
            ? plugin.SupportsDentalChart
            : string.Equals(visit.SpecialtyCode, "dental", StringComparison.OrdinalIgnoreCase);

        return ClinicalProjectionHelper.BuildVisitSessionAsync(
            _dbContext, clinicId, visit, supportsDental, _clock.UtcNow, cancellationToken);
    }

    private async Task<Visit?> LoadAuthorizedVisitAsync(Guid userId, Guid clinicId, Guid visitId, CancellationToken cancellationToken)
    {
        Visit? visit = await _dbContext.Visits
            .FirstOrDefaultAsync(v => v.ClinicId == clinicId && !v.IsVoided && v.Id == visitId, cancellationToken);
        if (visit == null)
        {
            return null;
        }

        Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);
        if (scopedDoctorId.HasValue && visit.DoctorId != scopedDoctorId.Value)
        {
            return null;
        }

        return visit;
    }

    private async Task<Guid?> ResolveScopedDoctorIdAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var perms = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (perms.IsSuccess &&
            (perms.Value.IsOwner || perms.Value.EffectivePermissions.Contains("clinic.settings.edit", StringComparer.OrdinalIgnoreCase)))
        {
            return null;
        }

        ClinicDoctor? doc = await _dbContext.ClinicDoctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.UserId == userId, cancellationToken);
        return doc?.Id;
    }

    private async Task<T> ExecuteForClinicAsync<T>(Guid clinicId, Func<Task<T>> action)
    {
        Guid? prev = _currentClinicContext.ClinicId;
        bool wasAdmin = _currentClinicContext.IsPlatformAdmin;
        try
        {
            _currentClinicContext.SetTenant(clinicId);
            return await action();
        }
        finally
        {
            if (wasAdmin)
            {
                _currentClinicContext.SetPlatformAdmin();
            }
            else if (prev.HasValue)
            {
                _currentClinicContext.SetTenant(prev.Value);
            }
            else
            {
                _currentClinicContext.Clear();
            }
        }
    }
}
