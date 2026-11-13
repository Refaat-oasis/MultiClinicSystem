using System.Data;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Manages clinic rooms, doctors, working hours, days off, atomic number-block reservations, and owner audit viewer.
/// </summary>
public sealed class ClinicStructureService : IClinicStructureService
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ClinicStructureService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<ClinicRoomResult>>> ListRoomsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "dashboard.view", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicRoomResult>>.Failure(gate.Error);
        }

        var list = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicRooms
                .Where(r => r.ClinicId == clinicId && !r.IsVoided)
                .OrderBy(r => r.Code)
                .Select(r => new ClinicRoomResult(
                    r.Id,
                    r.ClinicId,
                    r.Code,
                    r.NameAr,
                    r.NameEn,
                    r.SpecialtyCode,
                    r.FloorOrLocation,
                    r.IsActive,
                    r.CreatedAtUtc))
                .ToListAsync(cancellationToken));

        return Result<IReadOnlyList<ClinicRoomResult>>.Success(list);
    }

    public async Task<Result<ClinicRoomResult>> CreateRoomAsync(
        Guid userId,
        Guid clinicId,
        string code,
        string nameAr,
        string nameEn,
        string? specialtyCode,
        string? floorOrLocation,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicRoomResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<ClinicRoomResult>.Failure(Error.Validation("ROOM_CODE_REQUIRED", "Room code is required."));
        }

        string cleanCode = code.Trim().ToUpperInvariant();
        Guid id = GuidV7.NewGuid();
        DateTimeOffset now = _clock.UtcNow;

        var res = await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool exists = await _dbContext.ClinicRooms
                .AnyAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Code == cleanCode, cancellationToken);

            if (exists)
            {
                return Result<ClinicRoomResult>.Failure(Error.Conflict("ROOM_CODE_EXISTS", $"Room code '{cleanCode}' already exists."));
            }

            var room = new ClinicRoom(id, clinicId, cleanCode, nameAr, nameEn, specialtyCode, floorOrLocation, now);
            _dbContext.ClinicRooms.Add(room);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ClinicRoomResult>.Success(new ClinicRoomResult(
                room.Id, room.ClinicId, room.Code, room.NameAr, room.NameEn, room.SpecialtyCode, room.FloorOrLocation, room.IsActive, room.CreatedAtUtc));
        });

        if (res.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "ClinicRoomCreated",
                entityName: "ClinicRoom",
                entityId: id.ToString(),
                oldValues: null,
                newValues: JsonSerializer.Serialize(new { cleanCode, nameAr, nameEn }),
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return res;
    }

    public async Task<Result<ClinicRoomResult>> UpdateRoomAsync(
        Guid userId,
        Guid clinicId,
        Guid roomId,
        string code,
        string nameAr,
        string nameEn,
        string? specialtyCode,
        string? floorOrLocation,
        bool isActive,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicRoomResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<ClinicRoomResult>.Failure(Error.Validation("ROOM_CODE_REQUIRED", "Room code is required."));
        }

        string cleanCode = code.Trim().ToUpperInvariant();
        var res = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var room = await _dbContext.ClinicRooms
                .FirstOrDefaultAsync(r => r.Id == roomId && r.ClinicId == clinicId && !r.IsVoided, cancellationToken);

            if (room == null)
            {
                return Result<ClinicRoomResult>.Failure(Error.NotFound("ROOM_NOT_FOUND", "Room not found."));
            }

            bool duplicate = await _dbContext.ClinicRooms
                .AnyAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.Id != roomId && r.Code == cleanCode, cancellationToken);

            if (duplicate)
            {
                return Result<ClinicRoomResult>.Failure(Error.Conflict("ROOM_CODE_EXISTS", $"Room code '{cleanCode}' already exists."));
            }

            room.Update(cleanCode, nameAr, nameEn, specialtyCode, floorOrLocation, isActive);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ClinicRoomResult>.Success(new ClinicRoomResult(
                room.Id, room.ClinicId, room.Code, room.NameAr, room.NameEn, room.SpecialtyCode, room.FloorOrLocation, room.IsActive, room.CreatedAtUtc));
        });

        if (res.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "ClinicRoomUpdated",
                entityName: "ClinicRoom",
                entityId: roomId.ToString(),
                oldValues: null,
                newValues: JsonSerializer.Serialize(new { cleanCode, isActive }),
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return res;
    }

    public async Task<Result> VoidRoomAsync(
        Guid userId,
        Guid clinicId,
        Guid roomId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var res = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var room = await _dbContext.ClinicRooms
                .FirstOrDefaultAsync(r => r.Id == roomId && r.ClinicId == clinicId && !r.IsVoided, cancellationToken);

            if (room == null)
            {
                return Result.Failure(Error.NotFound("ROOM_NOT_FOUND", "Room not found."));
            }

            room.Void(_clock.UtcNow, userId.ToString(), "Voided via structure settings");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        });

        if (res.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "ClinicRoomVoided",
                entityName: "ClinicRoom",
                entityId: roomId.ToString(),
                oldValues: null,
                newValues: null,
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return res;
    }

    public async Task<Result<IReadOnlyList<ClinicDoctorResult>>> ListDoctorsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "dashboard.view", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicDoctorResult>>.Failure(gate.Error);
        }

        var list = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var rows = await _dbContext.ClinicDoctors
                .Where(d => d.ClinicId == clinicId && !d.IsVoided)
                .OrderBy(d => d.FullName)
                .ToListAsync(cancellationToken);

            return rows.Select(MapDoctor).ToList();
        });

        return Result<IReadOnlyList<ClinicDoctorResult>>.Success(list);
    }

    public async Task<Result<ClinicDoctorResult>> CreateDoctorAsync(
        Guid userId,
        Guid clinicId,
        Guid? staffUserId,
        string fullName,
        string titleAr,
        string titleEn,
        IReadOnlyList<string> specialtyCodes,
        IReadOnlyList<Guid> assignedRoomIds,
        int slotDurationMinutes,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicDoctorResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<ClinicDoctorResult>.Failure(Error.Validation("DOCTOR_NAME_REQUIRED", "Doctor full name is required."));
        }

        Guid id = GuidV7.NewGuid();
        var doctor = new ClinicDoctor(
            id,
            clinicId,
            staffUserId,
            fullName,
            titleAr,
            titleEn,
            JsonSerializer.Serialize(specialtyCodes ?? Array.Empty<string>()),
            JsonSerializer.Serialize(assignedRoomIds ?? Array.Empty<Guid>()),
            slotDurationMinutes <= 0 ? 20 : slotDurationMinutes,
            _clock.UtcNow);

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            _dbContext.ClinicDoctors.Add(doctor);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await _auditService.LogAsync(
            action: "ClinicDoctorCreated",
            entityName: "ClinicDoctor",
            entityId: id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { doctor.FullName, slotDurationMinutes = doctor.SlotDurationMinutes }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<ClinicDoctorResult>.Success(MapDoctor(doctor));
    }

    public async Task<Result<ClinicDoctorResult>> UpdateDoctorAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        Guid? staffUserId,
        string fullName,
        string titleAr,
        string titleEn,
        IReadOnlyList<string> specialtyCodes,
        IReadOnlyList<Guid> assignedRoomIds,
        int slotDurationMinutes,
        bool isActive,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicDoctorResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<ClinicDoctorResult>.Failure(Error.Validation("DOCTOR_NAME_REQUIRED", "Doctor full name is required."));
        }

        var res = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var doctor = await _dbContext.ClinicDoctors
                .FirstOrDefaultAsync(d => d.Id == doctorId && d.ClinicId == clinicId && !d.IsVoided, cancellationToken);

            if (doctor == null)
            {
                return Result<ClinicDoctorResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor profile not found."));
            }

            doctor.Update(
                staffUserId,
                fullName,
                titleAr,
                titleEn,
                JsonSerializer.Serialize(specialtyCodes ?? Array.Empty<string>()),
                JsonSerializer.Serialize(assignedRoomIds ?? Array.Empty<Guid>()),
                slotDurationMinutes <= 0 ? 20 : slotDurationMinutes,
                isActive);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<ClinicDoctorResult>.Success(MapDoctor(doctor));
        });

        if (res.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "ClinicDoctorUpdated",
                entityName: "ClinicDoctor",
                entityId: doctorId.ToString(),
                oldValues: null,
                newValues: JsonSerializer.Serialize(new { fullName, isActive }),
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return res;
    }

    public async Task<Result> VoidDoctorAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var res = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var doctor = await _dbContext.ClinicDoctors
                .FirstOrDefaultAsync(d => d.Id == doctorId && d.ClinicId == clinicId && !d.IsVoided, cancellationToken);

            if (doctor == null)
            {
                return Result.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor profile not found."));
            }

            doctor.Void(_clock.UtcNow, userId.ToString(), "Voided via structure settings");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        });

        if (res.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "ClinicDoctorVoided",
                entityName: "ClinicDoctor",
                entityId: doctorId.ToString(),
                oldValues: null,
                newValues: null,
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return res;
    }

    public async Task<Result<IReadOnlyList<ClinicWorkingHourResult>>> GetWorkingHoursAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "dashboard.view", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicWorkingHourResult>>.Failure(gate.Error);
        }

        var rows = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicWorkingHours
                .Where(w => w.ClinicId == clinicId && !w.IsVoided && w.DoctorId == doctorId)
                .OrderBy(w => w.DayOfWeek)
                .Select(w => new ClinicWorkingHourResult(w.Id, w.ClinicId, w.DoctorId, w.DayOfWeek, w.StartTime, w.EndTime, w.IsClosed))
                .ToListAsync(cancellationToken));

        return Result<IReadOnlyList<ClinicWorkingHourResult>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<ClinicWorkingHourResult>>> SaveWorkingHoursAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        IReadOnlyList<WorkingHourInput> items,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicWorkingHourResult>>.Failure(gate.Error);
        }

        foreach (var item in items ?? Array.Empty<WorkingHourInput>())
        {
            if (item.DayOfWeek < 0 || item.DayOfWeek > 6)
            {
                return Result<IReadOnlyList<ClinicWorkingHourResult>>.Failure(Error.Validation(
                    "INVALID_DAY_OF_WEEK",
                    "DayOfWeek must be between 0 and 6."));
            }
        }

        DateTimeOffset now = _clock.UtcNow;
        var saved = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var existing = await _dbContext.ClinicWorkingHours
                .Where(w => w.ClinicId == clinicId && !w.IsVoided && w.DoctorId == doctorId)
                .ToListAsync(cancellationToken);

            foreach (var old in existing)
            {
                old.Void(now, userId.ToString(), "Replaced working schedule");
            }

            var inserted = new List<ClinicWorkingHourResult>();
            foreach (var item in items ?? Array.Empty<WorkingHourInput>())
            {
                var entity = new ClinicWorkingHour(
                    GuidV7.NewGuid(),
                    clinicId,
                    doctorId,
                    item.DayOfWeek,
                    item.StartTime,
                    item.EndTime,
                    item.IsClosed,
                    now);
                _dbContext.ClinicWorkingHours.Add(entity);
                inserted.Add(new ClinicWorkingHourResult(
                    entity.Id, entity.ClinicId, entity.DoctorId, entity.DayOfWeek, entity.StartTime, entity.EndTime, entity.IsClosed));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return inserted.OrderBy(i => i.DayOfWeek).ToList();
        });

        await _auditService.LogAsync(
            action: "WorkingHoursUpdated",
            entityName: "ClinicWorkingHour",
            entityId: doctorId?.ToString() ?? clinicId.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { doctorId, count = saved.Count }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<IReadOnlyList<ClinicWorkingHourResult>>.Success(saved);
    }

    public async Task<Result<IReadOnlyList<ClinicDayOffResult>>> ListDaysOffAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "dashboard.view", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ClinicDayOffResult>>.Failure(gate.Error);
        }

        var rows = await ExecuteForClinicAsync(clinicId, () =>
            _dbContext.ClinicDaysOff
                .Where(d => d.ClinicId == clinicId && !d.IsVoided)
                .OrderBy(d => d.StartDate)
                .Select(d => new ClinicDayOffResult(
                    d.Id, d.ClinicId, d.DoctorId, d.StartDate, d.EndDate, d.ReasonAr, d.ReasonEn, d.CreatedAtUtc))
                .ToListAsync(cancellationToken));

        return Result<IReadOnlyList<ClinicDayOffResult>>.Success(rows);
    }

    public async Task<Result<ClinicDayOffResult>> CreateDayOffAsync(
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
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicDayOffResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
        {
            return Result<ClinicDayOffResult>.Failure(Error.Validation("DATE_REQUIRED", "Start and end dates are required."));
        }

        Guid id = GuidV7.NewGuid();
        var entity = new ClinicDayOff(id, clinicId, doctorId, startDate, endDate, reasonAr, reasonEn, _clock.UtcNow);

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            _dbContext.ClinicDaysOff.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        await _auditService.LogAsync(
            action: "DayOffCreated",
            entityName: "ClinicDayOff",
            entityId: id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { entity.StartDate, entity.EndDate, entity.DoctorId }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<ClinicDayOffResult>.Success(new ClinicDayOffResult(
            entity.Id, entity.ClinicId, entity.DoctorId, entity.StartDate, entity.EndDate, entity.ReasonAr, entity.ReasonEn, entity.CreatedAtUtc));
    }

    public async Task<Result> VoidDayOffAsync(
        Guid userId,
        Guid clinicId,
        Guid dayOffId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "structure.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure(gate.Error);
        }

        var res = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var entity = await _dbContext.ClinicDaysOff
                .FirstOrDefaultAsync(d => d.Id == dayOffId && d.ClinicId == clinicId && !d.IsVoided, cancellationToken);

            if (entity == null)
            {
                return Result.Failure(Error.NotFound("DAY_OFF_NOT_FOUND", "Day off entry not found."));
            }

            entity.Void(_clock.UtcNow, userId.ToString(), "Voided");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        });

        if (res.IsSuccess)
        {
            await _auditService.LogAsync(
                action: "DayOffVoided",
                entityName: "ClinicDayOff",
                entityId: dayOffId.ToString(),
                oldValues: null,
                newValues: null,
                userId: userId,
                clinicId: clinicId,
                ipAddress: ipAddress,
                requestId: requestId,
                cancellationToken: cancellationToken);
        }

        return res;
    }

    public async Task<Result<IReadOnlyList<NumberBlockResult>>> ListNumberBlocksAsync(
        Guid userId,
        Guid clinicId,
        string? kind,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "numberblocks.manage", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<NumberBlockResult>>.Failure(gate.Error);
        }

        var rows = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var query = _dbContext.NumberBlocks.Where(n => n.ClinicId == clinicId);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                string cleanKind = kind.Trim();
                query = query.Where(n => n.Kind == cleanKind);
            }

            return await query
                .OrderBy(n => n.FromNumber)
                .Select(n => new NumberBlockResult(
                    n.Id, n.ClinicId, n.DeviceId, n.Kind, n.FromNumber, n.ToNumber, n.NextNumber, n.Status, n.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        });

        return Result<IReadOnlyList<NumberBlockResult>>.Success(rows);
    }

    public async Task<Result<NumberBlockResult>> ReserveNumberBlockAsync(
        Guid userId,
        Guid clinicId,
        string targetDeviceId,
        string kind,
        int? blockSize,
        string? callerDeviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "numberblocks.manage", null, callerDeviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<NumberBlockResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            return Result<NumberBlockResult>.Failure(Error.Validation("DEVICE_ID_REQUIRED", "Target device ID is required."));
        }

        string normalizedKind;
        if (string.Equals(kind?.Trim(), "Receipt", StringComparison.OrdinalIgnoreCase))
        {
            normalizedKind = "Receipt";
        }
        else if (string.Equals(kind?.Trim(), "PatientFile", StringComparison.OrdinalIgnoreCase))
        {
            normalizedKind = "PatientFile";
        }
        else
        {
            return Result<NumberBlockResult>.Failure(Error.Validation("INVALID_BLOCK_KIND", "Number block kind must be 'Receipt' or 'PatientFile'."));
        }

        int size = blockSize.GetValueOrDefault(NumberBlock.DefaultBlockSize);
        if (size < 1 || size > 10_000)
        {
            return Result<NumberBlockResult>.Failure(Error.Validation("INVALID_BLOCK_SIZE", "Block size must be between 1 and 10000."));
        }

        NumberBlockResult? reserved = null;
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                reserved = await ExecuteForClinicAsync(clinicId, async () =>
                {
                    await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                    string lockResource = $"NumberBlock_{clinicId:N}_{normalizedKind}";
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;",
                        [lockResource],
                        cancellationToken);

                    long? maxTo = await _dbContext.NumberBlocks
                        .Where(n => n.ClinicId == clinicId && n.Kind == normalizedKind)
                        .MaxAsync(n => (long?)n.ToNumber, cancellationToken);

                    long fromNumber = (maxTo ?? 0L) + 1L;
                    long toNumber = fromNumber + size - 1L;

                    var block = new NumberBlock(
                        GuidV7.NewGuid(),
                        clinicId,
                        targetDeviceId.Trim(),
                        normalizedKind,
                        fromNumber,
                        toNumber,
                        _clock.UtcNow);

                    _dbContext.NumberBlocks.Add(block);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return new NumberBlockResult(
                        block.Id, block.ClinicId, block.DeviceId, block.Kind, block.FromNumber, block.ToNumber, block.NextNumber, block.Status, block.CreatedAtUtc);
                });

                break;
            }
            catch (Exception ex) when (attempt < 6 && (ex is DbUpdateException || ex is SqlException || ex.InnerException is DbUpdateException || ex.InnerException is SqlException))
            {
                _dbContext.ChangeTracker.Clear();
                await Task.Delay(attempt * 20, cancellationToken);
            }
        }

        if (reserved == null)
        {
            return Result<NumberBlockResult>.Failure(Error.Conflict("NUMBER_BLOCK_CONTENTION", "Could not reserve number block due to concurrent contention. Please retry."));
        }

        await _auditService.LogAsync(
            action: "NumberBlockReserved",
            entityName: "NumberBlock",
            entityId: reserved.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new { reserved.Kind, reserved.FromNumber, reserved.ToNumber, reserved.DeviceId }),
            userId: userId,
            clinicId: clinicId,
            ipAddress: ipAddress,
            requestId: requestId,
            cancellationToken: cancellationToken);

        return Result<NumberBlockResult>.Success(reserved);
    }

    public async Task<Result<IReadOnlyList<AuditLogItemResult>>> GetClinicAuditLogsAsync(
        Guid userId,
        Guid clinicId,
        string? actionFilter,
        int page,
        int pageSize,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "audit.read", null, deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<AuditLogItemResult>>.Failure(gate.Error);
        }

        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AuditLogs.Where(a => a.ClinicId == clinicId);
        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            string filter = actionFilter.Trim();
            query = query.Where(a => a.Action.Contains(filter));
        }

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(a => new AuditLogItemResult(
                a.Id,
                a.UserId,
                a.ClinicId,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.OldValues,
                a.NewValues,
                a.TimestampUtc,
                a.IpAddress,
                a.RequestId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AuditLogItemResult>>.Success(items);
    }

    private static ClinicDoctorResult MapDoctor(ClinicDoctor d)
    {
        IReadOnlyList<string> specs;
        IReadOnlyList<Guid> rooms;
        try
        {
            specs = JsonSerializer.Deserialize<List<string>>(d.SpecialtyCodesJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            specs = Array.Empty<string>();
        }

        try
        {
            rooms = JsonSerializer.Deserialize<List<Guid>>(d.AssignedRoomIdsJson) ?? new List<Guid>();
        }
        catch (JsonException)
        {
            rooms = Array.Empty<Guid>();
        }

        return new ClinicDoctorResult(
            d.Id,
            d.ClinicId,
            d.UserId,
            d.FullName,
            d.TitleAr,
            d.TitleEn,
            specs,
            rooms,
            d.SlotDurationMinutes,
            d.IsActive,
            d.CreatedAtUtc);
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
}
