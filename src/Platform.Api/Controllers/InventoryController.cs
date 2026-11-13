using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin HTTP transport controller for clinic inventory items, FEFO batches, purchase invoices,
/// manual adjustments, alerts, supplier statements, and settings.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/inventory")]
[Authorize]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
    }

    [HttpGet("items")]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListItems(
        Guid clinicId,
        [FromQuery] string? query,
        [FromQuery] string? specialtyCode,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.ListItemsAsync(userId, clinicId, query, specialtyCode, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(InventoryViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(InventoryItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateItem(
        Guid clinicId,
        [FromBody] UpsertInventoryItemDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpsertInventoryItemInput(
            null,
            dto.Code,
            dto.NameAr,
            dto.NameEn,
            dto.UnitName,
            dto.PackSize,
            dto.UnitCostAmount,
            dto.DefaultMarkupPercent,
            dto.LowStockThreshold,
            dto.SpecialtyCode,
            dto.StorageLocation,
            dto.IsActive,
            dto.OperationId);

        var result = await _inventoryService.UpsertItemAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("items/{itemId:guid}")]
    [ProducesResponseType(typeof(InventoryItemViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(
        Guid clinicId,
        Guid itemId,
        [FromBody] UpsertInventoryItemDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpsertInventoryItemInput(
            itemId,
            dto.Code,
            dto.NameAr,
            dto.NameEn,
            dto.UnitName,
            dto.PackSize,
            dto.UnitCostAmount,
            dto.DefaultMarkupPercent,
            dto.LowStockThreshold,
            dto.SpecialtyCode,
            dto.StorageLocation,
            dto.IsActive,
            dto.OperationId);

        var result = await _inventoryService.UpsertItemAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("batches")]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryItemBatchViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBatches(
        Guid clinicId,
        [FromQuery] Guid? itemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.ListBatchesAsync(userId, clinicId, itemId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(InventoryViewModelMapper.ToViewModel).ToList());
    }

    [HttpGet("movements")]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMovements(
        Guid clinicId,
        [FromQuery] Guid? itemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.ListMovementsAsync(userId, clinicId, itemId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(InventoryViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("purchase-invoices")]
    [ProducesResponseType(typeof(PurchaseInvoiceViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePurchaseInvoice(
        Guid clinicId,
        [FromBody] ReceivePurchaseInvoiceDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var lines = dto.Lines
            .Select(l => new PurchaseInvoiceLineInput(
                l.ItemId, l.PackSize, l.PackCount, l.PackPriceAmount, l.BatchNumber, l.ExpiryDate))
            .ToList();

        var input = new ReceivePurchaseInvoiceInput(
            dto.Id,
            dto.SupplierId,
            dto.SupplierName,
            dto.InvoiceNumber,
            dto.InvoiceDate,
            dto.PaidAmount,
            dto.PaymentMethod,
            dto.Notes,
            lines,
            dto.OperationId);

        var result = await _inventoryService.ReceivePurchaseInvoiceAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("purchase-invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseInvoiceViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPurchaseInvoices(
        Guid clinicId,
        [FromQuery] Guid? supplierId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.ListPurchaseInvoicesAsync(userId, clinicId, supplierId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(InventoryViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("adjustments")]
    [ProducesResponseType(typeof(StockMovementViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustStock(
        Guid clinicId,
        [FromBody] AdjustStockDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new AdjustStockInput(dto.ItemId, dto.BatchId, dto.QuantityDelta, dto.Reason, dto.OperationId);
        var result = await _inventoryService.AdjustStockAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("alerts")]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryAlertViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAlerts(
        Guid clinicId,
        [FromQuery] bool includeResolved = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.ListAlertsAsync(userId, clinicId, includeResolved, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(InventoryViewModelMapper.ToViewModel).ToList());
    }

    [HttpPost("alerts/{alertId:guid}/resolve")]
    [ProducesResponseType(typeof(InventoryAlertViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveAlert(
        Guid clinicId,
        Guid alertId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.ResolveAlertAsync(
            userId, clinicId, alertId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ClinicInventorySettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.GetSettingsAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ClinicInventorySettingViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid clinicId,
        [FromBody] UpdateClinicInventorySettingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var input = new UpdateClinicInventorySettingInput(dto.NegativeStockPolicy, dto.NearExpiryWarningDays, dto.OperationId);
        var result = await _inventoryService.UpdateSettingsAsync(
            userId, clinicId, input, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
    }

    [HttpGet("suppliers/{supplierId:guid}/statement")]
    [ProducesResponseType(typeof(SupplierInventoryStatementViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierStatement(
        Guid clinicId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _inventoryService.GetSupplierStatementAsync(userId, clinicId, supplierId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(InventoryViewModelMapper.ToViewModel(result.Value));
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

