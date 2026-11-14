using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for clinic scheduling, availability queries,
/// waiting queue management, waiting list, and slot offers.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}")]
[Authorize]
public sealed class SchedulingController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;
    private readonly IAvailabilityQueries _availabilityQueries;

    public SchedulingController(
        ISchedulingService schedulingService,
        IAvailabilityQueries availabilityQueries)
    {
        _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
        _availabilityQueries = availabilityQueries ?? throw new ArgumentNullException(nameof(availabilityQueries));
    }

    [HttpGet("scheduling/settings")]
    [ProducesResponseType(typeof(ClinicSchedulingSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.GetSchedulingSettingsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("scheduling/settings")]
    [ProducesResponseType(typeof(ClinicSchedulingSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid clinicId,
        [FromBody] UpdateClinicSchedulingSettingsDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.UpdateSchedulingSettingsAsync(
            userId, clinicId, dto.VipJumpLimit, dto.LateGraceMinutes, dto.MaxQueuePushes,
            dto.DefaultSlotStepMinutes, dto.MinBookingLeadMinutes, dto.RescheduleOptionsJson,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("scheduling/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentCategoryViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.ListCategoriesAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SchedulingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("scheduling/categories")]
    [ProducesResponseType(typeof(AppointmentCategoryViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCategory(
        Guid clinicId,
        [FromBody] UpsertAppointmentCategoryDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.CreateCategoryAsync(
            userId, clinicId, dto.SpecialtyCode, dto.NameAr, dto.NameEn,
            dto.DefaultDurationMinutes, dto.DefaultPriceAmount, dto.DefaultPriceCurrency,
            dto.BookableOnline, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : CreatedAtAction(nameof(ListCategories), new { clinicId }, SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("scheduling/categories/{categoryId:guid}")]
    [ProducesResponseType(typeof(AppointmentCategoryViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCategory(
        Guid clinicId,
        Guid categoryId,
        [FromBody] UpsertAppointmentCategoryDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.UpdateCategoryAsync(
            userId, clinicId, categoryId, dto.SpecialtyCode, dto.NameAr, dto.NameEn,
            dto.DefaultDurationMinutes, dto.DefaultPriceAmount, dto.DefaultPriceCurrency,
            dto.BookableOnline, dto.IsActive, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("scheduling/doctors/{doctorId:guid}/booking-settings")]
    [ProducesResponseType(typeof(DoctorBookingSettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDoctorBookingSetting(
        Guid clinicId,
        Guid doctorId,
        [FromBody] UpdateDoctorBookingSettingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.UpdateDoctorBookingSettingAsync(
            userId, clinicId, doctorId, dto.AcceptsOnlineBooking, dto.MaxDailyOnlineBookings,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("scheduling/free-slots")]
    [ProducesResponseType(typeof(IReadOnlyList<FreeSlotItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFreeSlots(
        Guid clinicId,
        [FromQuery] Guid categoryId,
        [FromQuery] Guid? doctorId,
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _availabilityQueries.GetFreeSlotsAsync(
            userId, clinicId, categoryId, doctorId, fromUtc, toUtc, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SchedulingViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("scheduling/appointments")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAppointments(
        Guid clinicId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? doctorId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.ListAppointmentsAsync(
            userId, clinicId, fromUtc, toUtc, doctorId, status, GetDeviceId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SchedulingViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("scheduling/appointments/{appointmentId:guid}")]
    [ProducesResponseType(typeof(AppointmentItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointment(Guid clinicId, Guid appointmentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.GetAppointmentAsync(userId, clinicId, appointmentId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("scheduling/appointments")]
    [ProducesResponseType(typeof(AppointmentItemViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> BookAppointment(
        Guid clinicId,
        [FromBody] BookAppointmentDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new BookAppointmentInput(
            dto.Id, dto.PatientId, dto.DoctorId, dto.RoomId, dto.CategoryId,
            dto.StartUtc, dto.EndUtc, dto.Priority, dto.PlanSessionId, dto.Notes, dto.OperationId);

        var result = await _schedulingService.BookAppointmentAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : CreatedAtAction(nameof(GetAppointment), new { clinicId, appointmentId = result.Value.Id }, SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("scheduling/appointments/{appointmentId:guid}/status")]
    [ProducesResponseType(typeof(AppointmentItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> TransitionStatus(
        Guid clinicId,
        Guid appointmentId,
        [FromBody] TransitionAppointmentStatusDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.TransitionAppointmentStatusAsync(
            userId, clinicId, appointmentId, dto.NextStatus, dto.Reason,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("scheduling/appointments/{appointmentId:guid}/reschedule")]
    [ProducesResponseType(typeof(AppointmentItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reschedule(
        Guid clinicId,
        Guid appointmentId,
        [FromBody] RescheduleAppointmentDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new RescheduleAppointmentInput(
            dto.PresetKey, dto.CustomStartUtc, dto.CustomEndUtc, dto.NewDoctorId, dto.NewRoomId, dto.OperationId);

        var result = await _schedulingService.RescheduleAppointmentAsync(
            userId, clinicId, appointmentId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("scheduling/close-day")]
    [ProducesResponseType(typeof(CloseDayViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseDay(Guid clinicId, [FromBody] CloseDayDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.CloseDayAsync(
            userId, clinicId, dto.DoctorId, dto.StartDate, dto.EndDate, dto.ReasonAr, dto.ReasonEn,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("scheduling/doctors/{doctorId:guid}/timetable")]
    [ProducesResponseType(typeof(DoctorTimetableViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorTimetable(
        Guid clinicId,
        Guid doctorId,
        [FromQuery] string date,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.GetDoctorTimetableAsync(userId, clinicId, doctorId, date, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("queue/board")]
    [ProducesResponseType(typeof(IReadOnlyList<QueueEntryItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueBoard(
        Guid clinicId,
        [FromQuery] Guid? doctorId,
        [FromQuery] string? date,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.GetQueueBoardAsync(userId, clinicId, doctorId, date, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SchedulingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("queue/walk-in")]
    [ProducesResponseType(typeof(QueueEntryItemViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> EnqueueWalkIn(
        Guid clinicId,
        [FromBody] EnqueueWalkInDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.EnqueueWalkInAsync(
            userId, clinicId, dto.PatientId, dto.DoctorId, dto.Priority, dto.OperationId,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : StatusCode(StatusCodes.Status201Created, SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("queue/entries/{queueEntryId:guid}/push-late")]
    [ProducesResponseType(typeof(QueueEntryItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> PushLate(
        Guid clinicId,
        Guid queueEntryId,
        [FromBody] PushLateQueueEntryDto? dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.PushLateQueueEntryAsync(
            userId, clinicId, queueEntryId, dto?.MarkNoShowOnExceed ?? true,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("queue/entries/{queueEntryId:guid}/call-next")]
    [ProducesResponseType(typeof(QueueEntryItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CallNext(Guid clinicId, Guid queueEntryId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.CallNextQueueEntryAsync(
            userId, clinicId, queueEntryId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("queue/waiting-list")]
    [ProducesResponseType(typeof(IReadOnlyList<WaitingListItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWaitingList(
        Guid clinicId,
        [FromQuery] string? date,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.ListWaitingListAsync(userId, clinicId, date, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SchedulingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("queue/waiting-list")]
    [ProducesResponseType(typeof(WaitingListItemViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddToWaitingList(
        Guid clinicId,
        [FromBody] AddToWaitingListDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.AddToWaitingListAsync(
            userId, clinicId, dto.PatientId, dto.DoctorId, dto.CategoryId, dto.PreferredDate,
            dto.Notes, dto.OperationId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : StatusCode(StatusCodes.Status201Created, SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("queue/slot-offers")]
    [ProducesResponseType(typeof(IReadOnlyList<SlotOfferItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSlotOffers(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.ListSlotOffersAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(SchedulingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("queue/slot-offers/{offerId:guid}/accept")]
    [ProducesResponseType(typeof(AppointmentItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptSlotOffer(Guid clinicId, Guid offerId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _schedulingService.AcceptSlotOfferAsync(
            userId, clinicId, offerId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(SchedulingViewModelMapper.ToViewModel(result.Value));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }

    private string? GetDeviceId() => User.FindFirstValue("device_id");

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetRequestId() => HttpContext.TraceIdentifier;
}
