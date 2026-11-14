using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements IAvailabilityQueries by combining clinic/doctor working hours, days off,
/// room & doctor occupancy, category duration, slot step, and booking lead time.
/// </summary>
public sealed class AvailabilityQueriesService : IAvailabilityQueries
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IClock _clock;
    private readonly ITimeEngineDurationProvider? _timeEngineDurationProvider;

    public AvailabilityQueriesService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IClock clock,
        ITimeEngineDurationProvider? timeEngineDurationProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeEngineDurationProvider = timeEngineDurationProvider;
    }

    public async Task<Result<IReadOnlyList<FreeSlotItemResult>>> GetFreeSlotsAsync(
        Guid userId,
        Guid clinicId,
        Guid categoryId,
        Guid? doctorId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(
            userId,
            clinicId,
            "scheduling.read",
            "scheduling",
            deviceId,
            cancellationToken);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<FreeSlotItemResult>>.Failure(gate.Error);
        }

        if (toUtc <= fromUtc)
        {
            return Result<IReadOnlyList<FreeSlotItemResult>>.Failure(
                Error.Validation("INVALID_RANGE", "toUtc must be after fromUtc."));
        }

        Guid? prevClinicId = _currentClinicContext.ClinicId;
        bool wasAdmin = _currentClinicContext.IsPlatformAdmin;
        try
        {
            _currentClinicContext.SetTenant(clinicId);

            Clinic? clinic = await _dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<IReadOnlyList<FreeSlotItemResult>>.Failure(
                    Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            AppointmentCategory? category = await _dbContext.AppointmentCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.Id == categoryId && !c.IsVoided && c.IsActive, cancellationToken);
            if (category == null)
            {
                return Result<IReadOnlyList<FreeSlotItemResult>>.Failure(
                    Error.NotFound("CATEGORY_NOT_FOUND", "Appointment category was not found."));
            }

            ClinicSchedulingSetting? settings = await _dbContext.ClinicSchedulingSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

            int slotStep = settings?.DefaultSlotStepMinutes ?? 15;
            int leadMinutes = settings?.MinBookingLeadMinutes ?? 0;

            List<ClinicDoctor> doctors = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .Where(d => d.ClinicId == clinicId && !d.IsVoided && d.IsActive && (!doctorId.HasValue || d.Id == doctorId.Value))
                .ToListAsync(cancellationToken);

            if (doctors.Count == 0)
            {
                return Result<IReadOnlyList<FreeSlotItemResult>>.Success(Array.Empty<FreeSlotItemResult>());
            }

            Dictionary<Guid, string> roomCodes = await _dbContext.ClinicRooms
                .AsNoTracking()
                .Where(r => r.ClinicId == clinicId && !r.IsVoided)
                .ToDictionaryAsync(r => r.Id, r => r.Code, cancellationToken);

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

            List<Appointment> existingAppointments = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.EndUtc > fromUtc && a.StartUtc < toUtc)
                .ToListAsync(cancellationToken);

            DateTimeOffset nowUtc = _clock.UtcNow;
            List<AvailabilityBusyInterval> busy = existingAppointments
                .Where(a => AppointmentStateMachine.IsOccupying(a, nowUtc))
                .Select(a => new AvailabilityBusyInterval(a.DoctorId, a.RoomId, a.StartUtc, a.EndUtc))
                .ToList();

            List<AvailabilityDoctorProfile> doctorProfiles = doctors
                .Select(d => new AvailabilityDoctorProfile(
                    d.Id,
                    SchedulingProjectionHelper.GetDefaultRoomId(d),
                    SchedulingProjectionHelper.GetPrimarySpecialty(d)))
                .ToList();

            IReadOnlyDictionary<Guid, int>? durationOverrides = _timeEngineDurationProvider != null
                ? await _timeEngineDurationProvider.GetAppliedDurationsByDoctorAsync(clinicId, categoryId, cancellationToken)
                : null;

            TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(clinic.TimeZone);
            IReadOnlyList<AvailableSlotCandidate> candidates = AvailabilityCalculator.ComputeFreeSlots(
                fromUtc,
                toUtc,
                _clock.UtcNow,
                category.DefaultDurationMinutes,
                slotStep,
                leadMinutes,
                tz,
                doctorProfiles,
                workingHours,
                daysOff,
                busy,
                durationOverrides);

            Dictionary<Guid, ClinicDoctor> doctorLookup = doctors.ToDictionary(d => d.Id);
            List<FreeSlotItemResult> output = candidates
                .Select(c =>
                {
                    ClinicDoctor doc = doctorLookup[c.DoctorId];
                    string? roomCode = c.RoomId.HasValue && roomCodes.TryGetValue(c.RoomId.Value, out string? rc) ? rc : null;
                    int slotMinutes = (int)Math.Round((c.EndUtc - c.StartUtc).TotalMinutes);
                    return new FreeSlotItemResult(
                        c.DoctorId,
                        doc.FullName,
                        doc.FullName,
                        c.RoomId,
                        roomCode,
                        c.StartUtc,
                        c.EndUtc,
                        slotMinutes);
                })
                .ToList();

            return Result<IReadOnlyList<FreeSlotItemResult>>.Success(output);
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
