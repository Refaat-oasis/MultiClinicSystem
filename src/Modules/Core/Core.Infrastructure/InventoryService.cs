using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 12 inventory catalog, pack-based purchase invoicing, FEFO batch consumption,
/// negative stock policy enforcement, supplier statement reconciliation, alerts, and inspect queries.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IClock _clock;
    private readonly IBillingService? _billingService;
    private readonly IScheduleRealtimeNotifier? _realtimeNotifier;

    public InventoryService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IAuditService auditService,
        IPlatformAccess platformAccess,
        IClock clock,
        IBillingService? billingService = null,
        IScheduleRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _platformAccess = platformAccess ?? throw new ArgumentNullException(nameof(platformAccess));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _billingService = billingService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result<IReadOnlyList<InventoryItemResult>>> ListItemsAsync(
        Guid userId,
        Guid clinicId,
        string? query,
        string? specialtyCode,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<InventoryItemResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<InventoryItem> dbQuery = _dbContext.InventoryItems
                .AsNoTracking()
                .Where(i => i.ClinicId == clinicId && !i.IsVoided);

            if (!string.IsNullOrWhiteSpace(specialtyCode))
            {
                string spec = specialtyCode.Trim().ToLowerInvariant();
                dbQuery = dbQuery.Where(i => i.SpecialtyCode == spec);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                string norm = ArabicNormalizer.NormalizeText(query);
                string upper = query.Trim().ToUpperInvariant();
                dbQuery = dbQuery.Where(i =>
                    i.Code.Contains(upper) ||
                    i.NormalizedSearchName.Contains(norm) ||
                    i.NormalizedSearchName.Contains(upper));
            }

            List<InventoryItem> rows = await dbQuery
                .OrderBy(i => i.Code)
                .Take(200)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<InventoryItemResult>>.Success(rows.Select(MapItem).ToList());
        });
    }

    public async Task<Result<InventoryItemResult>> UpsertItemAsync(
        Guid userId,
        Guid clinicId,
        UpsertInventoryItemInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.write", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<InventoryItemResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.NameAr) || string.IsNullOrWhiteSpace(input.NameEn))
        {
            return Result<InventoryItemResult>.Failure(Error.Validation("INVENTORY_ITEM_INVALID", "Item code, Arabic name, and English name are required."));
        }

        if (input.PackSize <= 0 || input.UnitCostAmount < 0m || input.DefaultMarkupPercent < 0m || input.LowStockThreshold < 0m)
        {
            return Result<InventoryItemResult>.Failure(Error.Validation("INVENTORY_ITEM_INVALID", "Pack size must be positive and costs/markup/threshold cannot be negative."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<InventoryItemResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    InventoryItem? existingItem = await _dbContext.InventoryItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.ClinicId == clinicId && i.Id == existingOp.ResultEntityId, cancellationToken);
                    if (existingItem != null)
                    {
                        return Result<InventoryItemResult>.Success(MapItem(existingItem));
                    }
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            string codeNorm = input.Code.Trim().ToUpperInvariant();
            Money unitCost = new(input.UnitCostAmount, clinic.Currency);

            InventoryItem? item = null;
            if (input.Id.HasValue && input.Id.Value != Guid.Empty)
            {
                item = await _dbContext.InventoryItems
                    .FirstOrDefaultAsync(i => i.ClinicId == clinicId && !i.IsVoided && i.Id == input.Id.Value, cancellationToken);
            }

            if (item == null)
            {
                bool duplicateCode = await _dbContext.InventoryItems
                    .AsNoTracking()
                    .AnyAsync(i => i.ClinicId == clinicId && !i.IsVoided && i.Code == codeNorm, cancellationToken);
                if (duplicateCode)
                {
                    return Result<InventoryItemResult>.Failure(Error.Conflict("DUPLICATE_ITEM_CODE", $"Inventory item code '{codeNorm}' already exists."));
                }

                item = new InventoryItem(
                    input.Id ?? GuidV7.NewGuid(),
                    clinicId,
                    codeNorm,
                    input.NameAr,
                    input.NameEn,
                    input.UnitName,
                    input.PackSize,
                    unitCost,
                    input.DefaultMarkupPercent,
                    input.LowStockThreshold,
                    input.SpecialtyCode,
                    input.StorageLocation,
                    input.OperationId,
                    now);
                _dbContext.InventoryItems.Add(item);
            }
            else
            {
                item.UpdateMetadata(
                    input.NameAr,
                    input.NameEn,
                    input.UnitName,
                    input.PackSize,
                    unitCost,
                    input.DefaultMarkupPercent,
                    input.LowStockThreshold,
                    input.SpecialtyCode,
                    input.StorageLocation,
                    input.IsActive,
                    now);
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "UpsertInventoryItem", item.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "InventoryItemUpserted",
                "InventoryItem",
                item.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { item.Id, item.Code, item.PackSize, item.UnitCostAmount, item.DefaultMarkupPercent, item.LowStockThreshold }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<InventoryItemResult>.Success(MapItem(item));
        });
    }

    public async Task<Result<IReadOnlyList<InventoryItemBatchResult>>> ListBatchesAsync(
        Guid userId,
        Guid clinicId,
        Guid? itemId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<InventoryItemBatchResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<IReadOnlyList<InventoryItemBatchResult>>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            ClinicInventorySetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, _clock.UtcNow, cancellationToken);
            DateOnly todayLocal = GetClinicToday(clinic.TimeZone, _clock.UtcNow);

            IQueryable<InventoryItemBatch> query = _dbContext.InventoryItemBatches
                .AsNoTracking()
                .Where(b => b.ClinicId == clinicId && !b.IsVoided);
            if (itemId.HasValue && itemId.Value != Guid.Empty)
            {
                query = query.Where(b => b.ItemId == itemId.Value);
            }

            List<InventoryItemBatch> batches = await query
                .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(b => b.ExpiryDate)
                .Take(300)
                .ToListAsync(cancellationToken);

            var itemMap = await LoadItemLookupAsync(clinicId, batches.Select(b => b.ItemId), cancellationToken);
            var results = batches.Select(b => MapBatch(b, itemMap.GetValueOrDefault(b.ItemId), todayLocal, settings.NearExpiryWarningDays)).ToList();
            return Result<IReadOnlyList<InventoryItemBatchResult>>.Success(results);
        });
    }

    public async Task<Result<IReadOnlyList<StockMovementResult>>> ListMovementsAsync(
        Guid userId,
        Guid clinicId,
        Guid? itemId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<StockMovementResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<StockMovement> query = _dbContext.StockMovements
                .AsNoTracking()
                .Where(m => m.ClinicId == clinicId && !m.IsVoided);
            if (itemId.HasValue && itemId.Value != Guid.Empty)
            {
                query = query.Where(m => m.ItemId == itemId.Value);
            }

            List<StockMovement> rows = await query
                .OrderByDescending(m => m.OccurredAtUtc)
                .Take(300)
                .ToListAsync(cancellationToken);

            var itemMap = await LoadItemLookupAsync(clinicId, rows.Select(m => m.ItemId), cancellationToken);
            var results = rows.Select(m => MapMovement(m, itemMap.GetValueOrDefault(m.ItemId))).ToList();
            return Result<IReadOnlyList<StockMovementResult>>.Success(results);
        });
    }

    public async Task<Result<PurchaseInvoiceResult>> ReceivePurchaseInvoiceAsync(
        Guid userId,
        Guid clinicId,
        ReceivePurchaseInvoiceInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.write", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PurchaseInvoiceResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.InvoiceNumber) || string.IsNullOrWhiteSpace(input.SupplierName) || input.Lines == null || input.Lines.Count == 0)
        {
            return Result<PurchaseInvoiceResult>.Failure(Error.Validation("PURCHASE_INVOICE_INVALID", "Invoice number, supplier name, and at least one line are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<PurchaseInvoiceResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    PurchaseInvoice? prior = await _dbContext.PurchaseInvoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == existingOp.ResultEntityId, cancellationToken);
                    if (prior != null)
                    {
                        return Result<PurchaseInvoiceResult>.Success(await ProjectInvoiceAsync(clinicId, prior, cancellationToken));
                    }
                }
            }

            string normalizedInvoiceNumber = input.InvoiceNumber.Trim();
            bool duplicateNumber = await _dbContext.PurchaseInvoices
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.InvoiceNumber == normalizedInvoiceNumber, cancellationToken);
            if (duplicateNumber)
            {
                return Result<PurchaseInvoiceResult>.Failure(Error.Conflict("DUPLICATE_INVOICE_NUMBER", $"Purchase invoice '{normalizedInvoiceNumber}' already exists."));
            }

            List<Guid> itemIds = input.Lines.Select(l => l.ItemId).Distinct().ToList();
            List<InventoryItem> items = await _dbContext.InventoryItems
                .Where(i => i.ClinicId == clinicId && !i.IsVoided && itemIds.Contains(i.Id))
                .ToListAsync(cancellationToken);
            Dictionary<Guid, InventoryItem> itemsById = items.ToDictionary(i => i.Id);

            foreach (PurchaseInvoiceLineInput line in input.Lines)
            {
                if (!itemsById.TryGetValue(line.ItemId, out InventoryItem? itemForValidation))
                {
                    return Result<PurchaseInvoiceResult>.Failure(Error.NotFound("INVENTORY_ITEM_NOT_FOUND", $"Inventory item '{line.ItemId}' was not found."));
                }

                if (line.PackCount <= 0 || line.PackPriceAmount < 0m || string.IsNullOrWhiteSpace(line.BatchNumber))
                {
                    return Result<PurchaseInvoiceResult>.Failure(Error.Validation("PURCHASE_LINE_INVALID", "Each line requires positive pack count, non-negative pack price, and a batch number."));
                }

                int effectivePackSize = line.PackSize ?? itemForValidation.PackSize;
                if (effectivePackSize <= 0)
                {
                    return Result<PurchaseInvoiceResult>.Failure(Error.Validation("PURCHASE_LINE_INVALID", "Pack size must be positive."));
                }
            }

            decimal totalInvoiceAmount = input.Lines.Sum(l => decimal.Round(l.PackPriceAmount * l.PackCount, 2, MidpointRounding.AwayFromZero));
            if (totalInvoiceAmount <= 0m || input.PaidAmount < 0m || input.PaidAmount > totalInvoiceAmount)
            {
                return Result<PurchaseInvoiceResult>.Failure(Error.Validation("PURCHASE_PAYMENT_INVALID", "Paid amount must be between 0 and the total invoice amount."));
            }

            DateTimeOffset now = _clock.UtcNow;
            DateOnly todayLocal = GetClinicToday(clinic.TimeZone, now);
            ClinicInventorySetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, now, cancellationToken);
            Guid invoiceId = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();

            var invoice = new PurchaseInvoice(
                invoiceId,
                clinicId,
                input.SupplierId,
                input.SupplierName,
                normalizedInvoiceNumber,
                input.InvoiceDate,
                new Money(totalInvoiceAmount, clinic.Currency),
                new Money(input.PaidAmount, clinic.Currency),
                input.Notes,
                userId.ToString(),
                input.OperationId,
                now);
            _dbContext.PurchaseInvoices.Add(invoice);

            var createdAlerts = new List<InventoryAlert>();
            foreach (PurchaseInvoiceLineInput lineInput in input.Lines)
            {
                InventoryItem item = itemsById[lineInput.ItemId];
                int effectivePackSize = lineInput.PackSize ?? item.PackSize;
                decimal unitCost = InventoryFefoAllocator.CalculatePackUnitCost(lineInput.PackPriceAmount, effectivePackSize, lineInput.PackCount);
                decimal totalUnits = checked(effectivePackSize * lineInput.PackCount);
                Money unitMoney = new(unitCost, clinic.Currency);

                var batch = new InventoryItemBatch(
                    GuidV7.NewGuid(),
                    clinicId,
                    item.Id,
                    lineInput.BatchNumber,
                    lineInput.ExpiryDate,
                    totalUnits,
                    unitMoney,
                    invoice.Id,
                    now);
                _dbContext.InventoryItemBatches.Add(batch);

                var lineEntity = new PurchaseInvoiceLine(
                    GuidV7.NewGuid(),
                    clinicId,
                    invoice.Id,
                    item.Id,
                    batch.Id,
                    effectivePackSize,
                    lineInput.PackCount,
                    new Money(lineInput.PackPriceAmount, clinic.Currency),
                    lineInput.BatchNumber,
                    lineInput.ExpiryDate);
                _dbContext.PurchaseInvoiceLines.Add(lineEntity);

                item.ReceivePurchaseUnits(totalUnits, unitCost, effectivePackSize, now);

                _dbContext.StockMovements.Add(new StockMovement(
                    GuidV7.NewGuid(),
                    clinicId,
                    item.Id,
                    batch.Id,
                    StockMovementType.PurchaseReceived,
                    totalUnits,
                    item.QuantityOnHand,
                    unitMoney,
                    null,
                    invoice.Id,
                    $"Purchase Invoice #{invoice.InvoiceNumber}",
                    userId.ToString(),
                    input.OperationId,
                    now));

                if (item.QuantityOnHand > item.LowStockThreshold)
                {
                    await ResolveActiveItemAlertsAsync(clinicId, item.Id, InventoryAlertKind.LowStock, now, cancellationToken);
                    await ResolveActiveItemAlertsAsync(clinicId, item.Id, InventoryAlertKind.NegativeStock, now, cancellationToken);
                }

                if (batch.IsExpired(todayLocal) || batch.IsNearExpiry(todayLocal, settings.NearExpiryWarningDays))
                {
                    string prefixAr = batch.IsExpired(todayLocal) ? "منتهية الصلاحية" : "قريبة من انتهاء الصلاحية";
                    string prefixEn = batch.IsExpired(todayLocal) ? "expired" : "near expiry";
                    var alert = new InventoryAlert(
                        GuidV7.NewGuid(),
                        clinicId,
                        item.Id,
                        batch.Id,
                        InventoryAlertKind.NearExpiry,
                        $"الدفعة {batch.BatchNumber} للصنف {item.NameAr} {prefixAr} ({batch.ExpiryDate:yyyy-MM-dd}).",
                        $"Batch {batch.BatchNumber} of {item.NameEn} ({item.Code}) is {prefixEn} ({batch.ExpiryDate:yyyy-MM-dd}).",
                        now);
                    _dbContext.InventoryAlerts.Add(alert);
                    createdAlerts.Add(alert);
                }
            }

            if (input.SupplierId.HasValue && input.SupplierId.Value != Guid.Empty && _billingService != null)
            {
                Result<bool> linkResult = await _billingService.RecordSupplierPurchaseInvoiceExpenseAsync(
                    clinicId,
                    input.SupplierId.Value,
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.SupplierName,
                    new Money(invoice.TotalAmount, invoice.Currency),
                    new Money(invoice.PaidAmount, invoice.Currency),
                    input.PaymentMethod,
                    userId.ToString(),
                    input.OperationId,
                    cancellationToken);

                if (linkResult.IsFailure)
                {
                    return Result<PurchaseInvoiceResult>.Failure(linkResult.Error);
                }
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "ReceivePurchaseInvoice", invoice.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await BroadcastInventoryAlertsAsync(clinicId, createdAlerts, now, cancellationToken);

            await _auditService.LogAsync(
                "PurchaseInvoiceReceived",
                "PurchaseInvoice",
                invoice.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { invoice.Id, invoice.InvoiceNumber, invoice.SupplierId, invoice.TotalAmount, invoice.PaidAmount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PurchaseInvoiceResult>.Success(await ProjectInvoiceAsync(clinicId, invoice, cancellationToken));
        });
    }

    public async Task<Result<IReadOnlyList<PurchaseInvoiceResult>>> ListPurchaseInvoicesAsync(
        Guid userId,
        Guid clinicId,
        Guid? supplierId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PurchaseInvoiceResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<PurchaseInvoice> query = _dbContext.PurchaseInvoices
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && !p.IsVoided);
            if (supplierId.HasValue && supplierId.Value != Guid.Empty)
            {
                query = query.Where(p => p.SupplierId == supplierId.Value);
            }

            List<PurchaseInvoice> invoices = await query
                .OrderByDescending(p => p.InvoiceDate)
                .ThenByDescending(p => p.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            var projected = new List<PurchaseInvoiceResult>(invoices.Count);
            foreach (PurchaseInvoice inv in invoices)
            {
                projected.Add(await ProjectInvoiceAsync(clinicId, inv, cancellationToken));
            }

            return Result<IReadOnlyList<PurchaseInvoiceResult>>.Success(projected);
        });
    }

    public async Task<Result<StockMovementResult>> AdjustStockAsync(
        Guid userId,
        Guid clinicId,
        AdjustStockInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.write", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<StockMovementResult>.Failure(gate.Error);
        }

        if (input.QuantityDelta == 0m || string.IsNullOrWhiteSpace(input.Reason))
        {
            return Result<StockMovementResult>.Failure(Error.Validation("STOCK_ADJUSTMENT_INVALID", "Non-zero quantity delta and reason are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    StockMovement? prior = await _dbContext.StockMovements
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.ClinicId == clinicId && m.Id == existingOp.ResultEntityId, cancellationToken);
                    if (prior != null)
                    {
                        var lookup = await LoadItemLookupAsync(clinicId, [prior.ItemId], cancellationToken);
                        return Result<StockMovementResult>.Success(MapMovement(prior, lookup.GetValueOrDefault(prior.ItemId)));
                    }
                }
            }

            InventoryItem? item = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(i => i.ClinicId == clinicId && !i.IsVoided && i.Id == input.ItemId, cancellationToken);
            if (item == null)
            {
                return Result<StockMovementResult>.Failure(Error.NotFound("INVENTORY_ITEM_NOT_FOUND", "Inventory item was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            ClinicInventorySetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, now, cancellationToken);

            InventoryItemBatch? batch = null;
            if (input.BatchId.HasValue && input.BatchId.Value != Guid.Empty)
            {
                batch = await _dbContext.InventoryItemBatches
                    .FirstOrDefaultAsync(b => b.ClinicId == clinicId && !b.IsVoided && b.ItemId == item.Id && b.Id == input.BatchId.Value, cancellationToken);
                if (batch == null)
                {
                    return Result<StockMovementResult>.Failure(Error.NotFound("INVENTORY_BATCH_NOT_FOUND", "Inventory batch was not found."));
                }

                if (batch.QuantityRemaining + input.QuantityDelta < 0m)
                {
                    return Result<StockMovementResult>.Failure(Error.Conflict("INSUFFICIENT_STOCK", "Batch quantity remaining cannot become negative."));
                }
            }

            decimal nextBalance;
            try
            {
                nextBalance = item.AdjustQuantity(input.QuantityDelta, settings.NegativeStockPolicy, now);
            }
            catch (InvalidOperationException ex)
            {
                return Result<StockMovementResult>.Failure(Error.Conflict("INSUFFICIENT_STOCK", ex.Message));
            }

            batch?.AdjustRemaining(input.QuantityDelta);

            var movement = new StockMovement(
                GuidV7.NewGuid(),
                clinicId,
                item.Id,
                batch?.Id,
                StockMovementType.ManualAdjustment,
                input.QuantityDelta,
                nextBalance,
                new Money(batch?.UnitCostAmount ?? item.UnitCostAmount, item.Currency),
                null,
                null,
                input.Reason,
                userId.ToString(),
                input.OperationId,
                now);
            _dbContext.StockMovements.Add(movement);

            var alerts = await EvaluateItemStockAlertsAsync(clinicId, item, now, cancellationToken);

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "AdjustStock", movement.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await BroadcastInventoryAlertsAsync(clinicId, alerts, now, cancellationToken);

            await _auditService.LogAsync(
                "InventoryAdjusted",
                "StockMovement",
                movement.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { item.Id, item.Code, input.QuantityDelta, BalanceAfter = nextBalance }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<StockMovementResult>.Success(MapMovement(movement, item));
        });
    }

    public async Task<Result<IReadOnlyList<InventoryAlertResult>>> ListAlertsAsync(
        Guid userId,
        Guid clinicId,
        bool includeResolved,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<InventoryAlertResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<InventoryAlert> query = _dbContext.InventoryAlerts
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided);
            if (!includeResolved)
            {
                query = query.Where(a => !a.IsResolved);
            }

            List<InventoryAlert> alerts = await query
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            var itemMap = await LoadItemLookupAsync(clinicId, alerts.Select(a => a.ItemId), cancellationToken);
            return Result<IReadOnlyList<InventoryAlertResult>>.Success(
                alerts.Select(a => MapAlert(a, itemMap.GetValueOrDefault(a.ItemId))).ToList());
        });
    }

    public async Task<Result<InventoryAlertResult>> ResolveAlertAsync(
        Guid userId,
        Guid clinicId,
        Guid alertId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.write", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<InventoryAlertResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            InventoryAlert? alert = await _dbContext.InventoryAlerts
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.Id == alertId, cancellationToken);
            if (alert == null)
            {
                return Result<InventoryAlertResult>.Failure(Error.NotFound("INVENTORY_ALERT_NOT_FOUND", "Inventory alert was not found."));
            }

            if (!alert.IsResolved)
            {
                alert.Resolve(_clock.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);

                await _auditService.LogAsync(
                    "InventoryAlertResolved",
                    "InventoryAlert",
                    alert.Id.ToString(),
                    null,
                    JsonSerializer.Serialize(new { alert.Id, alert.ItemId, Kind = alert.Kind.ToString() }),
                    userId,
                    clinicId,
                    ipAddress,
                    requestId,
                    cancellationToken);
            }

            var itemMap = await LoadItemLookupAsync(clinicId, [alert.ItemId], cancellationToken);
            return Result<InventoryAlertResult>.Success(MapAlert(alert, itemMap.GetValueOrDefault(alert.ItemId)));
        });
    }

    public async Task<Result<ClinicInventorySettingResult>> GetSettingsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicInventorySettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicInventorySetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, _clock.UtcNow, cancellationToken);
            return Result<ClinicInventorySettingResult>.Success(MapSettings(settings));
        });
    }

    public async Task<Result<ClinicInventorySettingResult>> UpdateSettingsAsync(
        Guid userId,
        Guid clinicId,
        UpdateClinicInventorySettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "clinic.settings.edit", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicInventorySettingResult>.Failure(gate.Error);
        }

        if (input.NearExpiryWarningDays < 1 || input.NearExpiryWarningDays > 365)
        {
            return Result<ClinicInventorySettingResult>.Failure(Error.Validation("INVALID_WARNING_DAYS", "Near-expiry warning days must be between 1 and 365."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            ClinicInventorySetting? settings = await _dbContext.ClinicInventorySettings
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);
            if (settings == null)
            {
                settings = new ClinicInventorySetting(GuidV7.NewGuid(), clinicId, now);
                _dbContext.ClinicInventorySettings.Add(settings);
            }

            settings.Update(input.NegativeStockPolicy, input.NearExpiryWarningDays, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "InventorySettingsUpdated",
                "ClinicInventorySetting",
                settings.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { NegativeStockPolicy = settings.NegativeStockPolicy.ToString(), settings.NearExpiryWarningDays }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ClinicInventorySettingResult>.Success(MapSettings(settings));
        });
    }

    public async Task<Result<SupplierInventoryStatementResult>> GetSupplierStatementAsync(
        Guid userId,
        Guid clinicId,
        Guid supplierId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "inventory.read", "inventory", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SupplierInventoryStatementResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<SupplierInventoryStatementResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            bool financeEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "finance" && m.Enabled, cancellationToken);

            Supplier? supplier = await _dbContext.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.Id == supplierId, cancellationToken);

            List<PurchaseInvoice> invoices = await _dbContext.PurchaseInvoices
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && !p.IsVoided && p.SupplierId == supplierId)
                .OrderByDescending(p => p.InvoiceDate)
                .ThenByDescending(p => p.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            if (supplier == null && invoices.Count == 0)
            {
                return Result<SupplierInventoryStatementResult>.Failure(Error.NotFound("SUPPLIER_NOT_FOUND", "Supplier was not found."));
            }

            string supplierName = supplier?.Name ?? invoices[0].SupplierName;
            decimal invPurchased = decimal.Round(invoices.Sum(i => i.TotalAmount), 2, MidpointRounding.AwayFromZero);
            decimal invPaid = decimal.Round(invoices.Sum(i => i.PaidAmount), 2, MidpointRounding.AwayFromZero);
            decimal invBalance = decimal.Round(invPurchased - invPaid, 2, MidpointRounding.AwayFromZero);

            decimal financeAccrued = 0m;
            decimal financePaid = 0m;
            if (financeEnabled)
            {
                financeAccrued = await _dbContext.Expenses
                    .AsNoTracking()
                    .Where(e => e.ClinicId == clinicId && !e.IsVoided && e.SupplierId == supplierId && !e.CountsAsCashOut)
                    .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
                financePaid = await _dbContext.SupplierPayments
                    .AsNoTracking()
                    .Where(sp => sp.ClinicId == clinicId && sp.SupplierId == supplierId)
                    .SumAsync(sp => (decimal?)sp.Amount, cancellationToken) ?? 0m;
            }

            var projectedInvoices = new List<PurchaseInvoiceResult>(invoices.Count);
            foreach (PurchaseInvoice inv in invoices)
            {
                projectedInvoices.Add(await ProjectInvoiceAsync(clinicId, inv, cancellationToken));
            }

            return Result<SupplierInventoryStatementResult>.Success(new SupplierInventoryStatementResult(
                supplierId,
                supplierName,
                invPurchased,
                invPaid,
                invBalance,
                decimal.Round(financeAccrued, 2, MidpointRounding.AwayFromZero),
                decimal.Round(financePaid, 2, MidpointRounding.AwayFromZero),
                decimal.Round(financeAccrued - financePaid, 2, MidpointRounding.AwayFromZero),
                financeEnabled,
                clinic.Currency,
                projectedInvoices));
        });
    }

    public async Task<Result<IReadOnlyList<ChargeToolLineInput>>> ConsumeVisitToolsAsync(
        Guid clinicId,
        Guid visitId,
        string actorUserId,
        IReadOnlyList<VisitToolUsageInput> toolsUsed,
        Guid? operationId,
        CancellationToken cancellationToken = default)
    {
        if (toolsUsed == null || toolsUsed.Count == 0)
        {
            return Result<IReadOnlyList<ChargeToolLineInput>>.Success(Array.Empty<ChargeToolLineInput>());
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool inventoryEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "inventory" && m.Enabled, cancellationToken);
            if (!inventoryEnabled)
            {
                return Result<IReadOnlyList<ChargeToolLineInput>>.Success(Array.Empty<ChargeToolLineInput>());
            }

            List<StockMovement> existingMovements = await _dbContext.StockMovements
                .AsNoTracking()
                .Where(m => m.ClinicId == clinicId
                            && !m.IsVoided
                            && m.ReferenceVisitId == visitId
                            && m.MovementType == StockMovementType.SessionUsed)
                .ToListAsync(cancellationToken);

            if (existingMovements.Count > 0)
            {
                var priorItems = await LoadItemLookupAsync(clinicId, existingMovements.Select(m => m.ItemId), cancellationToken);
                var existingLines = new List<ChargeToolLineInput>(toolsUsed.Count);
                foreach (VisitToolUsageInput req in toolsUsed)
                {
                    InventoryItem? item = priorItems.GetValueOrDefault(req.InventoryItemId);
                    if (item == null)
                    {
                        continue;
                    }

                    List<StockMovement> itemMoves = existingMovements.Where(m => m.ItemId == item.Id).ToList();
                    decimal qtySum = itemMoves.Sum(m => Math.Abs(m.QuantityDelta));
                    if (qtySum <= 0m)
                    {
                        qtySum = req.Quantity;
                    }

                    decimal totalCost = itemMoves.Sum(m => Math.Abs(m.QuantityDelta) * m.UnitCostAmount);
                    decimal avgCost = qtySum > 0m ? decimal.Round(totalCost / qtySum, 2, MidpointRounding.AwayFromZero) : item.UnitCostAmount;
                    decimal patientPrice = req.PatientUnitPriceOverride ?? InventoryFefoAllocator.CalculateMarkedUpPatientPrice(avgCost, item.DefaultMarkupPercent);
                    existingLines.Add(new ChargeToolLineInput(
                        ChargeLineKind.Tool,
                        $"{item.NameEn} ({item.Code})",
                        req.Quantity,
                        avgCost,
                        patientPrice,
                        item.Id));
                }

                return Result<IReadOnlyList<ChargeToolLineInput>>.Success(existingLines);
            }

            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<IReadOnlyList<ChargeToolLineInput>>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            var groupedRequests = toolsUsed
                .GroupBy(t => t.InventoryItemId)
                .Select(g => new VisitToolUsageInput(
                    g.Key,
                    decimal.Round(g.Sum(x => x.Quantity), 2, MidpointRounding.AwayFromZero),
                    g.LastOrDefault(x => x.PatientUnitPriceOverride.HasValue)?.PatientUnitPriceOverride))
                .ToList();

            foreach (VisitToolUsageInput req in groupedRequests)
            {
                if (req.Quantity <= 0m || (req.PatientUnitPriceOverride.HasValue && req.PatientUnitPriceOverride.Value < 0m))
                {
                    return Result<IReadOnlyList<ChargeToolLineInput>>.Failure(Error.Validation("INVALID_TOOL_USAGE", "Tool quantity must be positive and override price non-negative."));
                }
            }

            List<Guid> itemIds = groupedRequests.Select(r => r.InventoryItemId).ToList();
            List<InventoryItem> items = await _dbContext.InventoryItems
                .Where(i => i.ClinicId == clinicId && !i.IsVoided && itemIds.Contains(i.Id))
                .ToListAsync(cancellationToken);
            Dictionary<Guid, InventoryItem> itemsById = items.ToDictionary(i => i.Id);

            foreach (VisitToolUsageInput req in groupedRequests)
            {
                if (!itemsById.ContainsKey(req.InventoryItemId))
                {
                    return Result<IReadOnlyList<ChargeToolLineInput>>.Failure(Error.NotFound("INVENTORY_ITEM_NOT_FOUND", $"Inventory item '{req.InventoryItemId}' was not found."));
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            DateOnly todayLocal = GetClinicToday(clinic.TimeZone, now);
            ClinicInventorySetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, now, cancellationToken);

            List<InventoryItemBatch> batches = await _dbContext.InventoryItemBatches
                .Where(b => b.ClinicId == clinicId && !b.IsVoided && itemIds.Contains(b.ItemId) && b.QuantityRemaining > 0m)
                .ToListAsync(cancellationToken);
            Dictionary<Guid, InventoryItemBatch> batchesById = batches.ToDictionary(b => b.Id);

            var plannedAllocations = new List<(VisitToolUsageInput Request, InventoryItem Item, InventoryFefoAllocator.FefoAllocationResult Allocation)>();
            foreach (VisitToolUsageInput req in groupedRequests)
            {
                InventoryItem item = itemsById[req.InventoryItemId];
                List<InventoryFefoAllocator.BatchSlice> slices = batches
                    .Where(b => b.ItemId == item.Id)
                    .Select(b => new InventoryFefoAllocator.BatchSlice(b.Id, b.ExpiryDate, b.QuantityRemaining, b.UnitCostAmount))
                    .ToList();

                try
                {
                    InventoryFefoAllocator.FefoAllocationResult allocation = InventoryFefoAllocator.Allocate(
                        req.Quantity, slices, todayLocal, item.UnitCostAmount, settings.NegativeStockPolicy);
                    plannedAllocations.Add((req, item, allocation));
                }
                catch (InvalidOperationException ex)
                {
                    return Result<IReadOnlyList<ChargeToolLineInput>>.Failure(Error.Conflict("INSUFFICIENT_STOCK", ex.Message));
                }
            }

            var createdAlerts = new List<InventoryAlert>();
            var chargeLines = new List<ChargeToolLineInput>(plannedAllocations.Count);

            foreach ((VisitToolUsageInput req, InventoryItem item, InventoryFefoAllocator.FefoAllocationResult allocation) in plannedAllocations)
            {
                decimal runningBalance = item.QuantityOnHand;
                item.AdjustQuantity(-req.Quantity, settings.NegativeStockPolicy, now);

                foreach (InventoryFefoAllocator.AllocatedBatchDeduction deduction in allocation.BatchDeductions)
                {
                    InventoryItemBatch batch = batchesById[deduction.BatchId];
                    batch.Deduct(deduction.DeductedQuantity);
                    runningBalance = decimal.Round(runningBalance - deduction.DeductedQuantity, 2, MidpointRounding.AwayFromZero);

                    _dbContext.StockMovements.Add(new StockMovement(
                        GuidV7.NewGuid(),
                        clinicId,
                        item.Id,
                        batch.Id,
                        StockMovementType.SessionUsed,
                        -deduction.DeductedQuantity,
                        runningBalance,
                        new Money(deduction.UnitCost, item.Currency),
                        visitId,
                        null,
                        $"Visit {visitId:D} FEFO batch {batch.BatchNumber}",
                        actorUserId,
                        operationId,
                        now));
                }

                if (allocation.UnallocatedShortfallQuantity > 0m)
                {
                    runningBalance = decimal.Round(runningBalance - allocation.UnallocatedShortfallQuantity, 2, MidpointRounding.AwayFromZero);
                    _dbContext.StockMovements.Add(new StockMovement(
                        GuidV7.NewGuid(),
                        clinicId,
                        item.Id,
                        null,
                        StockMovementType.SessionUsed,
                        -allocation.UnallocatedShortfallQuantity,
                        runningBalance,
                        new Money(item.UnitCostAmount, item.Currency),
                        visitId,
                        null,
                        $"Visit {visitId:D} negative-stock shortfall",
                        actorUserId,
                        operationId,
                        now));
                }

                createdAlerts.AddRange(await EvaluateItemStockAlertsAsync(clinicId, item, now, cancellationToken));

                decimal patientUnitPrice = req.PatientUnitPriceOverride ??
                    InventoryFefoAllocator.CalculateMarkedUpPatientPrice(allocation.WeightedAverageUnitCost, item.DefaultMarkupPercent);

                chargeLines.Add(new ChargeToolLineInput(
                    ChargeLineKind.Tool,
                    $"{item.NameEn} ({item.Code})",
                    req.Quantity,
                    allocation.WeightedAverageUnitCost,
                    patientUnitPrice,
                    item.Id));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await BroadcastInventoryAlertsAsync(clinicId, createdAlerts, now, cancellationToken);

            return Result<IReadOnlyList<ChargeToolLineInput>>.Success(chargeLines);
        });
    }

    public async Task<Result<ClinicInventoryInspectResult>> InspectClinicInventoryAsync(
        Guid superAdminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        return await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                Clinic? clinic = await _dbContext.Clinics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
                if (clinic == null)
                {
                    return Result<ClinicInventoryInspectResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
                }

                DateTimeOffset now = _clock.UtcNow;
                DateOnly todayLocal = GetClinicToday(clinic.TimeZone, now);
                ClinicInventorySetting? savedSettings = await _dbContext.ClinicInventorySettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);
                ClinicInventorySettingResult settingsResult = savedSettings != null
                    ? MapSettings(savedSettings)
                    : MapSettings(new ClinicInventorySetting(Guid.Empty, clinicId, now));

                List<InventoryItem> items = await _dbContext.InventoryItems
                    .AsNoTracking()
                    .Where(i => i.ClinicId == clinicId && !i.IsVoided)
                    .OrderBy(i => i.Code)
                    .Take(200)
                    .ToListAsync(cancellationToken);

                Dictionary<Guid, InventoryItem> itemMap = items.ToDictionary(i => i.Id);

                List<InventoryItemBatch> batches = await _dbContext.InventoryItemBatches
                    .AsNoTracking()
                    .Where(b => b.ClinicId == clinicId && !b.IsVoided)
                    .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                    .ThenBy(b => b.ExpiryDate)
                    .Take(200)
                    .ToListAsync(cancellationToken);

                List<InventoryAlert> alerts = await _dbContext.InventoryAlerts
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicId && !a.IsVoided && !a.IsResolved)
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                List<PurchaseInvoice> invoices = await _dbContext.PurchaseInvoices
                    .AsNoTracking()
                    .Where(p => p.ClinicId == clinicId && !p.IsVoided)
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Take(25)
                    .ToListAsync(cancellationToken);

                var projectedInvoices = new List<PurchaseInvoiceResult>(invoices.Count);
                foreach (PurchaseInvoice inv in invoices)
                {
                    projectedInvoices.Add(await ProjectInvoiceAsync(clinicId, inv, cancellationToken));
                }

                return Result<ClinicInventoryInspectResult>.Success(new ClinicInventoryInspectResult(
                    clinicId,
                    settingsResult,
                    items.Select(MapItem).ToList(),
                    batches.Select(b => MapBatch(b, itemMap.GetValueOrDefault(b.ItemId), todayLocal, settingsResult.NearExpiryWarningDays)).ToList(),
                    alerts.Select(a => MapAlert(a, itemMap.GetValueOrDefault(a.ItemId))).ToList(),
                    projectedInvoices));
            },
            $"SuperAdmin:{superAdminId}",
            "InspectInventory",
            cancellationToken);
    }

    private async Task<List<InventoryAlert>> EvaluateItemStockAlertsAsync(
        Guid clinicId,
        InventoryItem item,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var added = new List<InventoryAlert>();
        if (item.QuantityOnHand < 0m)
        {
            bool exists = await _dbContext.InventoryAlerts
                .AsNoTracking()
                .AnyAsync(a => a.ClinicId == clinicId && !a.IsVoided && !a.IsResolved && a.ItemId == item.Id && a.Kind == InventoryAlertKind.NegativeStock, cancellationToken);
            if (!exists)
            {
                var alert = new InventoryAlert(
                    GuidV7.NewGuid(),
                    clinicId,
                    item.Id,
                    null,
                    InventoryAlertKind.NegativeStock,
                    $"تحذير: رصيد الصنف {item.NameAr} أصبح سالباً ({item.QuantityOnHand}).",
                    $"Warning: Stock for {item.NameEn} ({item.Code}) went negative ({item.QuantityOnHand}).",
                    nowUtc);
                _dbContext.InventoryAlerts.Add(alert);
                added.Add(alert);
            }
        }
        else if (item.IsAtOrBelowLowStockThreshold())
        {
            bool exists = await _dbContext.InventoryAlerts
                .AsNoTracking()
                .AnyAsync(a => a.ClinicId == clinicId && !a.IsVoided && !a.IsResolved && a.ItemId == item.Id && a.Kind == InventoryAlertKind.LowStock, cancellationToken);
            if (!exists)
            {
                var alert = new InventoryAlert(
                    GuidV7.NewGuid(),
                    clinicId,
                    item.Id,
                    null,
                    InventoryAlertKind.LowStock,
                    $"تنبيه انخفاض المخزون للصنف {item.NameAr}: المتاح {item.QuantityOnHand} من حد {item.LowStockThreshold}.",
                    $"Low stock alert for {item.NameEn} ({item.Code}): {item.QuantityOnHand} remaining (threshold {item.LowStockThreshold}).",
                    nowUtc);
                _dbContext.InventoryAlerts.Add(alert);
                added.Add(alert);
            }
        }
        else
        {
            await ResolveActiveItemAlertsAsync(clinicId, item.Id, InventoryAlertKind.LowStock, nowUtc, cancellationToken);
            await ResolveActiveItemAlertsAsync(clinicId, item.Id, InventoryAlertKind.NegativeStock, nowUtc, cancellationToken);
        }

        return added;
    }

    private async Task ResolveActiveItemAlertsAsync(
        Guid clinicId,
        Guid itemId,
        InventoryAlertKind kind,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        List<InventoryAlert> active = await _dbContext.InventoryAlerts
            .Where(a => a.ClinicId == clinicId && !a.IsVoided && !a.IsResolved && a.ItemId == itemId && a.Kind == kind)
            .ToListAsync(cancellationToken);

        foreach (InventoryAlert alert in active)
        {
            alert.Resolve(nowUtc);
        }
    }

    private async Task BroadcastInventoryAlertsAsync(
        Guid clinicId,
        List<InventoryAlert> alerts,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (_realtimeNotifier == null || alerts.Count == 0)
        {
            return;
        }

        foreach (InventoryAlert alert in alerts)
        {
            await _realtimeNotifier.NotifyClinicAsync(
                new ScheduleRealtimeEvent(
                    clinicId,
                    "InventoryAlertRaised",
                    alert.Id,
                    null,
                    nowUtc,
                    $"[{alert.Kind}] {alert.MessageEn}"),
                cancellationToken);
        }
    }

    private async Task<ClinicInventorySetting> GetOrBuildDefaultSettingsAsync(
        Guid clinicId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ClinicInventorySetting? existing = await _dbContext.ClinicInventorySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

        return existing ?? new ClinicInventorySetting(Guid.Empty, clinicId, nowUtc);
    }

    private async Task<Dictionary<Guid, InventoryItem>> LoadItemLookupAsync(
        Guid clinicId,
        IEnumerable<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        List<Guid> distinct = itemIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<Guid, InventoryItem>();
        }

        return await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.ClinicId == clinicId && distinct.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
    }

    private async Task<PurchaseInvoiceResult> ProjectInvoiceAsync(
        Guid clinicId,
        PurchaseInvoice invoice,
        CancellationToken cancellationToken)
    {
        List<PurchaseInvoiceLine> lines = await _dbContext.PurchaseInvoiceLines
            .AsNoTracking()
            .Where(l => l.ClinicId == clinicId && !l.IsVoided && l.PurchaseInvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        var itemMap = await LoadItemLookupAsync(clinicId, lines.Select(l => l.ItemId), cancellationToken);
        var lineResults = lines.Select(l =>
        {
            InventoryItem? item = itemMap.GetValueOrDefault(l.ItemId);
            return new PurchaseInvoiceLineResult(
                l.Id,
                l.ItemId,
                item?.Code ?? string.Empty,
                item?.NameAr ?? string.Empty,
                item?.NameEn ?? string.Empty,
                l.BatchId,
                l.PackSize,
                l.PackCount,
                l.TotalUnits,
                l.PackPriceAmount,
                l.LineTotalAmount,
                l.UnitCostAmount,
                l.Currency,
                l.BatchNumber,
                l.ExpiryDate);
        }).ToList();

        return new PurchaseInvoiceResult(
            invoice.Id,
            invoice.ClinicId,
            invoice.SupplierId,
            invoice.SupplierName,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.RemainingAmount,
            invoice.Currency,
            invoice.Notes,
            invoice.RecordedByUserId,
            invoice.CreatedAtUtc,
            lineResults);
    }

    private static InventoryItemResult MapItem(InventoryItem item) => new(
        item.Id,
        item.ClinicId,
        item.Code,
        item.NameAr,
        item.NameEn,
        item.UnitName,
        item.PackSize,
        item.UnitCostAmount,
        item.DefaultMarkupPercent,
        InventoryFefoAllocator.CalculateMarkedUpPatientPrice(item.UnitCostAmount, item.DefaultMarkupPercent),
        item.Currency,
        item.LowStockThreshold,
        item.QuantityOnHand,
        item.IsAtOrBelowLowStockThreshold(),
        item.SpecialtyCode,
        item.StorageLocation,
        item.IsActive,
        item.CreatedAtUtc,
        item.UpdatedAtUtc);

    private static InventoryItemBatchResult MapBatch(
        InventoryItemBatch batch,
        InventoryItem? item,
        DateOnly todayLocal,
        int warningDays) => new(
        batch.Id,
        batch.ClinicId,
        batch.ItemId,
        item?.Code ?? string.Empty,
        item?.NameAr ?? string.Empty,
        item?.NameEn ?? string.Empty,
        batch.BatchNumber,
        batch.ExpiryDate,
        batch.QuantityReceived,
        batch.QuantityRemaining,
        batch.UnitCostAmount,
        batch.Currency,
        batch.IsExpired(todayLocal),
        batch.IsNearExpiry(todayLocal, warningDays),
        batch.PurchaseInvoiceId,
        batch.CreatedAtUtc);

    private static StockMovementResult MapMovement(StockMovement movement, InventoryItem? item) => new(
        movement.Id,
        movement.ClinicId,
        movement.ItemId,
        item?.Code ?? string.Empty,
        item?.NameAr ?? string.Empty,
        item?.NameEn ?? string.Empty,
        movement.BatchId,
        movement.MovementType.ToString(),
        movement.QuantityDelta,
        movement.BalanceAfter,
        movement.UnitCostAmount,
        movement.Currency,
        movement.ReferenceVisitId,
        movement.ReferencePurchaseInvoiceId,
        movement.Reason,
        movement.CreatedByUserId,
        movement.OccurredAtUtc);

    private static InventoryAlertResult MapAlert(InventoryAlert alert, InventoryItem? item) => new(
        alert.Id,
        alert.ClinicId,
        alert.ItemId,
        item?.Code ?? string.Empty,
        item?.NameAr ?? string.Empty,
        item?.NameEn ?? string.Empty,
        alert.BatchId,
        alert.Kind.ToString(),
        alert.MessageAr,
        alert.MessageEn,
        alert.IsResolved,
        alert.CreatedAtUtc,
        alert.ResolvedAtUtc);

    private static ClinicInventorySettingResult MapSettings(ClinicInventorySetting s) => new(
        s.Id,
        s.ClinicId,
        s.NegativeStockPolicy.ToString(),
        s.NearExpiryWarningDays,
        s.UpdatedAtUtc);

    private static DateOnly GetClinicToday(string? timeZoneId, DateTimeOffset utcNow)
    {
        TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(timeZoneId);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utcNow, tz);
        return DateOnly.FromDateTime(local.DateTime);
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
}
