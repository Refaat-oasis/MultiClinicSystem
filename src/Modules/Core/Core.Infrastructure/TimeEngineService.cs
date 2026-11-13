using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 13 adaptive average-time learning per doctor and appointment category,
/// including idempotent outbox/background processing, excused-session filtering, outlier trimming,
/// step-size bounds, per-doctor toggles, Scheduling duration contract, and Super Admin inspect.
/// </summary>
public sealed class TimeEngineService : ITimeEngineService, ITimeEngineDurationProvider
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IClock _clock;

    public TimeEngineService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IAuditService auditService,
        IPlatformAccess platformAccess,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _platformAccess = platformAccess ?? throw new ArgumentNullException(nameof(platformAccess));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<int?> GetAppliedDurationMinutesAsync(
        Guid clinicId,
        Guid doctorId,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (clinicId == Guid.Empty || doctorId == Guid.Empty || categoryId == Guid.Empty)
        {
            return null;
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool moduleEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "time-engine" && m.Enabled, cancellationToken);

            if (!moduleEnabled)
            {
                return (int?)null;
            }

            CategoryTimeStat? stat = await _dbContext.CategoryTimeStats
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.ClinicId == clinicId &&
                         !s.IsVoided &&
                         s.DoctorId == doctorId &&
                         s.CategoryId == categoryId,
                    cancellationToken);

            if (stat == null || !stat.IsDoctorEngineEnabled || !stat.IsAdopted || stat.AppliedDurationMinutes < 5)
            {
                return null;
            }

            return stat.AppliedDurationMinutes;
        });
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAppliedDurationsByDoctorAsync(
        Guid clinicId,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (clinicId == Guid.Empty || categoryId == Guid.Empty)
        {
            return new Dictionary<Guid, int>();
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool moduleEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "time-engine" && m.Enabled, cancellationToken);

            if (!moduleEnabled)
            {
                return new Dictionary<Guid, int>();
            }

            List<CategoryTimeStat> adopted = await _dbContext.CategoryTimeStats
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId &&
                            !s.IsVoided &&
                            s.CategoryId == categoryId &&
                            s.IsDoctorEngineEnabled &&
                            s.IsAdopted &&
                            s.AppliedDurationMinutes >= 5)
                .ToListAsync(cancellationToken);

            return adopted.ToDictionary(s => s.DoctorId, s => s.AppliedDurationMinutes);
        });
    }

    public async Task<Result<ClinicTimeEngineSettingResult>> GetClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.read", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicTimeEngineSettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicTimeEngineSetting settings = await GetOrBuildClinicSettingAsync(clinicId, _clock.UtcNow, cancellationToken);
            return Result<ClinicTimeEngineSettingResult>.Success(MapClinicSettings(settings));
        });
    }

    public async Task<Result<ClinicTimeEngineSettingResult>> UpdateClinicSettingsAsync(
        Guid userId,
        Guid clinicId,
        UpdateClinicTimeEngineSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.manage", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicTimeEngineSettingResult>.Failure(gate.Error);
        }

        if (input.MinSampleSize < 2 || input.MinSampleSize > 100 ||
            input.TrimFraction < 0m || input.TrimFraction > 0.40m ||
            input.MaxStepChangePercent < 1m || input.MaxStepChangePercent > 100m)
        {
            return Result<ClinicTimeEngineSettingResult>.Failure(
                Error.Validation("INVALID_TIME_ENGINE_SETTINGS", "Sample size (2-100), trim fraction (0-0.4), or step limit (1-100%) is out of range."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicTimeEngineSetting? settings = await _dbContext.ClinicTimeEngineSettings
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

            if (settings == null)
            {
                settings = new ClinicTimeEngineSetting(GuidV7.NewGuid(), clinicId, now);
                _dbContext.ClinicTimeEngineSettings.Add(settings);
            }

            settings.Update(input.MinSampleSize, input.TrimFraction, input.MaxStepChangePercent, input.OperationId, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await RecalculateAllPairsInternalAsync(clinicId, now, cancellationToken);

            await _auditService.LogAsync(
                "ClinicTimeEngineSettingsUpdated",
                "ClinicTimeEngineSetting",
                settings.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { settings.MinSampleSize, settings.TrimFraction, settings.MaxStepChangePercent }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ClinicTimeEngineSettingResult>.Success(MapClinicSettings(settings));
        });
    }

    public async Task<Result<IReadOnlyList<DoctorTimeEngineSettingResult>>> ListDoctorSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.read", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<DoctorTimeEngineSettingResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            List<ClinicDoctor> doctors = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .Where(d => d.ClinicId == clinicId && !d.IsVoided && d.IsActive)
                .OrderBy(d => d.FullName)
                .Take(200)
                .ToListAsync(cancellationToken);

            List<DoctorTimeEngineSetting> saved = await _dbContext.DoctorTimeEngineSettings
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId && !s.IsVoided)
                .ToListAsync(cancellationToken);

            Dictionary<Guid, DoctorTimeEngineSetting> byDoc = saved.ToDictionary(s => s.DoctorId);

            List<DoctorTimeEngineSettingResult> output = doctors
                .Select(d => byDoc.TryGetValue(d.Id, out DoctorTimeEngineSetting? s)
                    ? new DoctorTimeEngineSettingResult(clinicId, d.Id, d.FullName, s.IsEngineEnabled, s.UpdatedAtUtc)
                    : new DoctorTimeEngineSettingResult(clinicId, d.Id, d.FullName, true, now))
                .ToList();

            return Result<IReadOnlyList<DoctorTimeEngineSettingResult>>.Success(output);
        });
    }

    public async Task<Result<DoctorTimeEngineSettingResult>> SetDoctorEngineEnabledAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        SetDoctorTimeEngineInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.manage", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<DoctorTimeEngineSettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicDoctor? doctor = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.Id == doctorId, cancellationToken);

            if (doctor == null)
            {
                return Result<DoctorTimeEngineSettingResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            DoctorTimeEngineSetting? setting = await _dbContext.DoctorTimeEngineSettings
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.DoctorId == doctorId, cancellationToken);

            if (setting == null)
            {
                setting = new DoctorTimeEngineSetting(GuidV7.NewGuid(), clinicId, doctorId, input.IsEngineEnabled, input.OperationId, now);
                _dbContext.DoctorTimeEngineSettings.Add(setting);
            }
            else
            {
                setting.SetEnabled(input.IsEngineEnabled, input.OperationId, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Recompute stats for this doctor across all categories immediately
            List<Guid> categoryIds = await _dbContext.CategoryTimeStats
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId && !s.IsVoided && s.DoctorId == doctorId)
                .Select(s => s.CategoryId)
                .Union(_dbContext.SessionDurations
                    .AsNoTracking()
                    .Where(d => d.ClinicId == clinicId && !d.IsVoided && d.DoctorId == doctorId)
                    .Select(d => d.CategoryId))
                .ToListAsync(cancellationToken);

            foreach (Guid catId in categoryIds)
            {
                await RecalculatePairInternalAsync(clinicId, doctorId, catId, now, cancellationToken, allowStepAdvance: false);
            }

            await _auditService.LogAsync(
                "DoctorTimeEngineToggled",
                "DoctorTimeEngineSetting",
                setting.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { doctorId, input.IsEngineEnabled }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<DoctorTimeEngineSettingResult>.Success(
                new DoctorTimeEngineSettingResult(clinicId, doctor.Id, doctor.FullName, setting.IsEngineEnabled, setting.UpdatedAtUtc));
        });
    }

    public async Task<Result<IReadOnlyList<CategoryTimeStatResult>>> ListCategoryStatsAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        Guid? categoryId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.read", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<CategoryTimeStatResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<CategoryTimeStat> query = _dbContext.CategoryTimeStats
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId && !s.IsVoided);

            if (doctorId.HasValue && doctorId.Value != Guid.Empty)
            {
                query = query.Where(s => s.DoctorId == doctorId.Value);
            }

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                query = query.Where(s => s.CategoryId == categoryId.Value);
            }

            List<CategoryTimeStat> stats = await query
                .OrderByDescending(s => s.LastUpdatedAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<CategoryTimeStatResult>>.Success(await ProjectStatsAsync(clinicId, stats, cancellationToken));
        });
    }

    public async Task<Result<IReadOnlyList<SessionDurationResult>>> ListSessionDurationsAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        Guid? categoryId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.read", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<SessionDurationResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<SessionDuration> query = _dbContext.SessionDurations
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId && !s.IsVoided);

            if (doctorId.HasValue && doctorId.Value != Guid.Empty)
            {
                query = query.Where(s => s.DoctorId == doctorId.Value);
            }

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                query = query.Where(s => s.CategoryId == categoryId.Value);
            }

            List<SessionDuration> rows = await query
                .OrderByDescending(s => s.OccurredAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<SessionDurationResult>>.Success(rows.Select(MapDuration).ToList());
        });
    }

    public async Task<Result<SessionDurationResult>> SetSessionDurationExcusedAsync(
        Guid userId,
        Guid clinicId,
        Guid visitId,
        SetSessionDurationExcusedInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.manage", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SessionDurationResult>.Failure(gate.Error);
        }

        if (input.IsExcused && string.IsNullOrWhiteSpace(input.ExcuseReason))
        {
            return Result<SessionDurationResult>.Failure(
                Error.Validation("EXCUSE_REASON_REQUIRED", "An excuse reason is required when excluding a session from time stats."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            SessionDuration? row = await _dbContext.SessionDurations
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.VisitId == visitId, cancellationToken);

            if (row == null)
            {
                return Result<SessionDurationResult>.Failure(Error.NotFound("SESSION_DURATION_NOT_FOUND", "Session duration record was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            row.SetExcused(input.IsExcused, input.ExcuseReason, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await RecalculatePairInternalAsync(clinicId, row.DoctorId, row.CategoryId, now, cancellationToken);

            await _auditService.LogAsync(
                "SessionDurationExcusedUpdated",
                "SessionDuration",
                row.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { row.VisitId, row.IsExcused, row.ExcuseReason }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<SessionDurationResult>.Success(MapDuration(row));
        });
    }

    public async Task<Result<IReadOnlyList<CategoryTimeStatResult>>> RecalculateClinicStatsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "time_engine.manage", "time-engine", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<CategoryTimeStatResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            await RecalculateAllPairsInternalAsync(clinicId, now, cancellationToken);

            await _auditService.LogAsync(
                "TimeEngineStatsRecalculated",
                "Clinic",
                clinicId.ToString(),
                null,
                "{}",
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            List<CategoryTimeStat> stats = await _dbContext.CategoryTimeStats
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId && !s.IsVoided)
                .OrderByDescending(s => s.LastUpdatedAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<CategoryTimeStatResult>>.Success(await ProjectStatsAsync(clinicId, stats, cancellationToken));
        });
    }

    public async Task ProcessCompletedSessionAsync(
        Guid clinicId,
        Guid visitId,
        Guid doctorId,
        Guid categoryId,
        int rawMinutes,
        int plannedMinutes,
        bool isExcused,
        string? excuseReason,
        DateTimeOffset occurredAtUtc,
        Guid? operationId,
        CancellationToken cancellationToken = default)
    {
        if (clinicId == Guid.Empty || visitId == Guid.Empty || doctorId == Guid.Empty || categoryId == Guid.Empty)
        {
            return;
        }

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool moduleEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "time-engine" && m.Enabled, cancellationToken);

            if (!moduleEnabled)
            {
                return 0;
            }

            // Strictly idempotent per (ClinicId, VisitId): never duplicate raw duration on replay
            bool exists = await _dbContext.SessionDurations
                .AsNoTracking()
                .AnyAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.VisitId == visitId, cancellationToken);

            if (exists)
            {
                return 0;
            }

            DateTimeOffset now = _clock.UtcNow;
            var duration = new SessionDuration(
                GuidV7.NewGuid(),
                clinicId,
                visitId,
                doctorId,
                categoryId,
                Math.Max(1, rawMinutes),
                Math.Max(5, plannedMinutes),
                isExcused,
                excuseReason,
                occurredAtUtc,
                operationId,
                now);

            _dbContext.SessionDurations.Add(duration);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await RecalculatePairInternalAsync(clinicId, doctorId, categoryId, now, cancellationToken, allowStepAdvance: !isExcused);
            return 0;
        });
    }

    public async Task<Result<ClinicTimeEngineInspectResult>> InspectClinicTimeEngineAsync(
        Guid superAdminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        Clinic? clinicExists = await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () => await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken),
            "InspectTimeEngine",
            $"SuperAdmin:{superAdminId}",
            cancellationToken);

        if (clinicExists == null)
        {
            return Result<ClinicTimeEngineInspectResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
        }

        return await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                ClinicTimeEngineSetting settings = await _dbContext.ClinicTimeEngineSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken)
                    ?? new ClinicTimeEngineSetting(Guid.Empty, clinicId, _clock.UtcNow);

                List<ClinicDoctor> doctors = await _dbContext.ClinicDoctors
                    .AsNoTracking()
                    .Where(d => d.ClinicId == clinicId && !d.IsVoided)
                    .OrderBy(d => d.FullName)
                    .Take(200)
                    .ToListAsync(cancellationToken);

                Dictionary<Guid, DoctorTimeEngineSetting> savedDocSettings = (await _dbContext.DoctorTimeEngineSettings
                    .AsNoTracking()
                    .Where(s => s.ClinicId == clinicId && !s.IsVoided)
                    .ToListAsync(cancellationToken))
                    .ToDictionary(s => s.DoctorId);

                List<DoctorTimeEngineSettingResult> docResults = doctors
                    .Select(d => savedDocSettings.TryGetValue(d.Id, out DoctorTimeEngineSetting? s)
                        ? new DoctorTimeEngineSettingResult(clinicId, d.Id, d.FullName, s.IsEngineEnabled, s.UpdatedAtUtc)
                        : new DoctorTimeEngineSettingResult(clinicId, d.Id, d.FullName, true, _clock.UtcNow))
                    .ToList();

                List<CategoryTimeStat> stats = await _dbContext.CategoryTimeStats
                    .AsNoTracking()
                    .Where(s => s.ClinicId == clinicId && !s.IsVoided)
                    .OrderByDescending(s => s.LastUpdatedAtUtc)
                    .Take(200)
                    .ToListAsync(cancellationToken);

                List<SessionDuration> durations = await _dbContext.SessionDurations
                    .AsNoTracking()
                    .Where(d => d.ClinicId == clinicId && !d.IsVoided)
                    .OrderByDescending(d => d.OccurredAtUtc)
                    .Take(50)
                    .ToListAsync(cancellationToken);

                return Result<ClinicTimeEngineInspectResult>.Success(new ClinicTimeEngineInspectResult(
                    clinicId,
                    MapClinicSettings(settings),
                    docResults,
                    await ProjectStatsAsync(clinicId, stats, cancellationToken),
                    durations.Select(MapDuration).ToList()));
            },
            "InspectTimeEngine",
            $"SuperAdmin:{superAdminId}",
            cancellationToken);
    }

    private async Task RecalculateAllPairsInternalAsync(Guid clinicId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var durationPairs = await _dbContext.SessionDurations
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && !d.IsVoided)
            .Select(d => new { d.DoctorId, d.CategoryId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var statPairs = await _dbContext.CategoryTimeStats
            .AsNoTracking()
            .Where(s => s.ClinicId == clinicId && !s.IsVoided)
            .Select(s => new { s.DoctorId, s.CategoryId })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var pair in durationPairs.Union(statPairs))
        {
            await RecalculatePairInternalAsync(clinicId, pair.DoctorId, pair.CategoryId, nowUtc, cancellationToken);
        }
    }

    private async Task RecalculatePairInternalAsync(
        Guid clinicId,
        Guid doctorId,
        Guid categoryId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken,
        bool allowStepAdvance = true)
    {
        AppointmentCategory? category = await _dbContext.AppointmentCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClinicId == clinicId && !c.IsVoided && c.Id == categoryId, cancellationToken);

        if (category == null)
        {
            return;
        }

        ClinicTimeEngineSetting clinicSettings = await GetOrBuildClinicSettingAsync(clinicId, nowUtc, cancellationToken);

        DoctorTimeEngineSetting? docSetting = await _dbContext.DoctorTimeEngineSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.DoctorId == doctorId, cancellationToken);
        bool isDoctorEnabled = docSetting?.IsEngineEnabled ?? true;

        List<SessionDuration> durations = await _dbContext.SessionDurations
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && !d.IsVoided && d.DoctorId == doctorId && d.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        CategoryTimeStat? stat = await _dbContext.CategoryTimeStats
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.DoctorId == doctorId && s.CategoryId == categoryId, cancellationToken);

        bool statExisted = stat != null;
        if (stat == null)
        {
            stat = new CategoryTimeStat(
                GuidV7.NewGuid(),
                clinicId,
                doctorId,
                categoryId,
                category.DefaultDurationMinutes,
                isDoctorEnabled,
                nowUtc);
            _dbContext.CategoryTimeStats.Add(stat);
        }

        int currentBaseline = stat.SuggestedDurationMinutes >= 5 ? stat.SuggestedDurationMinutes : category.DefaultDurationMinutes;

        TimeEngineCalculationResult calc;
        if (!allowStepAdvance && statExisted)
        {
            TimeEngineCalculationResult rawCalc = TimeEngineCalculator.Calculate(new TimeEngineCalculationInput(
                category.DefaultDurationMinutes,
                currentBaseline,
                clinicSettings.MinSampleSize,
                clinicSettings.TrimFraction,
                clinicSettings.MaxStepChangePercent,
                isDoctorEnabled,
                durations.Select(d => new TimeEngineSample(d.RawMinutes, d.IsExcused)).ToList()));

            int keptSuggested = currentBaseline;
            int keptApplied = isDoctorEnabled ? keptSuggested : category.DefaultDurationMinutes;
            calc = new TimeEngineCalculationResult(
                rawCalc.SampleCount,
                rawCalc.ExcusedCount,
                rawCalc.TrimmedAverageMinutes,
                keptSuggested,
                keptApplied,
                isDoctorEnabled && keptApplied != category.DefaultDurationMinutes);
        }
        else
        {
            calc = TimeEngineCalculator.Calculate(new TimeEngineCalculationInput(
                category.DefaultDurationMinutes,
                currentBaseline,
                clinicSettings.MinSampleSize,
                clinicSettings.TrimFraction,
                clinicSettings.MaxStepChangePercent,
                isDoctorEnabled,
                durations.Select(d => new TimeEngineSample(d.RawMinutes, d.IsExcused)).ToList()));
        }

        stat.UpdateStats(calc, category.DefaultDurationMinutes, isDoctorEnabled, nowUtc);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ClinicTimeEngineSetting> GetOrBuildClinicSettingAsync(
        Guid clinicId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ClinicTimeEngineSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken)
            ?? new ClinicTimeEngineSetting(Guid.Empty, clinicId, nowUtc);
    }

    private async Task<IReadOnlyList<CategoryTimeStatResult>> ProjectStatsAsync(
        Guid clinicId,
        List<CategoryTimeStat> stats,
        CancellationToken cancellationToken)
    {
        if (stats.Count == 0)
        {
            return Array.Empty<CategoryTimeStatResult>();
        }

        Dictionary<Guid, ClinicDoctor> doctorMap = await _dbContext.ClinicDoctors
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && !d.IsVoided)
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        Dictionary<Guid, AppointmentCategory> catMap = await _dbContext.AppointmentCategories
            .AsNoTracking()
            .Where(c => c.ClinicId == clinicId && !c.IsVoided)
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return stats.Select(s =>
        {
            doctorMap.TryGetValue(s.DoctorId, out ClinicDoctor? doc);
            catMap.TryGetValue(s.CategoryId, out AppointmentCategory? cat);
            return new CategoryTimeStatResult(
                s.Id,
                s.ClinicId,
                s.DoctorId,
                doc?.FullName ?? string.Empty,
                s.CategoryId,
                cat?.NameAr ?? string.Empty,
                cat?.NameEn ?? string.Empty,
                s.SampleCount,
                s.ExcusedCount,
                s.TrimmedAverageMinutes,
                s.SuggestedDurationMinutes,
                s.AppliedDurationMinutes,
                s.DefaultDurationMinutes,
                s.IsDoctorEngineEnabled,
                s.IsAdopted,
                s.LastUpdatedAtUtc);
        }).ToList();
    }

    private static ClinicTimeEngineSettingResult MapClinicSettings(ClinicTimeEngineSetting s) =>
        new(s.ClinicId, s.MinSampleSize, s.TrimFraction, s.MaxStepChangePercent, s.UpdatedAtUtc);

    private static SessionDurationResult MapDuration(SessionDuration d) =>
        new(d.Id, d.ClinicId, d.VisitId, d.DoctorId, d.CategoryId, d.RawMinutes, d.PlannedMinutes, d.IsExcused, d.ExcuseReason, d.OccurredAtUtc);

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
}
