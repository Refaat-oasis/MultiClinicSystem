using System.Data;
using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Contracts;
using Core.Application;
using Core.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Manages clinic scheduling settings, appointment categories, conflict-safe appointment booking,
/// status state machine, rescheduling, day closures, doctor timetables, waiting queue, waiting list,
/// portal booking contract, and Super Admin read-only inspection.
/// </summary>
public sealed class SchedulingService : ISchedulingService, IPortalBooking, ISchedulingPortalQueries
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IAuditService _auditService;
    private readonly IScheduleRealtimeNotifier _realtimeNotifier;
    private readonly IClock _clock;
    private readonly ITimeEngineDurationProvider? _timeEngineDurationProvider;

    public SchedulingService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IPlatformAccess platformAccess,
        IAuditService auditService,
        IScheduleRealtimeNotifier realtimeNotifier,
        IClock clock,
        ITimeEngineDurationProvider? timeEngineDurationProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _platformAccess = platformAccess ?? throw new ArgumentNullException(nameof(platformAccess));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _realtimeNotifier = realtimeNotifier ?? throw new ArgumentNullException(nameof(realtimeNotifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeEngineDurationProvider = timeEngineDurationProvider;
    }

    public async Task<Result<ClinicSchedulingSettingResult>> GetSchedulingSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.read", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicSchedulingSettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicSchedulingSetting s = await SchedulingProjectionHelper.GetOrCreateDefaultSettingsAsync(_dbContext, clinicId, _clock.UtcNow, cancellationToken);
            return Result<ClinicSchedulingSettingResult>.Success(new ClinicSchedulingSettingResult(
                s.VipJumpLimit, s.LateGraceMinutes, s.MaxQueuePushes, s.DefaultSlotStepMinutes, s.MinBookingLeadMinutes, s.RescheduleOptionsJson));
        });
    }

    public async Task<Result<ClinicSchedulingSettingResult>> UpdateSchedulingSettingsAsync(
        Guid userId,
        Guid clinicId,
        int vipJumpLimit,
        int lateGraceMinutes,
        int maxQueuePushes,
        int defaultSlotStepMinutes,
        int minBookingLeadMinutes,
        string? rescheduleOptionsJson,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinic.settings.edit", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicSchedulingSettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicSchedulingSetting s = await SchedulingProjectionHelper.GetOrCreateDefaultSettingsAsync(_dbContext, clinicId, now, cancellationToken);
            s.Update(vipJumpLimit, lateGraceMinutes, maxQueuePushes, defaultSlotStepMinutes, minBookingLeadMinutes, rescheduleOptionsJson, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "scheduling.settings.update",
                nameof(ClinicSchedulingSetting),
                s.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { s.VipJumpLimit, s.LateGraceMinutes, s.MaxQueuePushes, s.DefaultSlotStepMinutes, s.MinBookingLeadMinutes }),
                userId, clinicId, ipAddress, requestId, cancellationToken);

            return Result<ClinicSchedulingSettingResult>.Success(new ClinicSchedulingSettingResult(
                s.VipJumpLimit, s.LateGraceMinutes, s.MaxQueuePushes, s.DefaultSlotStepMinutes, s.MinBookingLeadMinutes, s.RescheduleOptionsJson));
        });
    }

    public async Task<Result<IReadOnlyList<AppointmentCategoryResult>>> ListCategoriesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.read", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<AppointmentCategoryResult>>.Failure(gate.Error);
        }

        List<AppointmentCategoryResult> list = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.AppointmentCategories
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided)
                .OrderBy(c => c.NameEn)
                .Select(c => new AppointmentCategoryResult(
                    c.Id, c.ClinicId, c.SpecialtyCode, c.NameAr, c.NameEn,
                    c.DefaultDurationMinutes, c.DefaultPriceAmount, c.DefaultPriceCurrency,
                    c.BookableOnline, c.IsActive, c.CreatedAtUtc))
                .ToListAsync(cancellationToken));

        return Result<IReadOnlyList<AppointmentCategoryResult>>.Success(list);
    }

    public async Task<Result<AppointmentCategoryResult>> CreateCategoryAsync(
        Guid userId,
        Guid clinicId,
        string specialtyCode,
        string nameAr,
        string nameEn,
        int defaultDurationMinutes,
        decimal defaultPriceAmount,
        string? defaultPriceCurrency,
        bool bookableOnline,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.write", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentCategoryResult>.Failure(gate.Error);
        }

        if (defaultDurationMinutes < 5 || defaultDurationMinutes > 480 || defaultPriceAmount < 0)
        {
            return Result<AppointmentCategoryResult>.Failure(Error.Validation("INVALID_CATEGORY", "Category duration must be 5..480 minutes and price >= 0."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            var price = new Money(defaultPriceAmount, string.IsNullOrWhiteSpace(defaultPriceCurrency) ? Money.DefaultCurrency : defaultPriceCurrency);
            var category = new AppointmentCategory(
                GuidV7.NewGuid(), clinicId, specialtyCode, nameAr, nameEn, defaultDurationMinutes, price, bookableOnline, now);

            _dbContext.AppointmentCategories.Add(category);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "scheduling.category.create", nameof(AppointmentCategory), category.Id.ToString(),
                null, JsonSerializer.Serialize(new { category.NameEn, category.DefaultDurationMinutes, category.DefaultPriceAmount }),
                userId, clinicId, ipAddress, requestId, cancellationToken);

            return Result<AppointmentCategoryResult>.Success(SchedulingProjectionHelper.ToCategoryResult(category));
        });
    }

    public async Task<Result<AppointmentCategoryResult>> UpdateCategoryAsync(
        Guid userId,
        Guid clinicId,
        Guid categoryId,
        string specialtyCode,
        string nameAr,
        string nameEn,
        int defaultDurationMinutes,
        decimal defaultPriceAmount,
        string? defaultPriceCurrency,
        bool bookableOnline,
        bool isActive,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.write", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentCategoryResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            AppointmentCategory? category = await _dbContext.AppointmentCategories
                .FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.Id == categoryId && !c.IsVoided, cancellationToken);
            if (category == null)
            {
                return Result<AppointmentCategoryResult>.Failure(Error.NotFound("CATEGORY_NOT_FOUND", "Appointment category was not found."));
            }

            if (defaultDurationMinutes < 5 || defaultDurationMinutes > 480 || defaultPriceAmount < 0)
            {
                return Result<AppointmentCategoryResult>.Failure(Error.Validation("INVALID_CATEGORY", "Category duration must be 5..480 minutes and price >= 0."));
            }

            var price = new Money(defaultPriceAmount, string.IsNullOrWhiteSpace(defaultPriceCurrency) ? category.DefaultPriceCurrency : defaultPriceCurrency);
            category.Update(specialtyCode, nameAr, nameEn, defaultDurationMinutes, price, bookableOnline, isActive, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "scheduling.category.update", nameof(AppointmentCategory), category.Id.ToString(),
                null, JsonSerializer.Serialize(new { category.NameEn, category.DefaultDurationMinutes, category.DefaultPriceAmount, category.BookableOnline }),
                userId, clinicId, ipAddress, requestId, cancellationToken);

            return Result<AppointmentCategoryResult>.Success(SchedulingProjectionHelper.ToCategoryResult(category));
        });
    }

    public async Task<Result<DoctorBookingSettingResult>> UpdateDoctorBookingSettingAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        bool acceptsOnlineBooking,
        int? maxDailyOnlineBookings,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.write", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<DoctorBookingSettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool doctorExists = await _dbContext.ClinicDoctors.AnyAsync(d => d.ClinicId == clinicId && d.Id == doctorId && !d.IsVoided, cancellationToken);
            if (!doctorExists)
            {
                return Result<DoctorBookingSettingResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor was not found in this clinic."));
            }

            DateTimeOffset now = _clock.UtcNow;
            DoctorBookingSetting? setting = await _dbContext.DoctorBookingSettings
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && s.DoctorId == doctorId && !s.IsVoided, cancellationToken);
            if (setting == null)
            {
                setting = new DoctorBookingSetting(GuidV7.NewGuid(), clinicId, doctorId, acceptsOnlineBooking, maxDailyOnlineBookings, now);
                _dbContext.DoctorBookingSettings.Add(setting);
            }
            else
            {
                setting.Update(acceptsOnlineBooking, maxDailyOnlineBookings, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<DoctorBookingSettingResult>.Success(new DoctorBookingSettingResult(setting.DoctorId, setting.AcceptsOnlineBooking, setting.MaxDailyOnlineBookings));
        });
    }

    public async Task<Result<IReadOnlyList<AppointmentItemResult>>> ListAppointmentsAsync(
        Guid userId,
        Guid clinicId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? doctorId,
        string? status,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.read", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<AppointmentItemResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);

            IQueryable<Appointment> query = _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided);

            if (scopedDoctorId.HasValue)
            {
                if (doctorId.HasValue && doctorId.Value != scopedDoctorId.Value)
                {
                    return Result<IReadOnlyList<AppointmentItemResult>>.Success(Array.Empty<AppointmentItemResult>());
                }

                query = query.Where(a => a.DoctorId == scopedDoctorId.Value);
            }
            else if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(a => a.EndUtc > fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(a => a.StartUtc < toUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentStatus>(status.Trim(), true, out AppointmentStatus parsedStatus))
            {
                query = query.Where(a => a.Status == parsedStatus);
            }

            List<Appointment> rows = await query.OrderBy(a => a.StartUtc).Take(250).ToListAsync(cancellationToken);
            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, rows, cancellationToken);
            return Result<IReadOnlyList<AppointmentItemResult>>.Success(hydrated);
        });
    }

    public async Task<Result<AppointmentItemResult>> GetAppointmentAsync(
        Guid userId,
        Guid clinicId,
        Guid appointmentId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.read", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentItemResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);
            Appointment? appt = await _dbContext.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == appointmentId && !a.IsVoided, cancellationToken);

            if (appt == null || (scopedDoctorId.HasValue && appt.DoctorId != scopedDoctorId.Value))
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("APPOINTMENT_NOT_FOUND", "Appointment was not found."));
            }

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [appt], cancellationToken);
            return Result<AppointmentItemResult>.Success(hydrated[0]);
        });
    }

    public async Task<Result<AppointmentItemResult>> BookAppointmentAsync(
        Guid userId,
        Guid clinicId,
        BookAppointmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.write", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentItemResult>.Failure(gate.Error);
        }

        if (!Enum.TryParse<AppointmentPriority>(input.Priority, true, out AppointmentPriority priority))
        {
            priority = AppointmentPriority.Normal;
        }

        if (priority is AppointmentPriority.Vip or AppointmentPriority.Rush)
        {
            var permsResult = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
            if (permsResult.IsFailure || !permsResult.Value.EffectivePermissions.Contains("scheduling.priority", StringComparer.OrdinalIgnoreCase))
            {
                return Result<AppointmentItemResult>.Failure(
                    Error.Forbidden("PRIORITY_CLAIM_REQUIRED", "Assigning VIP or Rush priority requires the scheduling.priority permission."));
            }
        }

        return await BookInternalAsync(
            clinicId,
            userId,
            AppointmentSource.Staff,
            input with { Priority = priority.ToString() },
            ipAddress,
            requestId,
            cancellationToken);
    }

    public async Task<Result<AppointmentItemResult>> BookPortalAppointmentAsync(
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        Guid categoryId,
        DateTimeOffset startUtc,
        string? notes,
        Guid? operationId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<string> enabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .Where(m => m.ClinicId == clinicId && m.Enabled)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);
            if (!enabled.Contains("scheduling", StringComparer.OrdinalIgnoreCase)
                || !enabled.Contains("portal-booking", StringComparer.OrdinalIgnoreCase))
            {
                return Result<AppointmentItemResult>.Failure(Error.Forbidden("MODULE_DISABLED", "Portal booking or scheduling module is not enabled for this clinic."));
            }

            var input = new BookAppointmentInput(
                null,
                patientId,
                doctorId,
                null,
                categoryId,
                startUtc,
                null,
                AppointmentPriority.Normal.ToString(),
                null,
                notes,
                operationId);

            return await BookInternalAsync(clinicId, null, AppointmentSource.Portal, input, null, null, cancellationToken);
        });
    }

    public async Task<Result<AppointmentItemResult>> CancelPortalAppointmentAsync(
        Guid clinicId,
        Guid patientId,
        Guid appointmentId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<string> enabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .Where(m => m.ClinicId == clinicId && m.Enabled)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);
            if (!enabled.Contains("scheduling", StringComparer.OrdinalIgnoreCase)
                || !enabled.Contains("portal-booking", StringComparer.OrdinalIgnoreCase))
            {
                return Result<AppointmentItemResult>.Failure(Error.Forbidden("MODULE_DISABLED", "Portal booking or scheduling module is not enabled for this clinic."));
            }

            Appointment? appt = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == appointmentId && !a.IsVoided, cancellationToken);
            if (appt == null || appt.PatientId != patientId)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("APPOINTMENT_NOT_FOUND", "Appointment was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            if (appt.Status != AppointmentStatus.Requested)
            {
                ClinicPortalBookingSetting bookingSettings = await PortalBookingHelper.GetOrBuildDefaultBookingSettingsAsync(
                    _dbContext, clinicId, now, cancellationToken);
                if (bookingSettings.CancellationCutoffHours > 0
                    && appt.StartUtc < now.AddHours(bookingSettings.CancellationCutoffHours))
                {
                    return Result<AppointmentItemResult>.Failure(Error.Validation(
                        "CANCELLATION_CUTOFF_PASSED",
                        $"Online cancellation is closed within {bookingSettings.CancellationCutoffHours} hours of the appointment."));
                }
            }

            return await ApplyTransitionInternalAsync(clinicId, null, appt, AppointmentStatus.Cancelled, reason ?? "Cancelled via portal", null, null, cancellationToken);
        });
    }

    public async Task<Result<AppointmentItemResult>> ApprovePortalBookingRequestAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid appointmentId,
        string? reviewNotes,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await AcquireClinicScheduleLockAsync(clinicId, cancellationToken);

            Appointment? appt = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == appointmentId && !a.IsVoided, cancellationToken);
            if (appt == null || appt.Source != AppointmentSource.Portal)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("BOOKING_REQUEST_NOT_FOUND", "Portal booking request was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            if (appt.IsPortalRequestExpired(now))
            {
                appt.DeclinePortalRequest(staffUserId, "RequestHoldExpired", now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result<AppointmentItemResult>.Failure(Error.Conflict("REQUEST_EXPIRED", "Portal booking request hold has expired."));
            }

            if (!appt.ApprovePortalRequest(staffUserId, reviewNotes, now))
            {
                return Result<AppointmentItemResult>.Failure(Error.Validation("REQUEST_NOT_PENDING", "Only pending portal booking requests can be approved."));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                "portal.booking.request.approve",
                nameof(Appointment),
                appt.Id.ToString(),
                AppointmentStatus.Requested.ToString(),
                JsonSerializer.Serialize(new { Status = AppointmentStatus.Booked.ToString(), ReviewNotes = reviewNotes }),
                staffUserId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(clinicId, "PortalBookingApproved", appt.Id, appt.DoctorId, now, "Portal booking request approved."),
                cancellationToken);

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [appt], cancellationToken);
            return Result<AppointmentItemResult>.Success(hydrated[0]);
        });
    }

    public async Task<Result<AppointmentItemResult>> DeclinePortalBookingRequestAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid appointmentId,
        string? reviewNotes,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Appointment? appt = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == appointmentId && !a.IsVoided, cancellationToken);
            if (appt == null || appt.Source != AppointmentSource.Portal)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("BOOKING_REQUEST_NOT_FOUND", "Portal booking request was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            if (!appt.DeclinePortalRequest(staffUserId, reviewNotes, now))
            {
                return Result<AppointmentItemResult>.Failure(Error.Validation("REQUEST_NOT_PENDING", "Only pending portal booking requests can be declined."));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "portal.booking.request.decline",
                nameof(Appointment),
                appt.Id.ToString(),
                AppointmentStatus.Requested.ToString(),
                JsonSerializer.Serialize(new { Status = AppointmentStatus.Declined.ToString(), ReviewNotes = reviewNotes }),
                staffUserId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(clinicId, "PortalBookingDeclined", appt.Id, appt.DoctorId, now, "Portal booking request declined."),
                cancellationToken);

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [appt], cancellationToken);
            return Result<AppointmentItemResult>.Success(hydrated[0]);
        });
    }

    public async Task<Result<int>> ExpireStalePortalBookingRequestsAsync(
        Guid clinicId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            List<Appointment> stale = await _dbContext.Appointments
                .Where(a => a.ClinicId == clinicId
                            && !a.IsVoided
                            && a.Status == AppointmentStatus.Requested
                            && a.ApprovalExpiresAtUtc.HasValue
                            && a.ApprovalExpiresAtUtc.Value <= now)
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
            {
                return Result<int>.Success(0);
            }

            foreach (Appointment item in stale)
            {
                item.DeclinePortalRequest(actorUserId, "RequestHoldExpired", now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "portal.booking.requests.expire",
                nameof(Appointment),
                clinicId.ToString(),
                null,
                JsonSerializer.Serialize(new { ExpiredCount = stale.Count }),
                actorUserId,
                clinicId,
                null,
                null,
                cancellationToken);

            return Result<int>.Success(stale.Count);
        });
    }

    public async Task<Result<AppointmentItemResult>> TransitionAppointmentStatusAsync(
        Guid userId,
        Guid clinicId,
        Guid appointmentId,
        string nextStatus,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.write", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentItemResult>.Failure(gate.Error);
        }

        if (!Enum.TryParse<AppointmentStatus>(nextStatus, true, out AppointmentStatus targetStatus))
        {
            return Result<AppointmentItemResult>.Failure(Error.Validation("INVALID_STATUS", $"Unknown appointment status '{nextStatus}'."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Appointment? appt = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == appointmentId && !a.IsVoided, cancellationToken);
            if (appt == null)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("APPOINTMENT_NOT_FOUND", "Appointment was not found."));
            }

            return await ApplyTransitionInternalAsync(clinicId, userId, appt, targetStatus, reason, ipAddress, requestId, cancellationToken);
        });
    }

    public async Task<Result<AppointmentItemResult>> RescheduleAppointmentAsync(
        Guid userId,
        Guid clinicId,
        Guid appointmentId,
        RescheduleAppointmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.write", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentItemResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await AcquireClinicScheduleLockAsync(clinicId, cancellationToken);

            Appointment? appt = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == appointmentId && !a.IsVoided, cancellationToken);
            if (appt == null)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("APPOINTMENT_NOT_FOUND", "Appointment was not found."));
            }

            TimeSpan duration = appt.EndUtc - appt.StartUtc;
            DateTimeOffset newStart;
            DateTimeOffset newEnd;

            if (input.CustomStartUtc.HasValue)
            {
                newStart = input.CustomStartUtc.Value;
                newEnd = input.CustomEndUtc ?? newStart.Add(duration);
            }
            else if (string.Equals(input.PresetKey, "after_week", StringComparison.OrdinalIgnoreCase))
            {
                newStart = appt.StartUtc.AddDays(7);
                newEnd = newStart.Add(duration);
            }
            else if (string.Equals(input.PresetKey, "after_month", StringComparison.OrdinalIgnoreCase))
            {
                newStart = appt.StartUtc.AddDays(30);
                newEnd = newStart.Add(duration);
            }
            else
            {
                return Result<AppointmentItemResult>.Failure(Error.Validation("INVALID_RESCHEDULE", "Provide a valid reschedule presetKey or customStartUtc."));
            }

            Guid targetDoctorId = input.NewDoctorId ?? appt.DoctorId;
            ClinicDoctor? doc = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.Id == targetDoctorId && !d.IsVoided && d.IsActive, cancellationToken);
            if (doc == null)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Target doctor was not found."));
            }

            Guid? targetRoomId = input.NewRoomId ?? appt.RoomId ?? SchedulingProjectionHelper.GetDefaultRoomId(doc);
            Result<bool> slotValidation = await ValidateSlotInsideClinicScheduleAsync(
                clinicId, targetDoctorId, targetRoomId, newStart, newEnd, clinic.TimeZone, appt.Id, cancellationToken);
            if (slotValidation.IsFailure)
            {
                return Result<AppointmentItemResult>.Failure(slotValidation.Error);
            }

            DateTimeOffset prevStart = appt.StartUtc;
            DateTimeOffset now = _clock.UtcNow;
            appt.Reschedule(newStart, newEnd, targetDoctorId, targetRoomId, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                "scheduling.appointment.reschedule",
                nameof(Appointment),
                appt.Id.ToString(),
                JsonSerializer.Serialize(new { StartUtc = prevStart }),
                JsonSerializer.Serialize(new { StartUtc = newStart, appt.DoctorId, appt.RoomId }),
                userId, clinicId, ipAddress, requestId, cancellationToken);

            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(clinicId, "SlotFreed", appt.Id, appt.DoctorId, now, "Appointment rescheduled."),
                cancellationToken);

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [appt], cancellationToken);
            return Result<AppointmentItemResult>.Success(hydrated[0]);
        });
    }

    public async Task<Result<CloseDayResult>> CloseDayAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string startDate,
        string endDate,
        string reasonAr,
        string reasonEn,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinic.settings.edit", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<CloseDayResult>.Failure(gate.Error);
        }

        if (!DateOnly.TryParseExact(startDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedStart) ||
            !DateOnly.TryParseExact(endDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedEnd) ||
            parsedEnd < parsedStart)
        {
            return Result<CloseDayResult>.Failure(Error.Validation("INVALID_DATE_RANGE", "startDate and endDate must be valid yyyy-MM-dd dates."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<CloseDayResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            var dayOff = new ClinicDayOff(
                GuidV7.NewGuid(),
                clinicId,
                doctorId,
                parsedStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                parsedEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                reasonAr,
                reasonEn,
                now);

            _dbContext.ClinicDaysOff.Add(dayOff);
            await _dbContext.SaveChangesAsync(cancellationToken);

            TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(clinic.TimeZone);
            DateTimeOffset minUtc = new(parsedStart.ToDateTime(TimeOnly.MinValue), tz.GetUtcOffset(parsedStart.ToDateTime(TimeOnly.MinValue)));
            DateTimeOffset maxUtc = new(parsedEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), tz.GetUtcOffset(parsedEnd.AddDays(1).ToDateTime(TimeOnly.MinValue)));

            List<Appointment> candidates = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId
                            && !a.IsVoided
                            && a.StartUtc >= minUtc.AddHours(-24)
                            && a.StartUtc <= maxUtc.AddHours(24)
                            && (!doctorId.HasValue || a.DoctorId == doctorId.Value))
                .OrderBy(a => a.StartUtc)
                .ToListAsync(cancellationToken);

            DateTimeOffset nowUtc = _clock.UtcNow;
            List<Appointment> affected = candidates.Where(a =>
            {
                if (!AppointmentStateMachine.IsOccupying(a, nowUtc) || a.Status == AppointmentStatus.Completed)
                {
                    return false;
                }

                DateOnly localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(a.StartUtc, tz).DateTime);
                return localDay >= parsedStart && localDay <= parsedEnd;
            }).ToList();

            await _auditService.LogAsync(
                "scheduling.day.close",
                nameof(ClinicDayOff),
                dayOff.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { dayOff.StartDate, dayOff.EndDate, AffectedCount = affected.Count }),
                userId, clinicId, ipAddress, requestId, cancellationToken);

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, affected, cancellationToken);
            return Result<CloseDayResult>.Success(new CloseDayResult(dayOff.Id, dayOff.StartDate, dayOff.EndDate, hydrated));
        });
    }

    public async Task<Result<DoctorTimetableResult>> GetDoctorTimetableAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        string date,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "scheduling.read", "scheduling", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<DoctorTimetableResult>.Failure(gate.Error);
        }

        if (!DateOnly.TryParseExact(date?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly localDate))
        {
            return Result<DoctorTimetableResult>.Failure(Error.Validation("INVALID_DATE", "Date must be yyyy-MM-dd."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);
            if (scopedDoctorId.HasValue && scopedDoctorId.Value != doctorId)
            {
                return Result<DoctorTimetableResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor timetable was not found."));
            }

            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            ClinicDoctor? doc = await _dbContext.ClinicDoctors.AsNoTracking().FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.Id == doctorId && !d.IsVoided, cancellationToken);
            if (clinic == null || doc == null)
            {
                return Result<DoctorTimetableResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor was not found."));
            }

            TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(clinic.TimeZone);
            DateTimeOffset dayStartUtc = new(localDate.ToDateTime(TimeOnly.MinValue), tz.GetUtcOffset(localDate.ToDateTime(TimeOnly.MinValue)));
            DateTimeOffset dayEndUtc = dayStartUtc.AddDays(1);

            List<Appointment> rows = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.DoctorId == doctorId && a.StartUtc >= dayStartUtc && a.StartUtc < dayEndUtc)
                .OrderBy(a => a.StartUtc)
                .ToListAsync(cancellationToken);

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, rows, cancellationToken);
            var byCat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var byStatus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (AppointmentItemResult item in hydrated)
            {
                byCat[item.CategoryNameEn] = byCat.GetValueOrDefault(item.CategoryNameEn) + 1;
                byStatus[item.Status] = byStatus.GetValueOrDefault(item.Status) + 1;
            }

            return Result<DoctorTimetableResult>.Success(new DoctorTimetableResult(
                doc.Id, doc.FullName, doc.FullName, localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                hydrated.Count, byCat, byStatus, hydrated));
        });
    }

    public async Task<Result<IReadOnlyList<QueueEntryItemResult>>> GetQueueBoardAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string? date,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.read", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<QueueEntryItemResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string targetDate = string.IsNullOrWhiteSpace(date)
                ? SchedulingProjectionHelper.ToLocalDateString(_clock.UtcNow, clinic?.TimeZone)
                : date.Trim();

            IQueryable<QueueEntry> query = _dbContext.QueueEntries
                .AsNoTracking()
                .Where(q => q.ClinicId == clinicId && !q.IsVoided && q.QueueDate == targetDate);
            if (doctorId.HasValue)
            {
                query = query.Where(q => q.DoctorId == doctorId.Value);
            }

            List<QueueEntry> entries = await query
                .OrderBy(q => q.DoctorId)
                .ThenBy(q => q.OrderIndex)
                .ThenBy(q => q.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            List<QueueEntryItemResult> hydrated = await SchedulingProjectionHelper.HydrateQueueAsync(_dbContext, clinicId, entries, cancellationToken);
            return Result<IReadOnlyList<QueueEntryItemResult>>.Success(hydrated);
        });
    }

    public async Task<Result<QueueEntryItemResult>> EnqueueWalkInAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid doctorId,
        string? priority,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.manage", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<QueueEntryItemResult>.Failure(gate.Error);
        }

        AppointmentPriority parsedPriority = Enum.TryParse<AppointmentPriority>(priority, true, out AppointmentPriority p) ? p : AppointmentPriority.Normal;
        if (parsedPriority is AppointmentPriority.Vip or AppointmentPriority.Rush)
        {
            var perms = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
            if (perms.IsFailure || !perms.Value.EffectivePermissions.Contains("scheduling.priority", StringComparer.OrdinalIgnoreCase))
            {
                return Result<QueueEntryItemResult>.Failure(Error.Forbidden("PRIORITY_CLAIM_REQUIRED", "Assigning VIP or Rush priority requires the scheduling.priority permission."));
            }
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            bool patientExists = await _dbContext.Patients.AnyAsync(pat => pat.ClinicId == clinicId && pat.Id == patientId && !pat.IsVoided, cancellationToken);
            bool doctorExists = await _dbContext.ClinicDoctors.AnyAsync(d => d.ClinicId == clinicId && d.Id == doctorId && !d.IsVoided && d.IsActive, cancellationToken);
            if (clinic == null || !patientExists || !doctorExists)
            {
                return Result<QueueEntryItemResult>.Failure(Error.NotFound("ENTITY_NOT_FOUND", "Patient or doctor was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            ClinicSchedulingSetting settings = await SchedulingProjectionHelper.GetOrCreateDefaultSettingsAsync(_dbContext, clinicId, now, cancellationToken);
            string queueDate = SchedulingProjectionHelper.ToLocalDateString(now, clinic.TimeZone);

            QueueEntry entry = await QueueWorkflowHelper.EnqueueInternalAsync(
                _dbContext, clinicId, null, patientId, doctorId, queueDate, parsedPriority, settings.VipJumpLimit, now, cancellationToken);

            await _auditService.LogAsync(
                "scheduling.queue.walkin", nameof(QueueEntry), entry.Id.ToString(),
                null, JsonSerializer.Serialize(new { entry.DoctorId, Priority = entry.Priority.ToString(), entry.OrderIndex, entry.VipJumpCount }),
                userId, clinicId, ipAddress, requestId, cancellationToken);

            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(clinicId, "QueueChanged", entry.Id, doctorId, now, "Walk-in added to queue."),
                cancellationToken);

            List<QueueEntryItemResult> hydrated = await SchedulingProjectionHelper.HydrateQueueAsync(_dbContext, clinicId, [entry], cancellationToken);
            return Result<QueueEntryItemResult>.Success(hydrated[0]);
        });
    }

    public async Task<Result<QueueEntryItemResult>> PushLateQueueEntryAsync(
        Guid userId,
        Guid clinicId,
        Guid queueEntryId,
        bool markNoShowOnExceed,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.manage", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<QueueEntryItemResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicSchedulingSetting settings = await SchedulingProjectionHelper.GetOrCreateDefaultSettingsAsync(_dbContext, clinicId, now, cancellationToken);
            QueueEntry? entry = await _dbContext.QueueEntries
                .FirstOrDefaultAsync(q => q.ClinicId == clinicId && q.Id == queueEntryId && !q.IsVoided, cancellationToken);
            if (entry == null)
            {
                return Result<QueueEntryItemResult>.Failure(Error.NotFound("QUEUE_ENTRY_NOT_FOUND", "Queue entry was not found."));
            }

            QueueEntryItemResult? pushed = await QueueWorkflowHelper.PushLateEntryAsync(
                _dbContext, _auditService, clinicId, userId, entry, settings.MaxQueuePushes, markNoShowOnExceed, ipAddress, requestId, now, cancellationToken);
            if (pushed == null)
            {
                return Result<QueueEntryItemResult>.Failure(Error.Validation("QUEUE_ENTRY_NOT_WAITING", "Only waiting queue entries can be pushed back."));
            }

            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(clinicId, "QueueChanged", entry.Id, entry.DoctorId, now, "Queue entry pushed back."),
                cancellationToken);

            return Result<QueueEntryItemResult>.Success(pushed);
        });
    }

    public async Task<Result<QueueEntryItemResult>> CallNextQueueEntryAsync(
        Guid userId,
        Guid clinicId,
        Guid queueEntryId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.manage", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<QueueEntryItemResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            QueueEntry? entry = await _dbContext.QueueEntries
                .FirstOrDefaultAsync(q => q.ClinicId == clinicId && q.Id == queueEntryId && !q.IsVoided, cancellationToken);
            if (entry == null)
            {
                return Result<QueueEntryItemResult>.Failure(Error.NotFound("QUEUE_ENTRY_NOT_FOUND", "Queue entry was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            entry.MarkInSession(now);
            if (entry.AppointmentId.HasValue)
            {
                Appointment? appt = await _dbContext.Appointments
                    .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == entry.AppointmentId.Value, cancellationToken);
                if (appt != null && AppointmentStateMachine.CanTransition(appt.Status, AppointmentStatus.InSession))
                {
                    appt.TryTransitionTo(AppointmentStatus.InSession, now);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(clinicId, "QueueChanged", entry.Id, entry.DoctorId, now, "Patient called into session."),
                cancellationToken);

            List<QueueEntryItemResult> hydrated = await SchedulingProjectionHelper.HydrateQueueAsync(_dbContext, clinicId, [entry], cancellationToken);
            return Result<QueueEntryItemResult>.Success(hydrated[0]);
        });
    }

    public async Task<Result<WaitingListItemResult>> AddToWaitingListAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        Guid? doctorId,
        Guid? categoryId,
        string preferredDate,
        string? notes,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.manage", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<WaitingListItemResult>.Failure(gate.Error);
        }

        if (!DateOnly.TryParseExact(preferredDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedDate))
        {
            return Result<WaitingListItemResult>.Failure(Error.Validation("INVALID_DATE", "preferredDate must be yyyy-MM-dd."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Patient? patient = await _dbContext.Patients.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == patientId && !p.IsVoided, cancellationToken);
            if (patient == null)
            {
                return Result<WaitingListItemResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            var entry = new WaitingListEntry(
                GuidV7.NewGuid(), clinicId, patientId, doctorId, categoryId,
                parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), notes, now);

            _dbContext.WaitingListEntries.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<WaitingListItemResult>.Success(new WaitingListItemResult(
                entry.Id, entry.ClinicId, entry.PatientId, patient.FullName, patient.FileNumber,
                entry.DoctorId, entry.CategoryId, entry.PreferredDate, entry.Notes, entry.IsFulfilled, entry.CreatedAtUtc));
        });
    }

    public async Task<Result<IReadOnlyList<WaitingListItemResult>>> ListWaitingListAsync(
        Guid userId,
        Guid clinicId,
        string? date,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.read", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<WaitingListItemResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<WaitingListEntry> query = _dbContext.WaitingListEntries
                .AsNoTracking()
                .Where(w => w.ClinicId == clinicId && !w.IsVoided);
            if (!string.IsNullOrWhiteSpace(date))
            {
                query = query.Where(w => w.PreferredDate == date.Trim());
            }

            List<WaitingListEntry> entries = await query.OrderBy(w => w.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
            List<Guid> pids = entries.Select(e => e.PatientId).Distinct().ToList();
            Dictionary<Guid, (string FullName, string FileNumber)> patients = await _dbContext.Patients
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && pids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => ValueTuple.Create(p.FullName, p.FileNumber), cancellationToken);

            List<WaitingListItemResult> output = entries.Select(w =>
            {
                patients.TryGetValue(w.PatientId, out (string FullName, string FileNumber) pat);
                return new WaitingListItemResult(
                    w.Id, w.ClinicId, w.PatientId, pat.FullName ?? string.Empty, pat.FileNumber ?? string.Empty,
                    w.DoctorId, w.CategoryId, w.PreferredDate, w.Notes, w.IsFulfilled, w.CreatedAtUtc);
            }).ToList();

            return Result<IReadOnlyList<WaitingListItemResult>>.Success(output);
        });
    }

    public async Task<Result<IReadOnlyList<SlotOfferItemResult>>> ListSlotOffersAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.read", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<SlotOfferItemResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<SlotOffer> offers = await _dbContext.SlotOffers
                .AsNoTracking()
                .Where(o => o.ClinicId == clinicId && !o.IsVoided)
                .OrderByDescending(o => o.OfferedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            List<Guid> pids = offers.Select(o => o.PatientId).Distinct().ToList();
            Dictionary<Guid, string> pNames = await _dbContext.Patients
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && pids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.FullName, cancellationToken);

            List<SlotOfferItemResult> output = offers.Select(o => new SlotOfferItemResult(
                o.Id, o.ClinicId, o.WaitingListEntryId, o.PatientId, pNames.GetValueOrDefault(o.PatientId) ?? string.Empty,
                o.DoctorId, o.RoomId, o.CategoryId, o.SlotStartUtc, o.SlotEndUtc, o.Status.ToString(), o.OfferedAtUtc)).ToList();

            return Result<IReadOnlyList<SlotOfferItemResult>>.Success(output);
        });
    }

    public async Task<Result<AppointmentItemResult>> AcceptSlotOfferAsync(
        Guid userId,
        Guid clinicId,
        Guid offerId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "queue.manage", "queue", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<AppointmentItemResult>.Failure(gate.Error);
        }

        SlotOffer? offer = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.SlotOffers.FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.Id == offerId && !o.IsVoided, cancellationToken));
        if (offer == null)
        {
            return Result<AppointmentItemResult>.Failure(Error.NotFound("OFFER_NOT_FOUND", "Slot offer was not found."));
        }

        if (offer.Status != SlotOfferStatus.Pending)
        {
            return Result<AppointmentItemResult>.Failure(Error.Validation("OFFER_NOT_PENDING", "Slot offer has already been resolved."));
        }

        var bookingInput = new BookAppointmentInput(
            null, offer.PatientId, offer.DoctorId, offer.RoomId, offer.CategoryId,
            offer.SlotStartUtc, offer.SlotEndUtc, AppointmentPriority.Normal.ToString(), null, "Accepted waiting list offer", null);

        Result<AppointmentItemResult> booked = await BookInternalAsync(clinicId, userId, AppointmentSource.Staff, bookingInput, ipAddress, requestId, cancellationToken);
        if (booked.IsFailure)
        {
            return booked;
        }

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            SlotOffer? trackedOffer = await _dbContext.SlotOffers.FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.Id == offerId, cancellationToken);
            WaitingListEntry? wl = await _dbContext.WaitingListEntries.FirstOrDefaultAsync(w => w.ClinicId == clinicId && w.Id == offer.WaitingListEntryId, cancellationToken);
            trackedOffer?.Accept(now);
            wl?.MarkFulfilled(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        return booked;
    }

    public async Task<Result<ClinicScheduleInspectResult>> InspectScheduleAsSuperAdminAsync(
        Guid superAdminId,
        Guid clinicId,
        string? date,
        CancellationToken cancellationToken = default)
    {
        ClinicScheduleInspectResult? snapshot = await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return null;
            }

            string targetDate = string.IsNullOrWhiteSpace(date)
                ? SchedulingProjectionHelper.ToLocalDateString(_clock.UtcNow, clinic.TimeZone)
                : date.Trim();

            List<AppointmentCategory> categories = await _dbContext.AppointmentCategories
                .AsNoTracking().Where(c => c.ClinicId == clinicId && !c.IsVoided).OrderBy(c => c.NameEn).ToListAsync(cancellationToken);
            List<Appointment> appts = await _dbContext.Appointments
                .AsNoTracking().Where(a => a.ClinicId == clinicId && !a.IsVoided).OrderBy(a => a.StartUtc).Take(200).ToListAsync(cancellationToken);
            List<QueueEntry> queue = await _dbContext.QueueEntries
                .AsNoTracking().Where(q => q.ClinicId == clinicId && !q.IsVoided && q.QueueDate == targetDate).OrderBy(q => q.OrderIndex).ToListAsync(cancellationToken);
            List<WaitingListEntry> waiting = await _dbContext.WaitingListEntries
                .AsNoTracking().Where(w => w.ClinicId == clinicId && !w.IsVoided).OrderBy(w => w.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);

            List<AppointmentItemResult> hydratedAppts = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, appts, cancellationToken);
            List<QueueEntryItemResult> hydratedQueue = await SchedulingProjectionHelper.HydrateQueueAsync(_dbContext, clinicId, queue, cancellationToken);

            List<Guid> wlPatients = waiting.Select(w => w.PatientId).Distinct().ToList();
            Dictionary<Guid, (string FullName, string FileNumber)> patLookup = wlPatients.Count == 0
                ? new Dictionary<Guid, (string, string)>()
                : await _dbContext.Patients.AsNoTracking()
                    .Where(p => p.ClinicId == clinicId && wlPatients.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => ValueTuple.Create(p.FullName, p.FileNumber), cancellationToken);

            List<WaitingListItemResult> hydratedWaiting = waiting.Select(w =>
            {
                patLookup.TryGetValue(w.PatientId, out (string FullName, string FileNumber) p);
                return new WaitingListItemResult(w.Id, w.ClinicId, w.PatientId, p.FullName ?? string.Empty, p.FileNumber ?? string.Empty, w.DoctorId, w.CategoryId, w.PreferredDate, w.Notes, w.IsFulfilled, w.CreatedAtUtc);
            }).ToList();

            return new ClinicScheduleInspectResult(
                clinicId,
                targetDate,
                categories.Select(SchedulingProjectionHelper.ToCategoryResult).ToList(),
                hydratedAppts,
                hydratedQueue,
                hydratedWaiting);
        }, "SuperAdmin inspect schedule", superAdminId.ToString(), cancellationToken);

        return snapshot == null
            ? Result<ClinicScheduleInspectResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."))
            : Result<ClinicScheduleInspectResult>.Success(snapshot);
    }

    private async Task<Result<AppointmentItemResult>> BookInternalAsync(
        Guid clinicId,
        Guid? userId,
        AppointmentSource source,
        BookAppointmentInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            if (input.OperationId.HasValue)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    Appointment? existingAppt = await _dbContext.Appointments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == existingOp.ResultEntityId && !a.IsVoided, cancellationToken);
                    if (existingAppt != null)
                    {
                        List<AppointmentItemResult> existingHydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [existingAppt], cancellationToken);
                        return Result<AppointmentItemResult>.Success(existingHydrated[0]);
                    }
                }
            }

            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            Patient? patient = await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == input.PatientId && !p.IsVoided, cancellationToken);
            ClinicDoctor? doctor = await _dbContext.ClinicDoctors.AsNoTracking().FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.Id == input.DoctorId && !d.IsVoided && d.IsActive, cancellationToken);
            AppointmentCategory? category = await _dbContext.AppointmentCategories.AsNoTracking().FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.Id == input.CategoryId && !c.IsVoided && c.IsActive, cancellationToken);

            if (clinic == null || patient == null || doctor == null || category == null)
            {
                return Result<AppointmentItemResult>.Failure(Error.NotFound("BOOKING_REFERENCE_NOT_FOUND", "Clinic, patient, doctor, or category was not found."));
            }

            DoctorBookingSetting? portalDocSetting = null;
            ClinicPortalBookingSetting? portalBookingSetting = null;
            if (source == AppointmentSource.Portal)
            {
                PatientAccount? portalAccount = await _dbContext.PatientAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.PatientId == patient.Id && !a.IsVoided, cancellationToken);
                if (portalAccount is { IsOnlineBookingBlocked: true })
                {
                    return Result<AppointmentItemResult>.Failure(Error.Forbidden(
                        "PATIENT_ONLINE_BOOKING_BLOCKED",
                        "Online booking is currently blocked for this patient."));
                }

                portalDocSetting = await _dbContext.DoctorBookingSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ClinicId == clinicId && s.DoctorId == doctor.Id && !s.IsVoided, cancellationToken);
                if (!category.BookableOnline || portalDocSetting is not { AcceptsOnlineBooking: true })
                {
                    return Result<AppointmentItemResult>.Failure(Error.Forbidden("ONLINE_BOOKING_DISABLED", "Selected doctor or category does not accept online booking."));
                }
            }

            int effectiveDurationMinutes = category.DefaultDurationMinutes;
            if (!input.EndUtc.HasValue && _timeEngineDurationProvider != null)
            {
                int? appliedDuration = await _timeEngineDurationProvider.GetAppliedDurationMinutesAsync(
                    clinicId,
                    doctor.Id,
                    category.Id,
                    cancellationToken);
                if (appliedDuration.HasValue)
                {
                    effectiveDurationMinutes = appliedDuration.Value;
                }
            }

            DateTimeOffset endUtc = input.EndUtc ?? input.StartUtc.AddMinutes(effectiveDurationMinutes);
            if (endUtc <= input.StartUtc)
            {
                return Result<AppointmentItemResult>.Failure(Error.Validation("INVALID_INTERVAL", "EndUtc must be after StartUtc."));
            }

            Guid? effectiveRoomId = input.RoomId ?? SchedulingProjectionHelper.GetDefaultRoomId(doctor);
            if (effectiveRoomId.HasValue)
            {
                bool roomExists = await _dbContext.ClinicRooms.AsNoTracking().AnyAsync(r => r.ClinicId == clinicId && r.Id == effectiveRoomId.Value && !r.IsVoided && r.IsActive, cancellationToken);
                if (!roomExists)
                {
                    return Result<AppointmentItemResult>.Failure(Error.NotFound("ROOM_NOT_FOUND", "Assigned room was not found."));
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            ClinicSchedulingSetting settings = await SchedulingProjectionHelper.GetOrCreateDefaultSettingsAsync(_dbContext, clinicId, now, cancellationToken);
            int effectiveMinLeadMinutes = settings.MinBookingLeadMinutes;
            if (source == AppointmentSource.Portal)
            {
                portalBookingSetting = await PortalBookingHelper.GetOrBuildDefaultBookingSettingsAsync(_dbContext, clinicId, now, cancellationToken);
                effectiveMinLeadMinutes = Math.Max(settings.MinBookingLeadMinutes, portalBookingSetting.MinLeadTimeMinutes);
                if (input.StartUtc > now.AddDays(portalBookingSetting.MaxDaysAhead))
                {
                    return Result<AppointmentItemResult>.Failure(Error.Validation(
                        "BOOKING_HORIZON_EXCEEDED",
                        $"Portal bookings cannot be more than {portalBookingSetting.MaxDaysAhead} days in advance."));
                }
            }

            if (effectiveMinLeadMinutes > 0 && input.StartUtc < now.AddMinutes(effectiveMinLeadMinutes))
            {
                return Result<AppointmentItemResult>.Failure(Error.Validation("LEAD_TIME_NOT_MET", $"Bookings require at least {effectiveMinLeadMinutes} minutes lead time."));
            }

            AppointmentPriority priority = Enum.TryParse<AppointmentPriority>(input.Priority, true, out AppointmentPriority p) ? p : AppointmentPriority.Normal;
            Guid appointmentId = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();

            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await AcquireClinicScheduleLockAsync(clinicId, cancellationToken);

            if (source == AppointmentSource.Portal && portalBookingSetting != null)
            {
                List<Appointment> futurePortalAppts = await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId
                                && !a.IsVoided
                                && a.PatientId == patient.Id
                                && a.Source == AppointmentSource.Portal
                                && a.StartUtc >= now)
                    .ToListAsync(cancellationToken);

                int activeFutureCount = futurePortalAppts.Count(a => AppointmentStateMachine.IsOccupying(a, now));
                if (activeFutureCount >= portalBookingSetting.MaxActiveFutureBookingsPerPatient)
                {
                    return Result<AppointmentItemResult>.Failure(Error.Validation(
                        "MAX_ACTIVE_FUTURE_BOOKINGS_REACHED",
                        $"Patients may hold at most {portalBookingSetting.MaxActiveFutureBookingsPerPatient} active upcoming online bookings."));
                }

                TimeZoneInfo clinicTz = ClinicTimeZoneResolver.Resolve(clinic.TimeZone);
                DateTimeOffset localStart = TimeZoneInfo.ConvertTime(input.StartUtc, clinicTz);
                DateTime localDayStartUnspecified = new(localStart.Year, localStart.Month, localStart.Day, 0, 0, 0, DateTimeKind.Unspecified);
                DateTimeOffset dayStartUtc = new DateTimeOffset(localDayStartUnspecified, clinicTz.GetUtcOffset(localDayStartUnspecified)).ToUniversalTime();
                DateTimeOffset dayEndUtc = new DateTimeOffset(localDayStartUnspecified.AddDays(1), clinicTz.GetUtcOffset(localDayStartUnspecified.AddDays(1))).ToUniversalTime();

                List<Appointment> dayPortalAppts = await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId
                                && !a.IsVoided
                                && a.Source == AppointmentSource.Portal
                                && a.StartUtc >= dayStartUtc
                                && a.StartUtc < dayEndUtc)
                    .ToListAsync(cancellationToken);

                int occupyingClinicDayCount = dayPortalAppts.Count(a => AppointmentStateMachine.IsOccupying(a, now));
                if (occupyingClinicDayCount >= portalBookingSetting.MaxBookingsPerDay)
                {
                    return Result<AppointmentItemResult>.Failure(Error.Validation(
                        "DAILY_PORTAL_BOOKING_LIMIT_REACHED",
                        "Daily online booking limit for this clinic has been reached."));
                }

                if (portalDocSetting?.MaxDailyOnlineBookings is int maxDocDaily)
                {
                    int doctorDayCount = dayPortalAppts.Count(a => a.DoctorId == doctor.Id && AppointmentStateMachine.IsOccupying(a, now));
                    if (doctorDayCount >= maxDocDaily)
                    {
                        return Result<AppointmentItemResult>.Failure(Error.Validation(
                            "DOCTOR_DAILY_ONLINE_LIMIT_REACHED",
                            "Doctor daily online booking limit has been reached."));
                    }
                }
            }

            Result<bool> valid = await ValidateSlotInsideClinicScheduleAsync(clinicId, doctor.Id, effectiveRoomId, input.StartUtc, endUtc, clinic.TimeZone, null, cancellationToken);
            if (valid.IsFailure)
            {
                Error slotError = source == AppointmentSource.Portal && string.Equals(valid.Error.Code, "APPOINTMENT_CONFLICT", StringComparison.Ordinal)
                    ? Error.Conflict("SLOT_TAKEN", "Selected slot is no longer available.")
                    : valid.Error;
                return Result<AppointmentItemResult>.Failure(slotError);
            }

            var appt = new Appointment(
                appointmentId, clinicId, patient.Id, doctor.Id, effectiveRoomId, category.Id,
                input.StartUtc, endUtc, priority, source, input.PlanSessionId, input.Notes, userId, now);

            if (source == AppointmentSource.Portal && portalBookingSetting?.Mode == PortalBookingMode.RequiresApproval)
            {
                appt.MarkAsPendingPortalRequest(now.AddHours(portalBookingSetting.ApprovalHoldHours), now);
            }

            _dbContext.Appointments.Add(appt);
            if (input.OperationId.HasValue)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(GuidV7.NewGuid(), clinicId, input.OperationId.Value, nameof(BookAppointmentAsync), appointmentId, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            if (source == AppointmentSource.Portal)
            {
                await _auditService.LogAsync(
                    "portal.booking.created",
                    nameof(Appointment),
                    appt.Id.ToString(),
                    null,
                    JsonSerializer.Serialize(new { appt.DoctorId, appt.CategoryId, Status = appt.Status.ToString(), appt.StartUtc }),
                    null,
                    clinicId,
                    ipAddress,
                    requestId,
                    cancellationToken);

                string eventType = appt.Status == AppointmentStatus.Requested ? "PortalBookingRequested" : "PortalBookingConfirmed";
                string msg = appt.Status == AppointmentStatus.Requested
                    ? "New portal booking request awaiting reception approval."
                    : "New portal appointment booked.";
                await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, eventType, appt.Id, doctor.Id, now, msg), cancellationToken);
            }

            if (priority is AppointmentPriority.Vip or AppointmentPriority.Rush)
            {
                await _auditService.LogAsync(
                    "scheduling.appointment.priority", nameof(Appointment), appt.Id.ToString(),
                    null, JsonSerializer.Serialize(new { Priority = priority.ToString(), appt.DoctorId }),
                    userId, clinicId, ipAddress, requestId, cancellationToken);
            }

            List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [appt], cancellationToken);
            return Result<AppointmentItemResult>.Success(hydrated[0]);
        });
    }

    private async Task<Result<AppointmentItemResult>> ApplyTransitionInternalAsync(
        Guid clinicId,
        Guid? userId,
        Appointment appt,
        AppointmentStatus targetStatus,
        string? reason,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken)
    {
        AppointmentStatus previous = appt.Status;
        DateTimeOffset now = _clock.UtcNow;
        if (!appt.TryTransitionTo(targetStatus, now, reason))
        {
            return Result<AppointmentItemResult>.Failure(Error.Validation("INVALID_STATUS_TRANSITION", $"Cannot transition appointment from {previous} to {targetStatus}."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        bool queueEnabled = await QueueWorkflowHelper.IsQueueModuleEnabledAsync(_dbContext, clinicId, cancellationToken);

        Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        string localDate = SchedulingProjectionHelper.ToLocalDateString(appt.StartUtc, clinic?.TimeZone);

        if (targetStatus == AppointmentStatus.Arrived)
        {
            if (queueEnabled)
            {
                ClinicSchedulingSetting settings = await SchedulingProjectionHelper.GetOrCreateDefaultSettingsAsync(_dbContext, clinicId, now, cancellationToken);
                QueueEntry entry = await QueueWorkflowHelper.EnqueueInternalAsync(
                    _dbContext, clinicId, appt.Id, appt.PatientId, appt.DoctorId, localDate, appt.Priority, settings.VipJumpLimit, now, cancellationToken);

                await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, "PatientArrived", appt.Id, appt.DoctorId, now, "Patient arrived."), cancellationToken);
                await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, "QueueChanged", entry.Id, appt.DoctorId, now, "Queue updated on arrival."), cancellationToken);
            }
            else
            {
                await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, "PatientArrived", appt.Id, appt.DoctorId, now, "Patient arrived."), cancellationToken);
            }
        }
        else if (targetStatus == AppointmentStatus.InSession && queueEnabled)
        {
            QueueEntry? linked = await _dbContext.QueueEntries.FirstOrDefaultAsync(q => q.ClinicId == clinicId && q.AppointmentId == appt.Id && !q.IsVoided, cancellationToken);
            if (linked != null)
            {
                linked.MarkInSession(now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, "QueueChanged", linked.Id, appt.DoctorId, now, "Patient in session."), cancellationToken);
            }
        }
        else if (targetStatus is AppointmentStatus.Completed or AppointmentStatus.NoShow)
        {
            if (targetStatus == AppointmentStatus.NoShow)
            {
                await PortalBookingHelper.RecordPatientNoShowAsync(_dbContext, clinicId, appt.PatientId, now, cancellationToken);
            }

            if (queueEnabled)
            {
                QueueEntry? linked = await _dbContext.QueueEntries.FirstOrDefaultAsync(q => q.ClinicId == clinicId && q.AppointmentId == appt.Id && !q.IsVoided, cancellationToken);
                if (linked != null)
                {
                    if (targetStatus == AppointmentStatus.Completed)
                    {
                        linked.MarkServed(now);
                    }
                    else
                    {
                        linked.IncrementPush(now, exceedMaxAsNoShow: true);
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, "QueueChanged", linked.Id, appt.DoctorId, now, "Queue updated."), cancellationToken);
                }
            }
        }
        else if (targetStatus == AppointmentStatus.Cancelled)
        {
            if (queueEnabled)
            {
                QueueEntry? linked = await _dbContext.QueueEntries.FirstOrDefaultAsync(q => q.ClinicId == clinicId && q.AppointmentId == appt.Id && !q.IsVoided, cancellationToken);
                if (linked != null)
                {
                    linked.MarkRemoved(now);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                await QueueWorkflowHelper.TryCreateOfferForFreedSlotAsync(_dbContext, clinicId, appt, localDate, now, cancellationToken);
            }

            await _realtimeNotifier.NotifyClinicAsync(new ScheduleRealtimeEvent(clinicId, "SlotFreed", appt.Id, appt.DoctorId, now, "Appointment cancelled and slot freed."), cancellationToken);
        }

        await _auditService.LogAsync(
            "scheduling.appointment.transition", nameof(Appointment), appt.Id.ToString(),
            JsonSerializer.Serialize(new { Status = previous.ToString() }),
            JsonSerializer.Serialize(new { Status = targetStatus.ToString(), Reason = reason }),
            userId, clinicId, ipAddress, requestId, cancellationToken);

        List<AppointmentItemResult> hydrated = await SchedulingProjectionHelper.HydrateAppointmentsAsync(_dbContext, clinicId, [appt], cancellationToken);
        return Result<AppointmentItemResult>.Success(hydrated[0]);
    }

    private async Task<Result<bool>> ValidateSlotInsideClinicScheduleAsync(
        Guid clinicId,
        Guid doctorId,
        Guid? roomId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string timeZoneId,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(timeZoneId);
        List<AvailabilityWorkingHourWindow> workingHours = await _dbContext.ClinicWorkingHours
            .AsNoTracking()
            .Where(w => w.ClinicId == clinicId && !w.IsVoided)
            .Select(w => new AvailabilityWorkingHourWindow(w.DoctorId, w.DayOfWeek, w.StartTime, w.EndTime, w.IsClosed))
            .ToListAsync(cancellationToken);

        List<AvailabilityDayOffRange> daysOff = await _dbContext.ClinicDaysOff
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && !d.IsVoided)
            .Select(d => new AvailabilityDayOffRange(d.DoctorId, d.StartDate, d.EndDate))
            .ToListAsync(cancellationToken);

        if (!AvailabilityCalculator.IsInsideWorkingWindow(startUtc, endUtc, doctorId, tz, workingHours, daysOff))
        {
            return Result<bool>.Failure(Error.Validation("OUTSIDE_WORKING_HOURS", "Requested slot is outside working hours or falls on a closed day."));
        }

        List<Appointment> candidates = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ClinicId == clinicId
                        && !a.IsVoided
                        && a.EndUtc > startUtc
                        && a.StartUtc < endUtc
                        && (!excludeAppointmentId.HasValue || a.Id != excludeAppointmentId.Value))
            .ToListAsync(cancellationToken);

        DateTimeOffset nowUtc = _clock.UtcNow;
        bool hasConflict = candidates.Any(a =>
            AppointmentStateMachine.IsOccupying(a, nowUtc)
            && AvailabilityCalculator.IntervalsOverlap(startUtc, endUtc, a.StartUtc, a.EndUtc)
            && (a.DoctorId == doctorId || (roomId.HasValue && a.RoomId == roomId.Value)));

        return hasConflict
            ? Result<bool>.Failure(Error.Conflict("APPOINTMENT_CONFLICT", "Doctor or room is already booked during the requested interval."))
            : Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyList<PortalUpcomingAppointmentItemResult>>> ListUpcomingAppointmentsForPortalAsync(
        Guid clinicId,
        Guid patientId,
        bool showDoctorNames,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            List<Appointment> upcoming = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId
                            && !a.IsVoided
                            && a.PatientId == patientId
                            && a.EndUtc >= now
                            && (a.Status == AppointmentStatus.Booked
                                || a.Status == AppointmentStatus.Arrived
                                || a.Status == AppointmentStatus.Late
                                || a.Status == AppointmentStatus.InSession
                                || (a.Status == AppointmentStatus.Requested
                                    && (!a.ApprovalExpiresAtUtc.HasValue || a.ApprovalExpiresAtUtc.Value > now))))
                .OrderBy(a => a.StartUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (upcoming.Count == 0)
            {
                return Result<IReadOnlyList<PortalUpcomingAppointmentItemResult>>.Success(Array.Empty<PortalUpcomingAppointmentItemResult>());
            }

            HashSet<Guid> categoryIds = upcoming.Select(a => a.CategoryId).Distinct().ToHashSet();
            Dictionary<Guid, AppointmentCategory> categories = await _dbContext.AppointmentCategories
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            Dictionary<Guid, string> doctorNames = new();
            if (showDoctorNames)
            {
                HashSet<Guid> doctorIds = upcoming.Select(a => a.DoctorId).Distinct().ToHashSet();
                doctorNames = await _dbContext.ClinicDoctors
                    .AsNoTracking()
                    .Where(d => d.ClinicId == clinicId && doctorIds.Contains(d.Id))
                    .ToDictionaryAsync(d => d.Id, d => d.FullName, cancellationToken);
            }

            List<PortalUpcomingAppointmentItemResult> results = upcoming
                .Select(a =>
                {
                    categories.TryGetValue(a.CategoryId, out AppointmentCategory? category);
                    string? docName = showDoctorNames && doctorNames.TryGetValue(a.DoctorId, out string? dn) ? dn : null;
                    return new PortalUpcomingAppointmentItemResult(
                        a.Id,
                        a.StartUtc,
                        a.EndUtc,
                        docName,
                        category?.SpecialtyCode ?? "general",
                        category?.NameAr ?? string.Empty,
                        category?.NameEn ?? string.Empty,
                        a.Status.ToString());
                })
                .ToList();

            return Result<IReadOnlyList<PortalUpcomingAppointmentItemResult>>.Success(results);
        });
    }

    private async Task<Guid?> ResolveScopedDoctorIdAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        var permsResult = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (permsResult.IsSuccess &&
            (permsResult.Value.IsOwner ||
             permsResult.Value.EffectivePermissions.Contains("clinic.settings.edit", StringComparer.OrdinalIgnoreCase) ||
             permsResult.Value.EffectivePermissions.Contains("queue.manage", StringComparer.OrdinalIgnoreCase)))
        {
            return null;
        }

        ClinicDoctor? callerDoctor = await _dbContext.ClinicDoctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.UserId == userId, cancellationToken);
        return callerDoctor?.Id;
    }

    private async Task AcquireClinicScheduleLockAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        var resource = new SqlParameter("@res", SqlDbType.NVarChar, 255) { Value = $"scheduling:{clinicId:N}" };
        const string sql = """
            DECLARE @rc int;
            EXEC @rc = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @rc < 0 THROW 50001, 'Could not acquire scheduling lock.', 1;
            """;
        await _dbContext.Database.ExecuteSqlRawAsync(sql, [resource], cancellationToken);
    }

    private async Task<T> ExecuteForClinicAsync<T>(Guid clinicId, Func<Task<T>> action)
    {
        Guid? prevClinicId = _currentClinicContext.ClinicId;
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
            else if (prevClinicId.HasValue)
            {
                _currentClinicContext.SetTenant(prevClinicId.Value);
            }
            else
            {
                _currentClinicContext.Clear();
            }
        }
    }
}
