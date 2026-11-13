using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller for clinic rooms, doctors, working hours, days off, atomic number blocks, and owner audit viewer.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}")]
[Authorize]
public sealed class ClinicStructureController : ControllerBase
{
    private readonly IClinicStructureService _structureService;

    public ClinicStructureController(IClinicStructureService structureService)
    {
        _structureService = structureService ?? throw new ArgumentNullException(nameof(structureService));
    }

    [HttpGet("rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicRoomViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRooms(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.ListRoomsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapRoom).ToList());
    }

    [HttpPost("rooms")]
    [ProducesResponseType(typeof(ClinicRoomViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRoom(Guid clinicId, [FromBody] CreateClinicRoomDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.CreateRoomAsync(
            userId, clinicId, dto.Code, dto.NameAr, dto.NameEn, dto.SpecialtyCode, dto.FloorOrLocation,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/rooms/{result.Value.Id}", MapRoom(result.Value));
    }

    [HttpPut("rooms/{roomId:guid}")]
    [ProducesResponseType(typeof(ClinicRoomViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRoom(Guid clinicId, Guid roomId, [FromBody] UpdateClinicRoomDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.UpdateRoomAsync(
            userId, clinicId, roomId, dto.Code, dto.NameAr, dto.NameEn, dto.SpecialtyCode, dto.FloorOrLocation, dto.IsActive,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapRoom(result.Value));
    }

    [HttpDelete("rooms/{roomId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidRoom(Guid clinicId, Guid roomId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.VoidRoomAsync(userId, clinicId, roomId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم إلغاء الغرفة بنجاح. / Room voided."));
    }

    [HttpGet("doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicDoctorViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDoctors(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.ListDoctorsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapDoctor).ToList());
    }

    [HttpPost("doctors")]
    [ProducesResponseType(typeof(ClinicDoctorViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDoctor(Guid clinicId, [FromBody] CreateClinicDoctorDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.CreateDoctorAsync(
            userId, clinicId, dto.UserId, dto.FullName, dto.TitleAr, dto.TitleEn,
            dto.SpecialtyCodes, dto.AssignedRoomIds, dto.SlotDurationMinutes,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/doctors/{result.Value.Id}", MapDoctor(result.Value));
    }

    [HttpPut("doctors/{doctorId:guid}")]
    [ProducesResponseType(typeof(ClinicDoctorViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDoctor(Guid clinicId, Guid doctorId, [FromBody] UpdateClinicDoctorDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.UpdateDoctorAsync(
            userId, clinicId, doctorId, dto.UserId, dto.FullName, dto.TitleAr, dto.TitleEn,
            dto.SpecialtyCodes, dto.AssignedRoomIds, dto.SlotDurationMinutes, dto.IsActive,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapDoctor(result.Value));
    }

    [HttpDelete("doctors/{doctorId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidDoctor(Guid clinicId, Guid doctorId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.VoidDoctorAsync(userId, clinicId, doctorId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم إلغاء ملف الطبيب بنجاح. / Doctor voided."));
    }

    [HttpGet("working-hours")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicWorkingHourViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkingHours(Guid clinicId, [FromQuery] Guid? doctorId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.GetWorkingHoursAsync(userId, clinicId, doctorId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapWorkingHour).ToList());
    }

    [HttpPut("working-hours")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicWorkingHourViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveWorkingHours(Guid clinicId, [FromBody] SaveWorkingHoursDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var inputs = (dto.Items ?? new List<WorkingHourItemDto>())
            .Select(i => new WorkingHourInput(i.DayOfWeek, i.StartTime, i.EndTime, i.IsClosed))
            .ToList();

        var result = await _structureService.SaveWorkingHoursAsync(
            userId, clinicId, dto.DoctorId, inputs, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapWorkingHour).ToList());
    }

    [HttpGet("days-off")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicDayOffViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDaysOff(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.ListDaysOffAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapDayOff).ToList());
    }

    [HttpPost("days-off")]
    [ProducesResponseType(typeof(ClinicDayOffViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDayOff(Guid clinicId, [FromBody] CreateClinicDayOffDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.CreateDayOffAsync(
            userId, clinicId, dto.DoctorId, dto.StartDate, dto.EndDate, dto.ReasonAr, dto.ReasonEn,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/days-off/{result.Value.Id}", MapDayOff(result.Value));
    }

    [HttpDelete("days-off/{dayOffId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidDayOff(Guid clinicId, Guid dayOffId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.VoidDayOffAsync(userId, clinicId, dayOffId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم إلغاء الإجازة بنجاح. / Day off voided."));
    }

    [HttpGet("number-blocks")]
    [ProducesResponseType(typeof(IReadOnlyList<NumberBlockViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListNumberBlocks(Guid clinicId, [FromQuery] string? kind, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.ListNumberBlocksAsync(userId, clinicId, kind, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapBlock).ToList());
    }

    [HttpPost("number-blocks/reserve")]
    [ProducesResponseType(typeof(NumberBlockViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> ReserveNumberBlock(Guid clinicId, [FromBody] ReserveNumberBlockDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string targetDevice = string.IsNullOrWhiteSpace(dto.DeviceId) ? (GetDeviceId() ?? "web-browser") : dto.DeviceId;
        var result = await _structureService.ReserveNumberBlockAsync(
            userId, clinicId, targetDevice, dto.Kind, dto.BlockSize, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/number-blocks/{result.Value.Id}", MapBlock(result.Value));
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClinicAuditLogs(
        Guid clinicId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _structureService.GetClinicAuditLogsAsync(
            userId, clinicId, action, page, pageSize, GetDeviceId(), cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(a => new AuditLogItemViewModel(
            a.Id, a.UserId, a.ClinicId, a.Action, a.EntityName, a.EntityId,
            a.OldValues, a.NewValues, a.TimestampUtc, a.IpAddress, a.RequestId)).ToList());
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private string? GetDeviceId() => User.FindFirst("deviceId")?.Value;
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetRequestId() => HttpContext.Items["RequestId"]?.ToString();

    private static ClinicRoomViewModel MapRoom(ClinicRoomResult r) => new(
        r.Id, r.ClinicId, r.Code, r.NameAr, r.NameEn, r.SpecialtyCode, r.FloorOrLocation, r.IsActive, r.CreatedAtUtc);

    private static ClinicDoctorViewModel MapDoctor(ClinicDoctorResult d) => new(
        d.Id, d.ClinicId, d.UserId, d.FullName, d.TitleAr, d.TitleEn, d.SpecialtyCodes, d.AssignedRoomIds, d.SlotDurationMinutes, d.IsActive, d.CreatedAtUtc);

    private static ClinicWorkingHourViewModel MapWorkingHour(ClinicWorkingHourResult w) => new(
        w.Id, w.ClinicId, w.DoctorId, w.DayOfWeek, w.StartTime, w.EndTime, w.IsClosed);

    private static ClinicDayOffViewModel MapDayOff(ClinicDayOffResult d) => new(
        d.Id, d.ClinicId, d.DoctorId, d.StartDate, d.EndDate, d.ReasonAr, d.ReasonEn, d.CreatedAtUtc);

    private static NumberBlockViewModel MapBlock(NumberBlockResult n) => new(
        n.Id, n.ClinicId, n.DeviceId, n.Kind, n.FromNumber, n.ToNumber, n.NextNumber, n.Status, n.CreatedAtUtc);
}
