using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Application service orchestrating Phase 15 offline sync: batch push idempotency,
/// conflict classification &amp; resolution, incremental cursor pull, pre-allocated number blocks,
/// clock-skew logging, and Super Admin read-only inspection.
/// </summary>
public sealed class SyncService : ISyncService
{
    private const string ModuleKey = "offline-sync";
    private const string InvalidPayloadError = "INVALID_PAYLOAD_JSON";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IAuditService _auditService;
    private readonly IPatientService _patientService;
    private readonly ISchedulingService _schedulingService;
    private readonly IClinicalService _clinicalService;
    private readonly IBillingService _billingService;
    private readonly IClinicManagementService _clinicService;
    private readonly IClock _clock;

    public SyncService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IPlatformAccess platformAccess,
        IAuditService auditService,
        IPatientService patientService,
        ISchedulingService schedulingService,
        IClinicalService clinicalService,
        IBillingService billingService,
        IClinicManagementService clinicService,
        IClock clock)
    {
        _dbContext = dbContext;
        _currentClinicContext = currentClinicContext;
        _authorizationService = authorizationService;
        _platformAccess = platformAccess;
        _auditService = auditService;
        _patientService = patientService;
        _schedulingService = schedulingService;
        _clinicalService = clinicalService;
        _billingService = billingService;
        _clinicService = clinicService;
        _clock = clock;
    }

    public async Task<Result<ClinicSyncSettingResult>> GetSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ClinicSyncSettingResult>.Failure(access.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicSyncSetting setting = await GetOrCreateSettingAsync(clinicId, _clock.UtcNow, cancellationToken);
            return Result<ClinicSyncSettingResult>.Success(MapSetting(setting));
        });
    }

    public async Task<Result<ClinicSyncSettingResult>> UpdateSettingsAsync(
        Guid userId,
        Guid clinicId,
        UpdateClinicSyncSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.settings", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ClinicSyncSettingResult>.Failure(access.Error);
        }

        if (input.OfflineSessionMaxHours < 1 || input.OfflineSessionMaxHours > 720 ||
            input.ReceiptBlockSize < 10 || input.ReceiptBlockSize > 5000 ||
            input.ReceiptBlockLowWaterMark < 1 || input.ReceiptBlockLowWaterMark >= input.ReceiptBlockSize ||
            input.ClockSkewWarningSeconds < 30 || input.ClockSkewWarningSeconds > 86400)
        {
            return Result<ClinicSyncSettingResult>.Failure(
                Error.Validation("INVALID_SYNC_SETTINGS", "Sync setting bounds are out of range."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicSyncSetting setting = await GetOrCreateSettingAsync(clinicId, now, cancellationToken);
            string oldJson = JsonSerializer.Serialize(MapSetting(setting));

            setting.Update(
                input.OfflineSessionMaxHours,
                input.ReceiptBlockSize,
                input.ReceiptBlockLowWaterMark,
                input.ClockSkewWarningSeconds,
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "SyncSettingsUpdated",
                nameof(ClinicSyncSetting),
                setting.Id.ToString(),
                oldJson,
                JsonSerializer.Serialize(MapSetting(setting)),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ClinicSyncSettingResult>.Success(MapSetting(setting));
        });
    }

    public async Task<Result<AllocatedNumberBlockResult>> AllocateNumberBlockAsync(
        Guid userId,
        Guid clinicId,
        AllocateNumberBlockInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        string normalizedDevice = (input.DeviceId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedDevice))
        {
            return Result<AllocatedNumberBlockResult>.Failure(
                Error.Validation("DEVICE_ID_REQUIRED", "DeviceId is required to allocate an offline number block."));
        }

        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, normalizedDevice, cancellationToken);
        if (access.IsFailure)
        {
            return Result<AllocatedNumberBlockResult>.Failure(access.Error);
        }

        var deviceCheck = await EnsureActiveDeviceAsync(userId, clinicId, normalizedDevice, cancellationToken);
        if (deviceCheck.IsFailure)
        {
            return Result<AllocatedNumberBlockResult>.Failure(deviceCheck.Error);
        }

        string kind = string.Equals(input.Kind?.Trim(), "PatientFile", StringComparison.OrdinalIgnoreCase)
            ? "PatientFile"
            : string.Equals(input.Kind?.Trim(), "Receipt", StringComparison.OrdinalIgnoreCase)
                ? "Receipt"
                : string.Empty;

        if (string.IsNullOrEmpty(kind))
        {
            return Result<AllocatedNumberBlockResult>.Failure(
                Error.Validation("INVALID_BLOCK_KIND", "Number block Kind must be 'Receipt' or 'PatientFile'."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicSyncSetting setting = await GetOrCreateSettingAsync(clinicId, now, cancellationToken);
            int size = input.RequestedSize is >= 10 and <= 5000
                ? input.RequestedSize.Value
                : setting.ReceiptBlockSize;

            long? maxTo = await _dbContext.NumberBlocks
                .Where(b => b.ClinicId == clinicId && b.Kind == kind)
                .MaxAsync(b => (long?)b.ToNumber, cancellationToken);

            long from = (maxTo ?? 0L) + 1L;
            long to = from + size - 1L;

            var block = new NumberBlock(GuidV7.NewGuid(), clinicId, normalizedDevice, kind, from, to, now);
            _dbContext.NumberBlocks.Add(block);

            SyncDeviceState deviceState = await GetOrCreateDeviceStateAsync(clinicId, normalizedDevice, userId, now, cancellationToken);
            deviceState.RefreshOnlineAuth(now);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "SyncNumberBlockAllocated",
                nameof(NumberBlock),
                block.Id.ToString(),
                null,
                $"{{\"deviceId\":\"{normalizedDevice}\",\"kind\":\"{kind}\",\"from\":{from.ToString(CultureInfo.InvariantCulture)},\"to\":{to.ToString(CultureInfo.InvariantCulture)}}}",
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<AllocatedNumberBlockResult>.Success(MapNumberBlock(block));
        });
    }

    public async Task<Result<IReadOnlyList<AllocatedNumberBlockResult>>> ListNumberBlocksAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<AllocatedNumberBlockResult>>.Failure(access.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<NumberBlock> query = _dbContext.NumberBlocks
                .AsNoTracking()
                .Where(b => b.ClinicId == clinicId);

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                string d = deviceId.Trim();
                query = query.Where(b => b.DeviceId == d);
            }

            List<NumberBlock> list = await query
                .OrderByDescending(b => b.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<AllocatedNumberBlockResult>>.Success(
                list.Select(MapNumberBlock).ToList());
        });
    }

    public async Task<Result<SyncPushBatchResult>> PushAsync(
        Guid userId,
        Guid clinicId,
        SyncPushBatchInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        string normalizedDevice = (input.DeviceId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedDevice))
        {
            return Result<SyncPushBatchResult>.Failure(
                Error.Validation("DEVICE_ID_REQUIRED", "DeviceId is required for sync push."));
        }

        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, normalizedDevice, cancellationToken);
        if (access.IsFailure)
        {
            return Result<SyncPushBatchResult>.Failure(access.Error);
        }

        var deviceCheck = await EnsureActiveDeviceAsync(userId, clinicId, normalizedDevice, cancellationToken);
        if (deviceCheck.IsFailure)
        {
            return Result<SyncPushBatchResult>.Failure(deviceCheck.Error);
        }

        IReadOnlyList<SyncPushOperationInput> ops = input.Operations ?? Array.Empty<SyncPushOperationInput>();
        if (ops.Count > 200)
        {
            return Result<SyncPushBatchResult>.Failure(
                Error.Validation("BATCH_TOO_LARGE", "Sync push batch supports at most 200 operations."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicSyncSetting setting = await GetOrCreateSettingAsync(clinicId, now, cancellationToken);
            await GetOrCreateDeviceStateAsync(clinicId, normalizedDevice, userId, now, cancellationToken);

            long nextCursor = (await _dbContext.SyncOperationLogs
                .Where(l => l.ClinicId == clinicId && l.CursorSequence > 0)
                .MaxAsync(l => (long?)l.CursorSequence, cancellationToken)) ?? 0L;

            List<SyncPushOperationOutcomeResult> outcomes = new();
            int appliedCount = 0;
            int duplicateCount = 0;
            int conflictCount = 0;
            int rejectedCount = 0;

            foreach (SyncPushOperationInput op in ops)
            {
                ClockSkewEvaluation skew = SyncCalculator.EvaluateClockSkew(
                    op.ClientTimestampUtc, now, setting.ClockSkewWarningSeconds);
                ConflictClass conflictClass = SyncCalculator.ClassifyConflictClass(op.EntityType);

                SyncOperationLog? existing = await _dbContext.SyncOperationLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.ClinicId == clinicId && l.OperationId == op.OperationId, cancellationToken);

                if (existing != null)
                {
                    duplicateCount++;
                    outcomes.Add(new SyncPushOperationOutcomeResult(
                        op.OperationId,
                        op.ActionType,
                        op.EntityType,
                        op.EntityId,
                        SyncOperationStatus.DuplicateIgnored,
                        existing.CursorSequence,
                        null,
                        null,
                        null,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew));
                    continue;
                }

                if (skew.IsLargeSkew)
                {
                    await _auditService.LogAsync(
                        "SyncLargeClockSkew",
                        nameof(SyncOperationLog),
                        op.OperationId.ToString(),
                        null,
                        $"{{\"deviceId\":\"{normalizedDevice}\",\"skewSeconds\":{skew.SignedSkewSeconds.ToString(CultureInfo.InvariantCulture)}}}",
                        userId,
                        clinicId,
                        ipAddress,
                        requestId,
                        cancellationToken);
                }

                if (SyncCalculator.IsOnlineOnlyAction(op.ActionType))
                {
                    string reason = $"Action '{op.ActionType}' is online-only and cannot be synced.";
                    _dbContext.SyncOperationLogs.Add(new SyncOperationLog(
                        GuidV7.NewGuid(),
                        clinicId,
                        normalizedDevice,
                        userId,
                        op.OperationId,
                        op.EntityType,
                        op.EntityId,
                        op.ActionType,
                        conflictClass,
                        0L,
                        op.ClientTimestampUtc,
                        now,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew,
                        SyncOperationStatus.Rejected,
                        "ONLINE_ONLY_ACTION_FORBIDDEN",
                        reason,
                        SafePayload(op.PayloadJson)));
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    rejectedCount++;
                    outcomes.Add(new SyncPushOperationOutcomeResult(
                        op.OperationId,
                        op.ActionType,
                        op.EntityType,
                        op.EntityId,
                        SyncOperationStatus.Rejected,
                        null,
                        "ONLINE_ONLY_ACTION_FORBIDDEN",
                        reason,
                        null,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew));
                    continue;
                }

                OperationExecOutcome exec = await ExecuteSingleOperationAsync(
                    userId,
                    clinicId,
                    normalizedDevice,
                    ipAddress,
                    requestId,
                    op,
                    nextCursor,
                    now,
                    cancellationToken);

                if (exec.Conflict != null)
                {
                    _dbContext.SyncConflicts.Add(exec.Conflict);
                    _dbContext.SyncOperationLogs.Add(new SyncOperationLog(
                        GuidV7.NewGuid(),
                        clinicId,
                        normalizedDevice,
                        userId,
                        op.OperationId,
                        op.EntityType,
                        op.EntityId,
                        op.ActionType,
                        conflictClass,
                        0L,
                        op.ClientTimestampUtc,
                        now,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew,
                        SyncOperationStatus.Conflict,
                        exec.Conflict.ConflictCode,
                        exec.Conflict.Reason,
                        SafePayload(op.PayloadJson)));
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    conflictCount++;
                    outcomes.Add(new SyncPushOperationOutcomeResult(
                        op.OperationId,
                        op.ActionType,
                        op.EntityType,
                        op.EntityId,
                        SyncOperationStatus.Conflict,
                        null,
                        exec.Conflict.ConflictCode,
                        exec.Conflict.Reason,
                        exec.Conflict.Id,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew));
                    continue;
                }

                if (exec.Error != null)
                {
                    _dbContext.SyncOperationLogs.Add(new SyncOperationLog(
                        GuidV7.NewGuid(),
                        clinicId,
                        normalizedDevice,
                        userId,
                        op.OperationId,
                        op.EntityType,
                        op.EntityId,
                        op.ActionType,
                        conflictClass,
                        0L,
                        op.ClientTimestampUtc,
                        now,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew,
                        SyncOperationStatus.Rejected,
                        exec.Error.Code,
                        Truncate(exec.Error.Message, 480),
                        SafePayload(op.PayloadJson)));
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    rejectedCount++;
                    outcomes.Add(new SyncPushOperationOutcomeResult(
                        op.OperationId,
                        op.ActionType,
                        op.EntityType,
                        op.EntityId,
                        SyncOperationStatus.Rejected,
                        null,
                        exec.Error.Code,
                        exec.Error.Message,
                        null,
                        skew.SignedSkewSeconds,
                        skew.IsLargeSkew));
                    continue;
                }

                nextCursor++;
                _dbContext.SyncOperationLogs.Add(new SyncOperationLog(
                    GuidV7.NewGuid(),
                    clinicId,
                    normalizedDevice,
                    userId,
                    op.OperationId,
                    op.EntityType,
                    op.EntityId,
                    op.ActionType,
                    conflictClass,
                    nextCursor,
                    op.ClientTimestampUtc,
                    now,
                    skew.SignedSkewSeconds,
                    skew.IsLargeSkew,
                    SyncOperationStatus.Applied,
                    null,
                    null,
                    SafePayload(op.PayloadJson)));
                await _dbContext.SaveChangesAsync(cancellationToken);

                appliedCount++;
                outcomes.Add(new SyncPushOperationOutcomeResult(
                    op.OperationId,
                    op.ActionType,
                    op.EntityType,
                    op.EntityId,
                    SyncOperationStatus.Applied,
                    nextCursor,
                    null,
                    null,
                    null,
                    skew.SignedSkewSeconds,
                    skew.IsLargeSkew,
                    exec.AssignedReceipts));
            }

            SyncDeviceState trackedDevice = await GetOrCreateDeviceStateAsync(clinicId, normalizedDevice, userId, now, cancellationToken);
            long maxSkew = outcomes.Count == 0
                ? 0L
                : outcomes.Max(o => Math.Abs(o.ClockSkewSeconds));
            trackedDevice.RecordPush(nextCursor, maxSkew, 0, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<SyncPushBatchResult>.Success(new SyncPushBatchResult(
                normalizedDevice,
                nextCursor,
                now,
                appliedCount,
                duplicateCount,
                conflictCount,
                rejectedCount,
                outcomes));
        });
    }

    public async Task<Result<SyncPullResponseResult>> PullAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        long cursor,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<SyncPullResponseResult>.Failure(access.Error);
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var deviceCheck = await EnsureActiveDeviceAsync(userId, clinicId, deviceId.Trim(), cancellationToken);
            if (deviceCheck.IsFailure)
            {
                return Result<SyncPullResponseResult>.Failure(deviceCheck.Error);
            }
        }

        int boundedLimit = Math.Clamp(limit, 1, 200);
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;

            List<SyncOperationLog> raw = await _dbContext.SyncOperationLogs
                .AsNoTracking()
                .Where(l => l.ClinicId == clinicId && l.Status == SyncOperationStatus.Applied && l.CursorSequence > cursor)
                .OrderBy(l => l.CursorSequence)
                .Take(boundedLimit + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = raw.Count > boundedLimit;
            List<SyncOperationLog> slice = hasMore ? raw.Take(boundedLimit).ToList() : raw;
            long nextCursor = slice.Count > 0 ? slice[^1].CursorSequence : cursor;

            int openConflicts = await _dbContext.SyncConflicts
                .AsNoTracking()
                .CountAsync(c => c.ClinicId == clinicId && c.Status == SyncConflictStatus.Open, cancellationToken);

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                SyncDeviceState deviceState = await GetOrCreateDeviceStateAsync(
                    clinicId, deviceId.Trim(), userId, now, cancellationToken);
                deviceState.RecordPull(nextCursor, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            List<SyncPullItemResult> items = slice
                .Select(l => new SyncPullItemResult(
                    l.CursorSequence,
                    l.OperationId,
                    l.DeviceId,
                    l.ActionType,
                    l.EntityType,
                    l.EntityId,
                    l.PayloadJson ?? "{}",
                    l.ServerReceivedAtUtc))
                .ToList();

            return Result<SyncPullResponseResult>.Success(new SyncPullResponseResult(
                cursor,
                nextCursor,
                hasMore,
                now,
                items,
                openConflicts));
        });
    }

    public async Task<Result<IReadOnlyList<SyncConflictResult>>> ListConflictsAsync(
        Guid userId,
        Guid clinicId,
        SyncConflictStatus? status,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<SyncConflictResult>>.Failure(access.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<SyncConflict> query = _dbContext.SyncConflicts
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId);

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            List<SyncConflict> rows = await query
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<SyncConflictResult>>.Success(
                rows.Select(MapConflict).ToList());
        });
    }

    public async Task<Result<SyncConflictResult>> ResolveConflictAsync(
        Guid userId,
        Guid clinicId,
        Guid conflictId,
        ResolveSyncConflictInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.resolve", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<SyncConflictResult>.Failure(access.Error);
        }

        if (input.Resolution == SyncConflictStatus.Open)
        {
            return Result<SyncConflictResult>.Failure(
                Error.Validation("INVALID_RESOLUTION_STATUS", "Resolution cannot be set back to Open."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            SyncConflict? conflict = await _dbContext.SyncConflicts
                .FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.Id == conflictId, cancellationToken);

            if (conflict == null)
            {
                return Result<SyncConflictResult>.Failure(
                    Error.NotFound("CONFLICT_NOT_FOUND", "Sync conflict was not found."));
            }

            string previousStatus = conflict.Status.ToString();
            DateTimeOffset now = _clock.UtcNow;
            conflict.Resolve(input.Resolution, input.ResolutionNote, userId, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "SyncConflictResolved",
                nameof(SyncConflict),
                conflict.Id.ToString(),
                previousStatus,
                conflict.Status.ToString(),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<SyncConflictResult>.Success(MapConflict(conflict));
        });
    }

    public async Task<Result<IReadOnlyList<SyncDeviceStateResult>>> ListDevicesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId, clinicId, "offline_sync.use", ModuleKey, deviceId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<SyncDeviceStateResult>>.Failure(access.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<SyncDeviceState> states = await _dbContext.SyncDeviceStates
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId)
                .OrderByDescending(s => s.LastOnlineAuthAtUtc)
                .ToListAsync(cancellationToken);

            List<AppDevice> rawAppDevices = await _dbContext.AppDevices
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            Dictionary<string, AppDevice> appDevices = rawAppDevices
                .GroupBy(d => d.DeviceId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsApproved).First(), StringComparer.Ordinal);

            List<SyncDeviceStateResult> results = states
                .Select(s =>
                {
                    appDevices.TryGetValue(s.DeviceId, out AppDevice? appDev);
                    return new SyncDeviceStateResult(
                        s.Id,
                        s.ClinicId,
                        s.DeviceId,
                        s.UserId,
                        s.LastSyncCursor,
                        s.LastPushAtUtc,
                        s.LastPullAtUtc,
                        s.LastOnlineAuthAtUtc,
                        s.MaxClockSkewSecondsObserved,
                        s.PendingOperationsReported,
                        appDev?.IsApproved ?? false,
                        appDev?.IsRevoked ?? false);
                })
                .ToList();

            return Result<IReadOnlyList<SyncDeviceStateResult>>.Success(results);
        });
    }

    public async Task<Result<ClinicOfflineSyncInspectResult>> InspectClinicOfflineSyncAsync(
        Guid adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        ClinicOfflineSyncInspectResult? inspect = await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                bool clinicExists = await _dbContext.Clinics
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == clinicId, cancellationToken);
                if (!clinicExists)
                {
                    return null;
                }

                ClinicSyncSetting? setting = await _dbContext.ClinicSyncSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ClinicId == clinicId, cancellationToken);

                List<SyncDeviceState> states = await _dbContext.SyncDeviceStates
                    .AsNoTracking()
                    .Where(s => s.ClinicId == clinicId)
                    .ToListAsync(cancellationToken);

                List<AppDevice> rawAppDevices = await _dbContext.AppDevices
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                Dictionary<string, AppDevice> appDevices = rawAppDevices
                    .GroupBy(d => d.DeviceId, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsApproved).First(), StringComparer.Ordinal);

                List<SyncConflict> conflicts = await _dbContext.SyncConflicts
                    .AsNoTracking()
                    .Where(c => c.ClinicId == clinicId)
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .Take(50)
                    .ToListAsync(cancellationToken);

                List<NumberBlock> blocks = await _dbContext.NumberBlocks
                    .AsNoTracking()
                    .Where(b => b.ClinicId == clinicId)
                    .OrderByDescending(b => b.CreatedAtUtc)
                    .Take(50)
                    .ToListAsync(cancellationToken);

                _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                    GuidV7.NewGuid(),
                    adminId.ToString(),
                    "SuperAdminReadOnlyInspect",
                    _clock.UtcNow,
                    clinicId,
                    "Inspected offline-sync settings, devices, conflicts, and number blocks.",
                    "SuperAdmin"));

                await _dbContext.SaveChangesAsync(cancellationToken);

                ClinicSyncSettingResult settingResult = setting == null
                    ? new ClinicSyncSettingResult(clinicId, 24, 50, 10, 300, _clock.UtcNow)
                    : MapSetting(setting);

                return new ClinicOfflineSyncInspectResult(
                    clinicId,
                    settingResult,
                    states.Select(s =>
                    {
                        appDevices.TryGetValue(s.DeviceId, out AppDevice? appDev);
                        return new SyncDeviceStateResult(
                            s.Id,
                            s.ClinicId,
                            s.DeviceId,
                            s.UserId,
                            s.LastSyncCursor,
                            s.LastPushAtUtc,
                            s.LastPullAtUtc,
                            s.LastOnlineAuthAtUtc,
                            s.MaxClockSkewSecondsObserved,
                            s.PendingOperationsReported,
                            appDev?.IsApproved ?? false,
                            appDev?.IsRevoked ?? false);
                    }).ToList(),
                    conflicts.Select(MapConflict).ToList(),
                    blocks.Select(MapNumberBlock).ToList());
            },
            adminId.ToString(),
            "SuperAdminReadOnlyInspect",
            cancellationToken);

        return inspect == null
            ? Result<ClinicOfflineSyncInspectResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."))
            : Result<ClinicOfflineSyncInspectResult>.Success(inspect);
    }

    private async Task<OperationExecOutcome> ExecuteSingleOperationAsync(
        Guid userId,
        Guid clinicId,
        string deviceId,
        string? ipAddress,
        string? requestId,
        SyncPushOperationInput op,
        long currentCursor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        JsonDocument? doc = null;
        try
        {
            string payload = string.IsNullOrWhiteSpace(op.PayloadJson) ? "{}" : op.PayloadJson;
            doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;
            string action = (op.ActionType ?? string.Empty).Trim();

            if (Matches(action, "RegisterPatient", "patients.create"))
            {
                var input = new CreatePatientInput(
                    ClientPatientId: TryGetGuid(root, "clientPatientId") ?? op.EntityId,
                    FullName: GetString(root, "fullName") ?? "Patient",
                    Phone: GetString(root, "phone") ?? string.Empty,
                    SecondaryPhone: GetString(root, "secondaryPhone"),
                    DateOfBirth: null,
                    ApproximateAgeYears: TryGetInt(root, "approximateAgeYears"),
                    Gender: GetString(root, "gender") ?? "Unknown",
                    NationalId: GetString(root, "nationalId"),
                    Address: GetString(root, "address"),
                    AssignedDoctorId: TryGetGuid(root, "assignedDoctorId"),
                    PrimarySpecialtyCode: GetString(root, "primarySpecialtyCode"),
                    ConfirmDuplicateCreation: TryGetBool(root, "confirmDuplicateCreation") ?? true,
                    OperationId: op.OperationId);

                var res = await _patientService.CreatePatientAsync(userId, clinicId, input, deviceId, ipAddress, requestId, cancellationToken);
                return FromResult(res);
            }

            if (Matches(action, "BookAppointment", "scheduling.book"))
            {
                Guid? roomId = TryGetGuid(root, "roomId");
                DateTimeOffset startUtc = TryGetDateTimeOffset(root, "startUtc") ?? now;
                var input = new BookAppointmentInput(
                    Id: TryGetGuid(root, "id") ?? op.EntityId,
                    PatientId: TryGetGuid(root, "patientId") ?? Guid.Empty,
                    DoctorId: TryGetGuid(root, "doctorId") ?? Guid.Empty,
                    RoomId: roomId,
                    CategoryId: TryGetGuid(root, "categoryId") ?? Guid.Empty,
                    StartUtc: startUtc,
                    EndUtc: TryGetDateTimeOffset(root, "endUtc"),
                    Priority: GetString(root, "priority") ?? "Normal",
                    PlanSessionId: TryGetGuid(root, "planSessionId"),
                    Notes: GetString(root, "notes"),
                    OperationId: op.OperationId);

                var res = await _schedulingService.BookAppointmentAsync(userId, clinicId, input, deviceId, ipAddress, requestId, cancellationToken);
                if (res.IsFailure && (res.Error.Code == "APPOINTMENT_CONFLICT" || res.Error.Code == "SLOT_TAKEN"))
                {
                    if (_dbContext.ChangeTracker.HasChanges())
                    {
                        _dbContext.ChangeTracker.Clear();
                    }

                    string serverSnap = $"{{\"doctorId\":\"{input.DoctorId}\",\"startUtc\":\"{startUtc.ToString("O", CultureInfo.InvariantCulture)}\",\"errorCode\":\"{res.Error.Code}\"}}";
                    var conflict = new SyncConflict(
                        GuidV7.NewGuid(),
                        clinicId,
                        deviceId,
                        op.OperationId,
                        "Appointment",
                        op.EntityId,
                        SyncCalculator.ClassifyConflictClass("Appointment"),
                        res.Error.Code,
                        res.Error.Message,
                        SafePayload(op.PayloadJson),
                        serverSnap,
                        now);

                    return new OperationExecOutcome(null, conflict, null);
                }

                return FromResult(res);
            }

            if (Matches(action, "QueueChange", "scheduling.queue", "CheckIn"))
            {
                Guid? apptId = TryGetGuid(root, "appointmentId");
                string? nextStatus = GetString(root, "nextStatus");
                if (apptId.HasValue && !string.IsNullOrWhiteSpace(nextStatus))
                {
                    var res = await _schedulingService.TransitionAppointmentStatusAsync(
                        userId, clinicId, apptId.Value, nextStatus!, GetString(root, "reason"), deviceId, ipAddress, requestId, cancellationToken);
                    return FromResult(res);
                }

                var walkIn = await _schedulingService.EnqueueWalkInAsync(
                    userId,
                    clinicId,
                    TryGetGuid(root, "patientId") ?? Guid.Empty,
                    TryGetGuid(root, "doctorId") ?? Guid.Empty,
                    GetString(root, "priority"),
                    op.OperationId,
                    deviceId,
                    ipAddress,
                    requestId,
                    cancellationToken);
                return FromResult(walkIn);
            }

            if (Matches(action, "StartVisit", "clinical.start-visit"))
            {
                var input = new StartVisitInput(
                    ClientVisitId: TryGetGuid(root, "clientVisitId") ?? op.EntityId,
                    AppointmentId: TryGetGuid(root, "appointmentId"),
                    PatientId: TryGetGuid(root, "patientId"),
                    DoctorId: TryGetGuid(root, "doctorId"),
                    SpecialtyCode: GetString(root, "specialtyCode"),
                    RoomId: TryGetGuid(root, "roomId"),
                    PlannedDurationMinutes: TryGetInt(root, "plannedDurationMinutes"),
                    IsVisibleToPatient: TryGetBool(root, "isVisibleToPatient") ?? false,
                    OperationId: op.OperationId,
                    CategoryId: TryGetGuid(root, "categoryId"));

                var res = await _clinicalService.StartOrResumeVisitAsync(userId, clinicId, input, deviceId, ipAddress, requestId, cancellationToken);
                return FromResult(res);
            }

            if (Matches(action, "CompleteVisit", "clinical.complete-visit"))
            {
                Guid visitId = TryGetGuid(root, "visitId") ?? op.EntityId;
                IReadOnlyList<VisitToolUsageInput>? tools = root.TryGetProperty("toolsUsed", out JsonElement toolsEl) && toolsEl.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<List<VisitToolUsageInput>>(toolsEl.GetRawText(), JsonOptions)
                    : null;

                var input = new CompleteVisitInput(
                    IsOvertimeExcused: TryGetBool(root, "isOvertimeExcused") ?? false,
                    OvertimeExcuseReason: GetString(root, "overtimeExcuseReason"),
                    OperationId: op.OperationId,
                    ToolsUsed: tools);

                var res = await _clinicalService.CompleteVisitAsync(userId, clinicId, visitId, input, deviceId, ipAddress, requestId, cancellationToken);
                return FromResult(res);
            }

            if (Matches(action, "CreateCharge", "billing.create-charge"))
            {
                var input = JsonSerializer.Deserialize<CreateChargeInput>(payload, JsonOptions);
                if (input == null)
                {
                    return new OperationExecOutcome(Error.Validation(InvalidPayloadError, "Invalid charge payload."), null, null);
                }

                CreateChargeInput effective = input with
                {
                    Id = input.Id ?? op.EntityId,
                    OperationId = op.OperationId
                };
                var res = await _billingService.CreateChargeAsync(userId, clinicId, effective, deviceId, ipAddress, requestId, cancellationToken);
                return FromResult(res);
            }

            if (Matches(action, "RecordPayment", "billing.record-payment"))
            {
                var input = JsonSerializer.Deserialize<RecordPaymentInput>(payload, JsonOptions);
                if (input == null)
                {
                    return new OperationExecOutcome(Error.Validation(InvalidPayloadError, "Invalid payment payload."), null, null);
                }

                RecordPaymentInput effective = input with
                {
                    Id = input.Id ?? op.EntityId,
                    OperationId = op.OperationId
                };
                var res = await _billingService.RecordPaymentAsync(userId, clinicId, effective, deviceId, ipAddress, requestId, cancellationToken);
                if (res.IsFailure)
                {
                    if (_dbContext.ChangeTracker.HasChanges())
                    {
                        _dbContext.ChangeTracker.Clear();
                    }

                    return new OperationExecOutcome(res.Error, null, null);
                }

                IReadOnlyList<string> receipts = res.Value
                    .Select(p => p.ReceiptNumber)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList();
                return new OperationExecOutcome(null, null, receipts);
            }

            if (Matches(action, "ClinicSettingsUpdate", "core.settings-update"))
            {
                var current = await _clinicService.GetClinicSettingsAsync(userId, clinicId, cancellationToken);
                if (current.IsFailure)
                {
                    return new OperationExecOutcome(current.Error, null, null);
                }

                string oldSettingsJson = JsonSerializer.Serialize(current.Value);
                if (op.ClientBaseCursor.HasValue && op.ClientBaseCursor.Value < currentCursor)
                {
                    await _auditService.LogAsync(
                        "SyncLastWriterWinsOverwrite",
                        nameof(Clinic),
                        clinicId.ToString(),
                        oldSettingsJson,
                        SafePayload(op.PayloadJson),
                        userId,
                        clinicId,
                        ipAddress,
                        requestId,
                        cancellationToken);
                }

                var updated = await _clinicService.UpdateClinicSettingsAsync(
                    userId,
                    clinicId,
                    GetString(root, "name") ?? current.Value.Name,
                    GetString(root, "street") ?? current.Value.Street ?? string.Empty,
                    GetString(root, "city") ?? current.Value.City ?? string.Empty,
                    GetString(root, "governorate") ?? current.Value.Governorate ?? string.Empty,
                    GetString(root, "mainPhone") ?? current.Value.MainPhone ?? string.Empty,
                    current.Value.ExtraPhones,
                    GetString(root, "contactEmail") ?? current.Value.ContactEmail,
                    GetString(root, "slug") ?? current.Value.Slug,
                    GetString(root, "timeZone") ?? current.Value.TimeZone,
                    GetString(root, "currency") ?? current.Value.Currency,
                    GetString(root, "roundingRule") ?? current.Value.RoundingRule,
                    current.Value.LogoAssetId,
                    ipAddress,
                    requestId,
                    cancellationToken);

                return FromResult(updated);
            }

            return new OperationExecOutcome(
                Error.Validation("UNSUPPORTED_SYNC_ACTION", $"Action '{op.ActionType}' is not supported by offline sync."),
                null,
                null);
        }
        catch (JsonException)
        {
            if (_dbContext.ChangeTracker.HasChanges())
            {
                _dbContext.ChangeTracker.Clear();
            }

            return new OperationExecOutcome(Error.Validation(InvalidPayloadError, "PayloadJson is not valid JSON."), null, null);
        }
        finally
        {
            doc?.Dispose();
        }
    }

    private OperationExecOutcome FromResult<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            if (_dbContext.ChangeTracker.HasChanges())
            {
                _dbContext.ChangeTracker.Clear();
            }

            return new OperationExecOutcome(result.Error, null, null);
        }

        return new OperationExecOutcome(null, null, null);
    }

    private async Task<Result> EnsureActiveDeviceAsync(
        Guid userId,
        Guid clinicId,
        string deviceId,
        CancellationToken cancellationToken)
    {
        AppDevice? device = await _dbContext.AppDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId, cancellationToken);

        if (device != null && device.IsRevoked)
        {
            return Result.Failure(Error.Unauthorized("DEVICE_REVOKED", "This device has been revoked. Sign in again online."));
        }

        bool userInClinic = await _dbContext.UserClinics
            .AsNoTracking()
            .AnyAsync(uc => uc.ClinicId == clinicId && uc.UserId == userId, cancellationToken);

        return userInClinic
            ? Result.Success()
            : Result.Failure(Error.NotFound("CLINIC_ACCESS_DENIED", "Clinic access denied."));
    }

    private async Task<ClinicSyncSetting> GetOrCreateSettingAsync(
        Guid clinicId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ClinicSyncSetting? setting = await _dbContext.ClinicSyncSettings
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId, cancellationToken);
        if (setting != null)
        {
            return setting;
        }

        setting = new ClinicSyncSetting(GuidV7.NewGuid(), clinicId, 24, 50, 10, 300, now);
        _dbContext.ClinicSyncSettings.Add(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private async Task<SyncDeviceState> GetOrCreateDeviceStateAsync(
        Guid clinicId,
        string deviceId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SyncDeviceState? state = await _dbContext.SyncDeviceStates
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && s.DeviceId == deviceId, cancellationToken);
        if (state != null)
        {
            return state;
        }

        state = new SyncDeviceState(GuidV7.NewGuid(), clinicId, deviceId, userId, now);
        _dbContext.SyncDeviceStates.Add(state);
        return state;
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

    private static ClinicSyncSettingResult MapSetting(ClinicSyncSetting s) => new(
        s.ClinicId,
        s.OfflineSessionMaxHours,
        s.ReceiptBlockSize,
        s.ReceiptBlockLowWaterMark,
        s.ClockSkewWarningSeconds,
        s.UpdatedAtUtc);

    private static AllocatedNumberBlockResult MapNumberBlock(NumberBlock b) => new(
        b.Id,
        b.ClinicId,
        b.DeviceId,
        b.Kind,
        b.FromNumber,
        b.ToNumber,
        b.NextNumber,
        Math.Max(0, b.ToNumber - b.NextNumber + 1),
        b.Status,
        b.CreatedAtUtc);

    private static SyncConflictResult MapConflict(SyncConflict c) => new(
        c.Id,
        c.ClinicId,
        c.OperationId,
        c.DeviceId,
        c.EntityType,
        c.EntityId,
        c.ConflictClass,
        c.ConflictCode,
        c.Reason,
        c.ClientPayloadJson,
        c.ServerSnapshotJson,
        c.Status,
        c.ResolvedByUserId?.ToString(),
        c.ResolvedAtUtc,
        c.ResolutionNote,
        c.CreatedAtUtc);

    private static bool Matches(string actual, params string[] candidates) =>
        candidates.Any(c => string.Equals(actual, c, StringComparison.OrdinalIgnoreCase));

    private static string SafePayload(string? raw) =>
        Truncate(string.IsNullOrWhiteSpace(raw) ? "{}" : raw, 7900);

    private static string Truncate(string input, int maxLen) =>
        input.Length <= maxLen ? input : input[..maxLen];

    private static string? GetString(JsonElement el, string prop) =>
        TryGetPropertyInsensitive(el, prop, out JsonElement val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;

    private static Guid? TryGetGuid(JsonElement el, string prop) =>
        TryGetPropertyInsensitive(el, prop, out JsonElement val) &&
        val.ValueKind == JsonValueKind.String &&
        Guid.TryParse(val.GetString(), out Guid parsed)
            ? parsed
            : null;

    private static int? TryGetInt(JsonElement el, string prop) =>
        TryGetPropertyInsensitive(el, prop, out JsonElement val) &&
        val.ValueKind == JsonValueKind.Number &&
        val.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private static bool? TryGetBool(JsonElement el, string prop) =>
        TryGetPropertyInsensitive(el, prop, out JsonElement val) &&
        (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False)
            ? val.GetBoolean()
            : null;

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement el, string prop) =>
        TryGetPropertyInsensitive(el, prop, out JsonElement val) &&
        val.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(val.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dto)
            ? dto
            : null;

    private static bool TryGetPropertyInsensitive(JsonElement el, string name, out JsonElement val)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            val = default;
            return false;
        }

        if (el.TryGetProperty(name, out val))
        {
            return true;
        }

        foreach (JsonProperty p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                val = p.Value;
                return true;
            }
        }

        val = default;
        return false;
    }

    private sealed record OperationExecOutcome(
        Error? Error,
        SyncConflict? Conflict,
        IReadOnlyList<string>? AssignedReceipts);
}
