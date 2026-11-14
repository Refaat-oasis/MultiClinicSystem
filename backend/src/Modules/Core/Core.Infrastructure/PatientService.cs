using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Manages clinic patient registry, Arabic search, duplicate detection, doctor/specialty scoping,
/// medical history, notes, attachments behind IFileStorage, and Super Admin inspection.
/// </summary>
public sealed class PatientService : IPatientService, IPatientPortalQueries
{
    private const int MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    };

    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IFileStorage _fileStorage;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public PatientService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IPlatformAccess platformAccess,
        IFileStorage fileStorage,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _platformAccess = platformAccess ?? throw new ArgumentNullException(nameof(platformAccess));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<ClinicPatientScopingResult>> GetPatientScopingAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.read", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicPatientScopingResult>.Failure(gate.Error);
        }

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        if (clinic == null)
        {
            return Result<ClinicPatientScopingResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
        }

        return Result<ClinicPatientScopingResult>.Success(
            new ClinicPatientScopingResult(clinic.ScopePatientsByDoctor, clinic.ScopePatientsBySpecialty));
    }

    public async Task<Result<ClinicPatientScopingResult>> ConfigurePatientScopingAsync(
        Guid userId,
        Guid clinicId,
        bool scopeByDoctor,
        bool scopeBySpecialty,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinic.settings.edit", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicPatientScopingResult>.Failure(gate.Error);
        }

        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        if (clinic == null)
        {
            return Result<ClinicPatientScopingResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
        }

        bool prevDoctor = clinic.ScopePatientsByDoctor;
        bool prevSpecialty = clinic.ScopePatientsBySpecialty;
        clinic.SetPatientScoping(scopeByDoctor, scopeBySpecialty);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "PatientScopingUpdated",
            "Clinic",
            clinic.Id.ToString(),
            JsonSerializer.Serialize(new { ScopePatientsByDoctor = prevDoctor, ScopePatientsBySpecialty = prevSpecialty }),
            JsonSerializer.Serialize(new { clinic.ScopePatientsByDoctor, clinic.ScopePatientsBySpecialty }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result<ClinicPatientScopingResult>.Success(
            new ClinicPatientScopingResult(clinic.ScopePatientsByDoctor, clinic.ScopePatientsBySpecialty));
    }

    public async Task<Result<PatientSearchResult>> SearchPatientsAsync(
        Guid userId,
        Guid clinicId,
        string? query,
        int limit,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.read", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientSearchResult>.Failure(gate.Error);
        }

        int boundedLimit = Math.Clamp(limit <= 0 ? 25 : limit, 1, 50);
        string normQuery = ArabicNormalizer.NormalizeText(query);
        string normPhoneQuery = ArabicNormalizer.NormalizePhone(query);
        string rawQuery = query?.Trim() ?? string.Empty;

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var result = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var baseQuery = _dbContext.Patients
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && !p.IsVoided);

            if (!string.IsNullOrEmpty(normQuery) || !string.IsNullOrEmpty(normPhoneQuery) || !string.IsNullOrEmpty(rawQuery))
            {
                baseQuery = baseQuery.Where(p =>
                    (!string.IsNullOrEmpty(normQuery) && p.NormalizedName.Contains(normQuery)) ||
                    (!string.IsNullOrEmpty(normPhoneQuery) && p.NormalizedPhone.Contains(normPhoneQuery)) ||
                    (!string.IsNullOrEmpty(rawQuery) && p.FileNumber.Contains(rawQuery)));
            }

            List<Patient> candidates = await baseQuery
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            List<Patient> scoped = candidates.Where(p => IsPatientVisibleToScope(p, scopeContext)).ToList();
            int totalCount = scoped.Count;
            List<Patient> pageItems = scoped.Take(boundedLimit).ToList();

            List<Guid> pageIds = pageItems.Select(p => p.Id).ToList();
            Dictionary<Guid, AllergyStats> allergyStats = new();
            if (scopeContext.IsHistoryEnabled && pageIds.Count > 0)
            {
                var stats = await _dbContext.PatientAllergies
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId && !a.IsVoided && pageIds.Contains(a.PatientId))
                    .GroupBy(a => a.PatientId)
                    .Select(g => new
                    {
                        PatientId = g.Key,
                        Count = g.Count(),
                        HasSevere = g.Any(x => x.Severity == "Severe")
                    })
                    .ToListAsync(cancellationToken);

                foreach (var row in stats)
                {
                    allergyStats[row.PatientId] = new AllergyStats(row.Count, row.HasSevere);
                }
            }

            var summaries = pageItems.Select(p =>
            {
                allergyStats.TryGetValue(p.Id, out var stat);
                return new PatientSummaryItemResult(
                    p.Id,
                    p.FileNumber,
                    p.FullName,
                    p.Phone,
                    p.Gender,
                    p.DateOfBirth,
                    PatientAgeCalculator.CalculateAgeYears(p.DateOfBirth, p.ApproximateAgeYears, today),
                    p.AssignedDoctorId,
                    p.PrimarySpecialtyCode,
                    stat.Count,
                    stat.HasSevere,
                    p.CreatedAtUtc);
            }).ToList();

            return new PatientSearchResult(totalCount, summaries);
        });

        return Result<PatientSearchResult>.Success(result);
    }

    public async Task<Result<IReadOnlyList<PatientDuplicateWarningResult>>> CheckDuplicatesAsync(
        Guid userId,
        Guid clinicId,
        string fullName,
        string phone,
        Guid? excludePatientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.read", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PatientDuplicateWarningResult>>.Failure(gate.Error);
        }

        List<PatientDuplicateWarningResult> matches = await FindDuplicateWarningsAsync(clinicId, fullName, phone, excludePatientId, cancellationToken);
        return Result<IReadOnlyList<PatientDuplicateWarningResult>>.Success(matches);
    }

    public async Task<Result<PatientDetailResult>> CreatePatientAsync(
        Guid userId,
        Guid clinicId,
        CreatePatientInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.write", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientDetailResult>.Failure(gate.Error);
        }

        if (input.ClientPatientId.HasValue && input.ClientPatientId.Value == Guid.Empty)
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("INVALID_CLIENT_ID", "Client patient ID cannot be an empty GUID."));
        }

        if (string.IsNullOrWhiteSpace(input.FullName) || string.IsNullOrWhiteSpace(input.Phone))
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("PATIENT_REQUIRED_FIELDS", "Patient full name and primary phone are required."));
        }

        DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (input.DateOfBirth.HasValue && input.DateOfBirth.Value > today)
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("INVALID_DATE_OF_BIRTH", "Date of birth cannot be in the future."));
        }

        if (input.ApproximateAgeYears.HasValue && (input.ApproximateAgeYears.Value < 0 || input.ApproximateAgeYears.Value > 130))
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("INVALID_APPROXIMATE_AGE", "Approximate age must be between 0 and 130."));
        }

        // Sync idempotency check through ProcessedOperations
        if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
        {
            Guid? existingPatientId = await ExecuteForClinicAsync(clinicId, async () =>
                await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .Where(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value)
                    .Select(o => (Guid?)o.ResultEntityId)
                    .FirstOrDefaultAsync(cancellationToken));

            if (existingPatientId.HasValue)
            {
                return await BuildPatientDetailResultAsync(clinicId, existingPatientId.Value, userId, isSuperAdminInspect: false, cancellationToken);
            }
        }

        List<PatientDuplicateWarningResult> duplicateWarnings = await FindDuplicateWarningsAsync(
            clinicId, input.FullName, input.Phone, excludePatientId: null, cancellationToken);

        if (duplicateWarnings.Count > 0 && !input.ConfirmDuplicateCreation)
        {
            return Result<PatientDetailResult>.Failure(Error.Conflict(
                "DUPLICATE_PATIENT_WARNING",
                $"Found {duplicateWarnings.Count} potential duplicate patient(s). Confirm duplicate creation to proceed."));
        }

        string effectiveDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "server-default" : deviceId.Trim();
        Guid patientId = input.ClientPatientId ?? GuidV7.NewGuid();
        Patient? createdPatient = null;

        for (int attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                createdPatient = await ExecuteForClinicAsync(clinicId, async () =>
                {
                    await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                    string lockResource = $"NumberBlock_{clinicId:N}_PatientFile";
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;",
                        [lockResource],
                        cancellationToken);

                    NumberBlock? block = await _dbContext.NumberBlocks
                        .Where(n => n.ClinicId == clinicId && n.Kind == "PatientFile" && n.Status == "Active" && n.DeviceId == effectiveDeviceId)
                        .OrderBy(n => n.FromNumber)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (block == null)
                    {
                        block = await _dbContext.NumberBlocks
                            .Where(n => n.ClinicId == clinicId && n.Kind == "PatientFile" && n.Status == "Active")
                            .OrderBy(n => n.FromNumber)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    if (block == null || block.NextNumber > block.ToNumber)
                    {
                        long? maxTo = await _dbContext.NumberBlocks
                            .Where(n => n.ClinicId == clinicId && n.Kind == "PatientFile")
                            .MaxAsync(n => (long?)n.ToNumber, cancellationToken);

                        long fromNum = (maxTo ?? 0L) + 1L;
                        long toNum = fromNum + NumberBlock.DefaultBlockSize - 1L;
                        block = new NumberBlock(
                            GuidV7.NewGuid(),
                            clinicId,
                            effectiveDeviceId,
                            "PatientFile",
                            fromNum,
                            toNum,
                            _clock.UtcNow);
                        _dbContext.NumberBlocks.Add(block);
                    }

                    var consumed = block.ConsumeNext();
                    if (consumed.IsFailure)
                    {
                        throw new InvalidOperationException(consumed.Error.Message);
                    }

                    string fileNumber = $"P-{consumed.Value:D6}";
                    var patient = new Patient(
                        patientId,
                        clinicId,
                        fileNumber,
                        input.FullName,
                        input.Phone,
                        input.SecondaryPhone,
                        input.DateOfBirth,
                        input.ApproximateAgeYears,
                        input.Gender,
                        input.NationalId,
                        input.Address,
                        input.AssignedDoctorId,
                        input.PrimarySpecialtyCode,
                        userId,
                        _clock.UtcNow);

                    _dbContext.Patients.Add(patient);

                    if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
                    {
                        _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                            GuidV7.NewGuid(),
                            clinicId,
                            input.OperationId.Value,
                            "Patient",
                            patient.Id,
                            _clock.UtcNow));
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return patient;
                });

                break;
            }
            catch (Exception ex) when (attempt < 6 && (ex is DbUpdateException || ex is SqlException || ex.InnerException is DbUpdateException || ex.InnerException is SqlException))
            {
                _dbContext.ChangeTracker.Clear();
                await Task.Delay(attempt * 20, cancellationToken);
            }
        }

        if (createdPatient == null)
        {
            return Result<PatientDetailResult>.Failure(Error.Conflict("NUMBER_BLOCK_CONTENTION", "Could not allocate patient file number under contention. Please retry."));
        }

        // Audit IDs and file number only (never patient name, phone, or medical text).
        await _auditService.LogAsync(
            "PatientCreated",
            "Patient",
            createdPatient.Id.ToString(),
            null,
            JsonSerializer.Serialize(new { createdPatient.Id, createdPatient.FileNumber }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return await BuildPatientDetailResultAsync(
            clinicId,
            createdPatient.Id,
            userId,
            isSuperAdminInspect: false,
            cancellationToken,
            duplicateWarnings);
    }

    public async Task<Result<PatientDetailResult>> GetPatientAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.read", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientDetailResult>.Failure(gate.Error);
        }

        return await BuildPatientDetailResultAsync(clinicId, patientId, userId, isSuperAdminInspect: false, cancellationToken);
    }

    public async Task<Result<PatientDetailResult>> UpdatePatientAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        CreatePatientInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.write", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientDetailResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.FullName) || string.IsNullOrWhiteSpace(input.Phone))
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("PATIENT_REQUIRED_FIELDS", "Patient full name and primary phone are required."));
        }

        DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        if (input.DateOfBirth.HasValue && input.DateOfBirth.Value > today)
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("INVALID_DATE_OF_BIRTH", "Date of birth cannot be in the future."));
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool updated = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);

            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return false;
            }

            patient.UpdateDemographics(
                input.FullName,
                input.Phone,
                input.SecondaryPhone,
                input.DateOfBirth,
                input.ApproximateAgeYears,
                input.Gender,
                input.NationalId,
                input.Address,
                input.AssignedDoctorId,
                input.PrimarySpecialtyCode,
                _clock.UtcNow);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (!updated)
        {
            return Result<PatientDetailResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        await _auditService.LogAsync(
            "PatientUpdated",
            "Patient",
            patientId.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return await BuildPatientDetailResultAsync(clinicId, patientId, userId, isSuperAdminInspect: false, cancellationToken);
    }

    public async Task<Result> VoidPatientAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.write", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool voided = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);

            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return false;
            }

            patient.Void(_clock.UtcNow, userId.ToString(), reason);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (!voided)
        {
            return Result.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        await _auditService.LogAsync(
            "PatientVoided",
            "Patient",
            patientId.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PatientAllergyItemResult>> AddAllergyAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string allergen,
        string severity,
        string? reaction,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.history.write", "patient-history", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientAllergyItemResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(allergen))
        {
            return Result<PatientAllergyItemResult>.Failure(Error.Validation("ALLERGEN_REQUIRED", "Allergen name is required."));
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        PatientAllergy? created = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);

            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return null;
            }

            var entity = new PatientAllergy(
                GuidV7.NewGuid(),
                clinicId,
                patientId,
                allergen,
                severity,
                reaction,
                userId,
                _clock.UtcNow);

            _dbContext.PatientAllergies.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        });

        if (created == null)
        {
            return Result<PatientAllergyItemResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        await _auditService.LogAsync(
            "PatientAllergyAdded",
            "PatientAllergy",
            created.Id.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, AllergyId = created.Id, created.Severity }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result<PatientAllergyItemResult>.Success(new PatientAllergyItemResult(
            created.Id, created.PatientId, created.Allergen, created.Severity, created.Reaction, created.RecordedByUserId, created.CreatedAtUtc));
    }

    public async Task<Result> VoidAllergyAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid allergyId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.history.write", "patient-history", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool voided = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return false;
            }

            var allergy = await _dbContext.PatientAllergies
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.PatientId == patientId && a.Id == allergyId && !a.IsVoided, cancellationToken);
            if (allergy == null)
            {
                return false;
            }

            allergy.Void(_clock.UtcNow, userId.ToString(), reason);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (!voided)
        {
            return Result.Failure(Error.NotFound("ALLERGY_NOT_FOUND", "Patient allergy was not found."));
        }

        await _auditService.LogAsync(
            "PatientAllergyVoided",
            "PatientAllergy",
            allergyId.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, AllergyId = allergyId }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PatientDetailResult>> UpdateMedicalHistoryAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        UpdatePatientMedicalHistoryInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.history.write", "patient-history", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientDetailResult>.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool updated = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);

            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return false;
            }

            string chronicJson = SerializeCleanList(input.ChronicConditions);
            string medsJson = SerializeCleanList(input.CurrentMedications);
            string surgeriesJson = SerializeCleanList(input.PastSurgeries);

            patient.UpdateMedicalHistory(
                input.Summary,
                chronicJson,
                medsJson,
                surgeriesJson,
                input.MarkReviewed,
                userId,
                _clock.UtcNow);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (!updated)
        {
            return Result<PatientDetailResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        await _auditService.LogAsync(
            "PatientHistoryUpdated",
            "Patient",
            patientId.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, input.MarkReviewed }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return await BuildPatientDetailResultAsync(clinicId, patientId, userId, isSuperAdminInspect: false, cancellationToken);
    }

    public async Task<Result<PatientNoteItemResult>> AddNoteAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string noteText,
        bool isPrivate,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.history.write", "patient-history", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientNoteItemResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(noteText))
        {
            return Result<PatientNoteItemResult>.Failure(Error.Validation("NOTE_TEXT_REQUIRED", "Note text is required."));
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        PatientNote? created = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);

            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return null;
            }

            Guid? doctorUserId = scopeContext.DoctorRecordId.HasValue ? userId : null;
            var entity = new PatientNote(
                GuidV7.NewGuid(),
                clinicId,
                patientId,
                doctorUserId,
                noteText,
                isPrivate,
                userId,
                _clock.UtcNow);

            _dbContext.PatientNotes.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        });

        if (created == null)
        {
            return Result<PatientNoteItemResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        await _auditService.LogAsync(
            "PatientNoteAdded",
            "PatientNote",
            created.Id.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, NoteId = created.Id, created.IsPrivate }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result<PatientNoteItemResult>.Success(new PatientNoteItemResult(
            created.Id, created.PatientId, created.DoctorUserId, created.NoteText, created.IsPrivate, created.CreatedByUserId, created.CreatedAtUtc));
    }

    public async Task<Result> VoidNoteAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid noteId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.history.write", "patient-history", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool voided = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return false;
            }

            var note = await _dbContext.PatientNotes
                .FirstOrDefaultAsync(n => n.ClinicId == clinicId && n.PatientId == patientId && n.Id == noteId && !n.IsVoided, cancellationToken);
            if (note == null)
            {
                return false;
            }

            if (note.IsPrivate && note.CreatedByUserId != userId && note.DoctorUserId != userId && !scopeContext.IsOwnerOrAdmin)
            {
                return false;
            }

            note.Void(_clock.UtcNow, userId.ToString(), reason);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (!voided)
        {
            return Result.Failure(Error.NotFound("NOTE_NOT_FOUND", "Patient note was not found."));
        }

        await _auditService.LogAsync(
            "PatientNoteVoided",
            "PatientNote",
            noteId.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, NoteId = noteId }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PatientAttachmentItemResult>> UploadAttachmentAsync(
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
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "attachments.write", "attachments", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientAttachmentItemResult>.Failure(gate.Error);
        }

        if (content == null || content.Length == 0)
        {
            return Result<PatientAttachmentItemResult>.Failure(Error.Validation("EMPTY_FILE", "Attachment file content cannot be empty."));
        }

        if (content.Length > MaxAttachmentBytes)
        {
            return Result<PatientAttachmentItemResult>.Failure(Error.Validation("FILE_TOO_LARGE", "Attachment exceeds maximum allowed size of 10 MB."));
        }

        string cleanContentType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedAttachmentContentTypes.Contains(cleanContentType))
        {
            return Result<PatientAttachmentItemResult>.Failure(Error.Validation("UNSUPPORTED_FILE_TYPE", "Allowed attachment types are JPEG, PNG, WebP, and PDF."));
        }

        if (!HasValidMagicBytes(cleanContentType, content))
        {
            return Result<PatientAttachmentItemResult>.Failure(Error.Validation("INVALID_FILE_SIGNATURE", "File header does not match declared content type."));
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool patientExistsAndVisible = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            return patient != null && IsPatientVisibleToScope(patient, scopeContext);
        });

        if (!patientExistsAndVisible)
        {
            return Result<PatientAttachmentItemResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        string sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        Guid attachmentId = GuidV7.NewGuid();
        string safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "attachment.bin" : fileName.Trim());
        string storageKey = $"patients/{clinicId:N}/{patientId:N}/{attachmentId:N}-{safeFileName}";

        await _fileStorage.SaveAsync(storageKey, content, cleanContentType, cancellationToken);

        string? thumbStorageKey = null;
        if (cleanContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            thumbStorageKey = $"patients/{clinicId:N}/{patientId:N}/thumbs/{attachmentId:N}-{safeFileName}";
            await _fileStorage.SaveAsync(thumbStorageKey, content, cleanContentType, cancellationToken);
        }

        var attachment = new PatientAttachment(
            attachmentId,
            clinicId,
            patientId,
            category,
            safeFileName,
            cleanContentType,
            content.LongLength,
            sha256,
            storageKey,
            thumbStorageKey,
            notes,
            userId,
            _clock.UtcNow);

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            _dbContext.PatientAttachments.Add(attachment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        await _auditService.LogAsync(
            "PatientAttachmentUploaded",
            "PatientAttachment",
            attachment.Id.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, AttachmentId = attachment.Id, attachment.ContentType, attachment.SizeBytes }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result<PatientAttachmentItemResult>.Success(new PatientAttachmentItemResult(
            attachment.Id,
            attachment.PatientId,
            attachment.Category,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.Sha256Hash,
            !string.IsNullOrEmpty(attachment.ThumbnailStorageKey),
            attachment.Notes,
            attachment.UploadedByUserId,
            attachment.CreatedAtUtc));
    }

    public async Task<Result<PatientAttachmentDownloadResult>> DownloadAttachmentAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        bool thumbnail,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "attachments.read", "attachments", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        PatientAttachment? attachment = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return null;
            }

            return await _dbContext.PatientAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.PatientId == patientId && a.Id == attachmentId && !a.IsVoided, cancellationToken);
        });

        if (attachment == null)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment was not found."));
        }

        string key = thumbnail && !string.IsNullOrEmpty(attachment.ThumbnailStorageKey)
            ? attachment.ThumbnailStorageKey
            : attachment.StorageKey;

        byte[]? bytes = await _fileStorage.ReadAsync(key, cancellationToken);
        if (bytes == null)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.NotFound("ATTACHMENT_FILE_MISSING", "Attachment payload was not found in storage."));
        }

        return Result<PatientAttachmentDownloadResult>.Success(
            new PatientAttachmentDownloadResult(attachment.FileName, attachment.ContentType, bytes));
    }

    public async Task<Result> VoidAttachmentAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "attachments.write", "attachments", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        bool voided = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return false;
            }

            var attachment = await _dbContext.PatientAttachments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.PatientId == patientId && a.Id == attachmentId && !a.IsVoided, cancellationToken);
            if (attachment == null)
            {
                return false;
            }

            attachment.Void(_clock.UtcNow, userId.ToString(), reason);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (!voided)
        {
            return Result.Failure(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment was not found."));
        }

        await _auditService.LogAsync(
            "PatientAttachmentVoided",
            "PatientAttachment",
            attachmentId.ToString(),
            null,
            JsonSerializer.Serialize(new { PatientId = patientId, AttachmentId = attachmentId }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PatientDetailResult>> InspectPatientAsSuperAdminAsync(
        string adminId,
        Guid clinicId,
        Guid patientId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<PatientDetailResult>.Failure(Error.Validation("ACCESS_REASON_REQUIRED", "An inspection reason is required."));
        }

        return await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                adminId,
                reason.Trim(),
                _clock.UtcNow,
                clinicId,
                $"InspectPatient:{patientId}",
                "SuperAdmin"));

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await BuildPatientDetailResultAsync(clinicId, patientId, Guid.Empty, isSuperAdminInspect: true, cancellationToken);
        }, reason, adminId, cancellationToken);
    }

    private async Task<List<PatientDuplicateWarningResult>> FindDuplicateWarningsAsync(
        Guid clinicId,
        string fullName,
        string phone,
        Guid? excludePatientId,
        CancellationToken cancellationToken)
    {
        string normName = ArabicNormalizer.NormalizeText(fullName);
        string normPhone = ArabicNormalizer.NormalizePhone(phone);

        List<PatientDuplicateCandidate> candidates = await ExecuteForClinicAsync(clinicId, async () =>
            await _dbContext.Patients
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && !p.IsVoided &&
                    ((!string.IsNullOrEmpty(normPhone) && p.NormalizedPhone == normPhone) ||
                     (!string.IsNullOrEmpty(normName) && p.NormalizedName == normName)))
                .Select(p => new PatientDuplicateCandidate(p.Id, p.FileNumber, p.FullName, p.NormalizedName, p.Phone, p.NormalizedPhone))
                .ToListAsync(cancellationToken));

        return PatientDuplicateDetector.FindMatches(fullName, phone, candidates, excludePatientId)
            .Select(m => new PatientDuplicateWarningResult(m.PatientId, m.FileNumber, m.FullName, m.Phone, m.ReasonCode, m.ConfidencePercent))
            .ToList();
    }

    private async Task<Result<PatientDetailResult>> BuildPatientDetailResultAsync(
        Guid clinicId,
        Guid patientId,
        Guid callerUserId,
        bool isSuperAdminInspect,
        CancellationToken cancellationToken,
        IReadOnlyList<PatientDuplicateWarningResult>? explicitWarnings = null)
    {
        PatientScopingContext scopeContext = isSuperAdminInspect
            ? await ResolveSuperAdminViewContextAsync(clinicId, cancellationToken)
            : await ResolveScopingContextAsync(callerUserId, clinicId, cancellationToken);

        DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        PatientDetailResult? detail = await ExecuteWithCurrentOrClinicAsync(clinicId, isSuperAdminInspect, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);

            if (patient == null || (!isSuperAdminInspect && !IsPatientVisibleToScope(patient, scopeContext)))
            {
                return null;
            }

            List<PatientAllergyItemResult> allergies = new();
            List<PatientNoteItemResult> notes = new();
            List<string> chronicConditions = new();
            List<string> currentMedications = new();
            List<string> pastSurgeries = new();
            string? summary = null;
            bool historyReviewed = false;

            if (scopeContext.IsHistoryEnabled)
            {
                allergies = await _dbContext.PatientAllergies
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId && a.PatientId == patientId && !a.IsVoided)
                    .OrderByDescending(a => a.Severity == "Severe")
                    .ThenByDescending(a => a.CreatedAtUtc)
                    .Select(a => new PatientAllergyItemResult(a.Id, a.PatientId, a.Allergen, a.Severity, a.Reaction, a.RecordedByUserId, a.CreatedAtUtc))
                    .ToListAsync(cancellationToken);

                var rawNotes = await _dbContext.PatientNotes
                    .AsNoTracking()
                    .Where(n => n.ClinicId == clinicId && n.PatientId == patientId && !n.IsVoided)
                    .OrderByDescending(n => n.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                notes = rawNotes
                    .Where(n => !n.IsPrivate || (!isSuperAdminInspect && (n.CreatedByUserId == callerUserId || n.DoctorUserId == callerUserId)))
                    .Select(n => new PatientNoteItemResult(n.Id, n.PatientId, n.DoctorUserId, n.NoteText, n.IsPrivate, n.CreatedByUserId, n.CreatedAtUtc))
                    .ToList();

                chronicConditions = DeserializeList(patient.ChronicConditionsJson);
                currentMedications = DeserializeList(patient.CurrentMedicationsJson);
                pastSurgeries = DeserializeList(patient.PastSurgeriesJson);
                summary = patient.MedicalHistorySummary;
                historyReviewed = patient.IsMedicalHistoryReviewed;
            }

            List<PatientAttachmentItemResult> attachments = new();
            if (scopeContext.IsAttachmentsEnabled)
            {
                attachments = await _dbContext.PatientAttachments
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId && a.PatientId == patientId && !a.IsVoided)
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Select(a => new PatientAttachmentItemResult(
                        a.Id,
                        a.PatientId,
                        a.Category,
                        a.FileName,
                        a.ContentType,
                        a.SizeBytes,
                        a.Sha256Hash,
                        !string.IsNullOrEmpty(a.ThumbnailStorageKey),
                        a.Notes,
                        a.UploadedByUserId,
                        a.CreatedAtUtc))
                    .ToListAsync(cancellationToken);
            }

            return new PatientDetailResult(
                patient.Id,
                patient.ClinicId,
                patient.FileNumber,
                patient.FullName,
                patient.Phone,
                patient.SecondaryPhone,
                patient.DateOfBirth,
                patient.ApproximateAgeYears,
                PatientAgeCalculator.CalculateAgeYears(patient.DateOfBirth, patient.ApproximateAgeYears, today),
                patient.Gender,
                patient.NationalId,
                patient.Address,
                patient.AssignedDoctorId,
                patient.PrimarySpecialtyCode,
                scopeContext.IsHistoryEnabled,
                historyReviewed,
                summary,
                chronicConditions,
                currentMedications,
                pastSurgeries,
                allergies,
                notes,
                scopeContext.IsAttachmentsEnabled,
                attachments,
                explicitWarnings ?? Array.Empty<PatientDuplicateWarningResult>(),
                patient.CreatedAtUtc,
                patient.UpdatedAtUtc);
        });

        if (detail == null)
        {
            return Result<PatientDetailResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
        }

        return Result<PatientDetailResult>.Success(detail);
    }

    private async Task<PatientScopingContext> ResolveScopingContextAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics
            .AsNoTracking()
            .Where(c => c.Id == clinicId)
            .Select(c => new { c.ScopePatientsByDoctor, c.ScopePatientsBySpecialty })
            .FirstOrDefaultAsync(cancellationToken);

        var perms = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        HashSet<string> enabledModules = perms.IsSuccess
            ? new HashSet<string>(perms.Value.EnabledModules, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool isHistoryEnabled = enabledModules.Contains("patient-history");
        bool isAttachmentsEnabled = enabledModules.Contains("attachments");
        bool isOwnerOrAdmin = perms.IsSuccess && (perms.Value.IsOwner ||
            perms.Value.EffectivePermissions.Contains("clinic.settings.edit", StringComparer.OrdinalIgnoreCase));

        Guid? doctorId = null;
        HashSet<string> doctorSpecialties = new(StringComparer.OrdinalIgnoreCase);

        var doctor = await ExecuteForClinicAsync(clinicId, async () =>
            await _dbContext.ClinicDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.UserId == userId && !d.IsVoided, cancellationToken));

        if (doctor != null)
        {
            doctorId = doctor.Id;
            foreach (string code in DeserializeList(doctor.SpecialtyCodesJson))
            {
                doctorSpecialties.Add(code.ToLowerInvariant());
            }
        }

        return new PatientScopingContext(
            clinic?.ScopePatientsByDoctor ?? false,
            clinic?.ScopePatientsBySpecialty ?? false,
            isOwnerOrAdmin,
            doctorId,
            doctorSpecialties,
            isHistoryEnabled,
            isAttachmentsEnabled);
    }

    private async Task<PatientScopingContext> ResolveSuperAdminViewContextAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        List<string> enabled = await _dbContext.ClinicModules
            .AsNoTracking()
            .Where(m => m.ClinicId == clinicId && m.Enabled)
            .Select(m => m.ModuleKey)
            .ToListAsync(cancellationToken);

        HashSet<string> set = new(enabled, StringComparer.OrdinalIgnoreCase);
        return new PatientScopingContext(
            ScopeByDoctor: false,
            ScopeBySpecialty: false,
            IsOwnerOrAdmin: true,
            DoctorRecordId: null,
            DoctorSpecialtyCodes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            IsHistoryEnabled: set.Contains("patient-history"),
            IsAttachmentsEnabled: set.Contains("attachments"));
    }

    private static bool IsPatientVisibleToScope(Patient patient, PatientScopingContext scope)
    {
        if (scope.IsOwnerOrAdmin || !scope.DoctorRecordId.HasValue)
        {
            return true;
        }

        if (scope.ScopeByDoctor && patient.AssignedDoctorId != scope.DoctorRecordId.Value)
        {
            return false;
        }

        if (scope.ScopeBySpecialty &&
            (string.IsNullOrWhiteSpace(patient.PrimarySpecialtyCode) ||
             !scope.DoctorSpecialtyCodes.Contains(patient.PrimarySpecialtyCode)))
        {
            return false;
        }

        return true;
    }

    private static bool HasValidMagicBytes(string contentType, byte[] bytes)
    {
        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        }

        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        }

        if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 12 &&
                   bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                   bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
        }

        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 4 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' && bytes[3] == (byte)'F';
        }

        return false;
    }

    private static string SerializeCleanList(IReadOnlyList<string>? items)
    {
        if (items == null || items.Count == 0)
        {
            return "[]";
        }

        List<string> cleaned = items
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(cleaned);
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public async Task<Result> SetNotePortalVisibilityAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid noteId,
        bool visibleToPatient,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.history.write", "patient-history", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return Result.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            var note = await _dbContext.PatientNotes
                .FirstOrDefaultAsync(n => n.ClinicId == clinicId && n.PatientId == patientId && n.Id == noteId && !n.IsVoided, cancellationToken);
            if (note == null)
            {
                return Result.Failure(Error.NotFound("NOTE_NOT_FOUND", "Patient note was not found."));
            }

            if (note.IsPrivate && visibleToPatient)
            {
                return Result.Failure(Error.Validation("PRIVATE_NOTE_CANNOT_BE_SHARED", "Doctor-private notes cannot be shared with the patient portal."));
            }

            bool previous = note.VisibleToPatient;
            note.SetVisibleToPatient(visibleToPatient);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PatientNotePortalVisibilityChanged",
                "PatientNote",
                note.Id.ToString(),
                JsonSerializer.Serialize(new { VisibleToPatient = previous }),
                JsonSerializer.Serialize(new { VisibleToPatient = note.VisibleToPatient }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result.Success();
        });
    }

    public async Task<Result> SetAttachmentPortalVisibilityAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        bool sharedWithPatient,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "attachments.write", "attachments", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return Result.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            var attachment = await _dbContext.PatientAttachments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.PatientId == patientId && a.Id == attachmentId && !a.IsVoided, cancellationToken);
            if (attachment == null)
            {
                return Result.Failure(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment was not found."));
            }

            bool previous = attachment.SharedWithPatient;
            attachment.SetSharedWithPatient(sharedWithPatient);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PatientAttachmentPortalVisibilityChanged",
                "PatientAttachment",
                attachment.Id.ToString(),
                JsonSerializer.Serialize(new { SharedWithPatient = previous }),
                JsonSerializer.Serialize(new { SharedWithPatient = attachment.SharedWithPatient }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result.Success();
        });
    }

    public async Task<Result> UpdatePatientPortalProfileAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? email,
        string? bloodType,
        string? guardianName,
        string? guardianEmail,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "patients.write", "patients", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var scopeContext = await ResolveScopingContextAsync(userId, clinicId, cancellationToken);
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            var patient = await _dbContext.Patients
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null || !IsPatientVisibleToScope(patient, scopeContext))
            {
                return Result.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            string? beforeEmail = patient.Email;
            string? beforeBlood = patient.BloodType;
            patient.SetPortalProfile(email, bloodType, guardianName, guardianEmail, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PatientPortalProfileUpdated",
                "Patient",
                patient.Id.ToString(),
                JsonSerializer.Serialize(new { HasEmail = !string.IsNullOrEmpty(beforeEmail), BloodType = beforeBlood }),
                JsonSerializer.Serialize(new { HasEmail = !string.IsNullOrEmpty(patient.Email), BloodType = patient.BloodType }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result.Success();
        });
    }

    public async Task<Result<PortalPatientSummaryResult>> GetPatientSummaryForPortalAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Patient? patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == patientId, cancellationToken);
            if (patient == null)
            {
                return Result<PortalPatientSummaryResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            int? ageYears = ComputeAgeYears(patient.DateOfBirth, patient.ApproximateAgeYears);
            return Result<PortalPatientSummaryResult>.Success(new PortalPatientSummaryResult(
                patient.Id,
                patient.FileNumber,
                patient.FullName,
                ageYears,
                patient.DateOfBirth,
                patient.Gender,
                patient.BloodType,
                patient.Email,
                patient.GuardianName));
        });
    }

    public async Task<Result<PortalMedicalBackgroundResult>> GetPatientMedicalBackgroundForPortalAsync(
        Guid clinicId,
        Guid patientId,
        bool includeMedicalBackground,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Patient? patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == patientId, cancellationToken);
            if (patient == null)
            {
                return Result<PortalMedicalBackgroundResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            List<PatientAllergyItemResult> allergies = Array.Empty<PatientAllergyItemResult>().ToList();
            List<string> chronic = new();
            List<string> medications = new();
            List<string> surgeries = new();
            List<PortalVisibleNoteItemResult> visibleNotes = new();

            if (includeMedicalBackground)
            {
                allergies = (await _dbContext.PatientAllergies
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId)
                    .OrderBy(a => a.CreatedAtUtc)
                    .ToListAsync(cancellationToken))
                    .OrderByDescending(a => string.Equals(a.Severity, "Severe", StringComparison.OrdinalIgnoreCase) ? 3 :
                                           string.Equals(a.Severity, "Moderate", StringComparison.OrdinalIgnoreCase) ? 2 : 1)
                    .ThenBy(a => a.CreatedAtUtc)
                    .Select(a => new PatientAllergyItemResult(
                        a.Id, a.PatientId, a.Allergen, a.Severity, a.Reaction, a.RecordedByUserId, a.CreatedAtUtc))
                    .ToList();

                chronic = DeserializeList(patient.ChronicConditionsJson);
                medications = DeserializeList(patient.CurrentMedicationsJson);
                surgeries = DeserializeList(patient.PastSurgeriesJson);

                visibleNotes = await _dbContext.PatientNotes
                    .AsNoTracking()
                    .Where(n => n.ClinicId == clinicId && !n.IsVoided && n.PatientId == patientId && !n.IsPrivate && n.VisibleToPatient)
                    .OrderByDescending(n => n.CreatedAtUtc)
                    .Select(n => new PortalVisibleNoteItemResult(n.Id, n.NoteText, n.CreatedAtUtc))
                    .ToListAsync(cancellationToken);
            }

            List<PortalSharedAttachmentItemResult> sharedAttachments = await _dbContext.PatientAttachments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId && a.SharedWithPatient)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Select(a => new PortalSharedAttachmentItemResult(
                    a.Id, a.Category, a.FileName, a.ContentType, a.SizeBytes, a.Notes, a.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            return Result<PortalMedicalBackgroundResult>.Success(new PortalMedicalBackgroundResult(
                allergies,
                chronic,
                medications,
                surgeries,
                includeMedicalBackground && patient.IsMedicalHistoryReviewed,
                includeMedicalBackground ? patient.MedicalHistorySummary : null,
                visibleNotes,
                sharedAttachments));
        });
    }

    public async Task<Result<PortalContactSnapshotResult>> GetPatientPortalContactAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Patient? patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == patientId, cancellationToken);
            if (patient == null)
            {
                return Result<PortalContactSnapshotResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            return Result<PortalContactSnapshotResult>.Success(new PortalContactSnapshotResult(
                patient.Id,
                patient.FileNumber,
                patient.FullName,
                patient.DateOfBirth,
                patient.Email,
                patient.GuardianName,
                patient.GuardianEmail));
        });
    }

    public async Task<Result<PortalContactSnapshotResult>> FindPatientByFileNumberAndDobForPortalAsync(
        Guid clinicId,
        string fileNumber,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileNumber))
        {
            return Result<PortalContactSnapshotResult>.Failure(Error.Validation("FILE_NUMBER_REQUIRED", "File number is required."));
        }

        string trimmedFile = fileNumber.Trim();
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Patient? patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.ClinicId == clinicId && !p.IsVoided && p.FileNumber == trimmedFile && p.DateOfBirth == dateOfBirth,
                    cancellationToken);
            if (patient == null)
            {
                return Result<PortalContactSnapshotResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            return Result<PortalContactSnapshotResult>.Success(new PortalContactSnapshotResult(
                patient.Id,
                patient.FileNumber,
                patient.FullName,
                patient.DateOfBirth,
                patient.Email,
                patient.GuardianName,
                patient.GuardianEmail));
        });
    }

    public async Task<Result<PatientAttachmentDownloadResult>> DownloadSharedAttachmentForPortalAsync(
        Guid clinicId,
        Guid patientId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        PatientAttachment? attachment = await ExecuteForClinicAsync(clinicId, async () =>
        {
            return await _dbContext.PatientAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId && a.Id == attachmentId && a.SharedWithPatient,
                    cancellationToken);
        });

        if (attachment == null)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment was not found."));
        }

        byte[]? bytes = await _fileStorage.ReadAsync(attachment.StorageKey, cancellationToken);
        if (bytes == null)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.NotFound("ATTACHMENT_FILE_MISSING", "Attachment payload was not found in storage."));
        }

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                patientId.ToString(),
                "PortalDownloadAttachment",
                _clock.UtcNow,
                clinicId,
                attachmentId.ToString(),
                "Patient"));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        return Result<PatientAttachmentDownloadResult>.Success(
            new PatientAttachmentDownloadResult(attachment.FileName, attachment.ContentType, bytes));
    }

    private int? ComputeAgeYears(DateOnly? dateOfBirth, int? approximateAgeYears)
    {
        if (!dateOfBirth.HasValue)
        {
            return approximateAgeYears;
        }

        DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        int age = today.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value > today.AddYears(-age))
        {
            age--;
        }

        return age < 0 ? 0 : age;
    }

    private Task<T> ExecuteWithCurrentOrClinicAsync<T>(Guid clinicId, bool keepPlatformAdmin, Func<Task<T>> action)
    {
        return keepPlatformAdmin ? action() : ExecuteForClinicAsync(clinicId, action);
    }

    private async Task<T> ExecuteForClinicAsync<T>(Guid clinicId, Func<Task<T>> action)
    {
        Guid? previousClinicId = _currentClinicContext.ClinicId;
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
            else if (previousClinicId.HasValue)
            {
                _currentClinicContext.SetTenant(previousClinicId.Value);
            }
            else
            {
                _currentClinicContext.Clear();
            }
        }
    }

    private readonly record struct AllergyStats(int Count, bool HasSevere);

    private sealed record PatientScopingContext(
        bool ScopeByDoctor,
        bool ScopeBySpecialty,
        bool IsOwnerOrAdmin,
        Guid? DoctorRecordId,
        HashSet<string> DoctorSpecialtyCodes,
        bool IsHistoryEnabled,
        bool IsAttachmentsEnabled);
}
