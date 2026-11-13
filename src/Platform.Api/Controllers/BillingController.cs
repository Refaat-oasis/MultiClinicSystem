using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for Phase 8 billing, ledger, payments, receipts, refunds, lab work,
/// finance-lite suppliers/expenses, and daily cash closing.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/billing")]
[Authorize]
public sealed class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService ?? throw new ArgumentNullException(nameof(billingService));
    }

    [HttpGet("price-list")]
    [ProducesResponseType(typeof(IReadOnlyList<PriceListItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPriceList(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.ListPriceListAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("price-list")]
    [ProducesResponseType(typeof(PriceListItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertPriceListItem(
        Guid clinicId,
        [FromBody] UpsertPriceListItemDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpsertPriceListItemInput(
            dto.Id, dto.PriceCode, dto.NameAr, dto.NameEn, dto.Kind, dto.UnitCostAmount, dto.DefaultPriceAmount, dto.IsActive, dto.OperationId);
        var result = await _billingService.UpsertPriceListItemAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("charges")]
    [ProducesResponseType(typeof(IReadOnlyList<ChargeViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCharges(Guid clinicId, [FromQuery] Guid? patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.ListChargesAsync(userId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("charges")]
    [ProducesResponseType(typeof(ChargeViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCharge(
        Guid clinicId,
        [FromBody] CreateChargeDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var lines = dto.Lines?
            .Select(l => new ChargeLineInput(l.Kind, l.PriceListItemId, l.InventoryItemId, l.Description, l.Quantity, l.UnitCostAmount, l.UnitChargedPriceAmount))
            .ToList();

        var input = new CreateChargeInput(
            dto.Id, dto.PatientId, dto.VisitId, dto.AppointmentId, dto.DoctorId,
            dto.StandardSessionPriceAmount, dto.IsFreeSession, dto.FreeSessionReason,
            dto.CustomSessionPriceAmount, dto.CustomPriceReason, dto.DiscountPercent, dto.DiscountFixedAmount,
            dto.DiscountReason, dto.ExtraChargeAmount, dto.ExtraChargeReason, lines, dto.Notes, dto.PostImmediately, dto.OperationId);

        var result = await _billingService.CreateChargeAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("charges/{chargeId:guid}/post")]
    [ProducesResponseType(typeof(ChargeViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostDraftCharge(Guid clinicId, Guid chargeId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.PostDraftChargeAsync(userId, clinicId, chargeId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("payments")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPayment(
        Guid clinicId,
        [FromBody] RecordPaymentDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var splits = dto.Splits.Select(s => new PaymentSplitInput(s.Method, s.Amount, s.Notes)).ToList();
        var input = new RecordPaymentInput(dto.Id, dto.PatientId, dto.ChargeId, dto.IsDeposit, splits, dto.OperationId);

        var result = await _billingService.RecordPaymentAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("payments/{paymentId:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadReceiptPdf(Guid clinicId, Guid paymentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.GenerateReceiptPdfAsync(userId, clinicId, paymentId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : File(result.Value.Bytes, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("refunds")]
    [ProducesResponseType(typeof(RefundViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRefund(
        Guid clinicId,
        [FromBody] CreateRefundDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreateRefundInput(dto.Id, dto.PatientId, dto.OriginalPaymentId, dto.ChargeId, dto.Method, dto.Amount, dto.Reason, dto.OperationId);
        var result = await _billingService.CreateRefundAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("skipped-receipts")]
    [ProducesResponseType(typeof(SkippedReceiptNumberViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordSkippedReceipt(
        Guid clinicId,
        [FromBody] RecordSkippedReceiptDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new RecordSkippedReceiptInput(dto.ReceiptNumber, dto.Reason, dto.OperationId);
        var result = await _billingService.RecordSkippedReceiptAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("skipped-receipts")]
    [ProducesResponseType(typeof(IReadOnlyList<SkippedReceiptNumberViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSkippedReceipts(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.ListSkippedReceiptsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("patients/{patientId:guid}/ledger")]
    [ProducesResponseType(typeof(PatientLedgerViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientLedger(Guid clinicId, Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.GetPatientLedgerAsync(userId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("lab-work")]
    [ProducesResponseType(typeof(LabWorkBillingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateLabWork(
        Guid clinicId,
        [FromBody] CreateLabWorkBillingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreateLabWorkBillingInput(
            dto.Id, dto.PatientId, dto.VisitId, dto.DoctorId, dto.SupplierId, dto.LabName,
            dto.WorkDescription, dto.ToothReference, dto.LabCostAmount, dto.PatientPriceAmount, dto.ImmediateExpenseMethod, dto.OperationId);
        var result = await _billingService.CreateLabWorkAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("lab-work")]
    [ProducesResponseType(typeof(IReadOnlyList<LabWorkBillingViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLabWork(Guid clinicId, [FromQuery] Guid? patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.ListLabWorkAsync(userId, clinicId, patientId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("suppliers")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSuppliers(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.ListSuppliersAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("suppliers")]
    [ProducesResponseType(typeof(SupplierViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSupplier(
        Guid clinicId,
        [FromBody] CreateSupplierDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreateSupplierInput(dto.Id, dto.Name, dto.Type, dto.Phone, dto.Notes, dto.OperationId);
        var result = await _billingService.CreateSupplierAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("suppliers/payments")]
    [ProducesResponseType(typeof(SupplierPaymentViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordSupplierPayment(
        Guid clinicId,
        [FromBody] RecordSupplierPaymentDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new RecordSupplierPaymentInput(dto.Id, dto.SupplierId, dto.Method, dto.Amount, dto.ReferenceNote, dto.OperationId);
        var result = await _billingService.RecordSupplierPaymentAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("expenses")]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExpenses(Guid clinicId, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _billingService.ListExpensesAsync(userId, clinicId, date, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(BillingViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("expenses")]
    [ProducesResponseType(typeof(ExpenseViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateExpense(
        Guid clinicId,
        [FromBody] CreateExpenseDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CreateExpenseInput(dto.Id, dto.Category, dto.Description, dto.Method, dto.Amount, dto.SupplierId, dto.CountsAsCashOut, dto.OccurredAtUtc, dto.OperationId);
        var result = await _billingService.CreateExpenseAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("closing")]
    [ProducesResponseType(typeof(DailyClosingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClosingPreview(Guid clinicId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        DateOnly effectiveDate = date == default ? DateOnly.FromDateTime(DateTime.UtcNow) : date;
        var result = await _billingService.GetDailyClosingPreviewAsync(userId, clinicId, effectiveDate, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("closing/close")]
    [ProducesResponseType(typeof(DailyClosingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseDay(
        Guid clinicId,
        [FromBody] CloseBillingDayDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new CloseDayInput(dto.ClosingDate, dto.CountedByMethod, dto.CloseNotes, dto.OperationId);
        var result = await _billingService.CloseDayAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPost("closing/reopen")]
    [ProducesResponseType(typeof(DailyClosingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReopenDay(
        Guid clinicId,
        [FromBody] ReopenBillingDayDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new ReopenDayInput(dto.ClosingDate, dto.Reason, dto.OperationId);
        var result = await _billingService.ReopenDayAsync(userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(BillingViewModelMapper.ToViewModel(result.Value));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private string? GetDeviceId() => Request.Headers["X-Device-Id"].FirstOrDefault();
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetRequestId() => HttpContext.TraceIdentifier;
}
