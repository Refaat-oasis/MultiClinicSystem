using System.Data;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 8 billing, append-only ledger, number-block receipt allocation, refunds,
/// daily cash closing with lock/reopen, finance-lite expenses/suppliers, lab-work costing, and portal billing queries.
/// </summary>
public sealed class BillingService : IBillingService, IBillingPortalQueries
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IClock _clock;

    public BillingService(
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

    public async Task<Result<IReadOnlyList<PriceListItemResult>>> ListPriceListAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PriceListItemResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<PriceListItem> items = await _dbContext.PriceListItems
                .AsNoTracking()
                .Where(x => x.ClinicId == clinicId && !x.IsVoided)
                .OrderBy(x => x.PriceCode)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<PriceListItemResult>>.Success(
                items.Select(MapPriceListItem).ToList());
        });
    }

    public async Task<Result<PriceListItemResult>> UpsertPriceListItemAsync(
        Guid userId,
        Guid clinicId,
        UpsertPriceListItemInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PriceListItemResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.PriceCode) || (string.IsNullOrWhiteSpace(input.NameAr) && string.IsNullOrWhiteSpace(input.NameEn)))
        {
            return Result<PriceListItemResult>.Failure(Error.Validation("PRICE_ITEM_INVALID", "PriceCode and item name are required."));
        }

        if (input.UnitCostAmount < 0m || input.DefaultPriceAmount < 0m)
        {
            return Result<PriceListItemResult>.Failure(Error.Validation("NEGATIVE_AMOUNT", "Unit cost and default price cannot be negative."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<PriceListItemResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    PriceListItem? prior = await _dbContext.PriceListItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == existingOp.ResultEntityId, cancellationToken);
                    if (prior != null)
                    {
                        return Result<PriceListItemResult>.Success(MapPriceListItem(prior));
                    }
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            string normalizedCode = input.PriceCode.Trim().ToUpperInvariant();
            PriceListItem? entity = input.Id.HasValue && input.Id.Value != Guid.Empty
                ? await _dbContext.PriceListItems.FirstOrDefaultAsync(x => x.ClinicId == clinicId && !x.IsVoided && x.Id == input.Id.Value, cancellationToken)
                : await _dbContext.PriceListItems.FirstOrDefaultAsync(x => x.ClinicId == clinicId && !x.IsVoided && x.PriceCode == normalizedCode, cancellationToken);

            var unitCost = new Money(input.UnitCostAmount, clinic.Currency);
            var defaultPrice = new Money(input.DefaultPriceAmount, clinic.Currency);
            bool isNew = entity == null;

            if (entity == null)
            {
                Guid id = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
                entity = new PriceListItem(
                    id, clinicId, normalizedCode, input.NameAr, input.NameEn,
                    input.Kind, unitCost, defaultPrice, input.OperationId, now);
                _dbContext.PriceListItems.Add(entity);
            }
            else
            {
                entity.Update(input.NameAr, input.NameEn, input.Kind, unitCost, defaultPrice, input.IsActive, now);
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "UpsertPriceListItem", entity.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                isNew ? "PriceListItemCreated" : "PriceListItemUpdated",
                "PriceListItem",
                entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { entity.Id, entity.PriceCode, entity.UnitCostAmount, entity.DefaultPriceAmount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PriceListItemResult>.Success(MapPriceListItem(entity));
        });
    }

    public async Task<Result<IReadOnlyList<ChargeResult>>> ListChargesAsync(
        Guid userId,
        Guid clinicId,
        Guid? patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ChargeResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<Charge> query = _dbContext.Charges
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided);

            if (patientId.HasValue && patientId.Value != Guid.Empty)
            {
                query = query.Where(c => c.PatientId == patientId.Value);
            }

            List<Charge> charges = await query
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<ChargeResult>>.Success(await ProjectChargesAsync(clinicId, charges, cancellationToken));
        });
    }

    public async Task<Result<ChargeResult>> CreateChargeAsync(
        Guid userId,
        Guid clinicId,
        CreateChargeInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ChargeResult>.Failure(gate.Error);
        }

        if (input.PatientId == Guid.Empty)
        {
            return Result<ChargeResult>.Failure(Error.Validation("PATIENT_REQUIRED", "PatientId is required."));
        }

        var permsResult = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (permsResult.IsFailure)
        {
            return Result<ChargeResult>.Failure(permsResult.Error);
        }

        HashSet<string> permSet = permsResult.Value.EffectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (input.IsFreeSession)
        {
            if (!permSet.Contains("billing.free_session"))
            {
                return Result<ChargeResult>.Failure(Error.Forbidden("INSUFFICIENT_PERMISSION_FREE_SESSION", "Permission 'billing.free_session' is required."));
            }

            if (string.IsNullOrWhiteSpace(input.FreeSessionReason))
            {
                return Result<ChargeResult>.Failure(Error.Validation("FREE_SESSION_REASON_REQUIRED", "Free session requires a reason."));
            }
        }
        else if (input.CustomSessionPriceAmount.HasValue)
        {
            if (!permSet.Contains("billing.custom_price"))
            {
                return Result<ChargeResult>.Failure(Error.Forbidden("INSUFFICIENT_PERMISSION_CUSTOM_PRICE", "Permission 'billing.custom_price' is required."));
            }

            if (input.CustomSessionPriceAmount.Value < 0m)
            {
                return Result<ChargeResult>.Failure(Error.Validation("NEGATIVE_AMOUNT", "Custom session price cannot be negative."));
            }

            if (string.IsNullOrWhiteSpace(input.CustomPriceReason))
            {
                return Result<ChargeResult>.Failure(Error.Validation("CUSTOM_PRICE_REASON_REQUIRED", "Custom session price requires a reason."));
            }
        }

        if ((input.DiscountPercent ?? 0m) > 0m || (input.DiscountFixedAmount ?? 0m) > 0m)
        {
            if (!permSet.Contains("billing.discount"))
            {
                return Result<ChargeResult>.Failure(Error.Forbidden("INSUFFICIENT_PERMISSION_DISCOUNT", "Permission 'billing.discount' is required."));
            }

            if (string.IsNullOrWhiteSpace(input.DiscountReason))
            {
                return Result<ChargeResult>.Failure(Error.Validation("DISCOUNT_REASON_REQUIRED", "Discount requires a reason."));
            }
        }

        if ((input.ExtraChargeAmount ?? 0m) > 0m)
        {
            if (!permSet.Contains("billing.extra_charge"))
            {
                return Result<ChargeResult>.Failure(Error.Forbidden("INSUFFICIENT_PERMISSION_EXTRA_CHARGE", "Permission 'billing.extra_charge' is required."));
            }

            if (string.IsNullOrWhiteSpace(input.ExtraChargeReason))
            {
                return Result<ChargeResult>.Failure(Error.Validation("EXTRA_CHARGE_REASON_REQUIRED", "Extra charge requires a reason."));
            }
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<ChargeResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            Patient? patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == input.PatientId, cancellationToken);
            if (patient == null)
            {
                return Result<ChargeResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found in this clinic."));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    Charge? prior = await _dbContext.Charges
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.Id == existingOp.ResultEntityId, cancellationToken);
                    if (prior != null)
                    {
                        IReadOnlyList<ChargeResult> projectedPrior = await ProjectChargesAsync(clinicId, [prior], cancellationToken);
                        return Result<ChargeResult>.Success(projectedPrior[0]);
                    }
                }
            }

            DateTimeOffset now = _clock.UtcNow;
            if (input.PostImmediately && await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, now, cancellationToken))
            {
                return Result<ChargeResult>.Failure(Error.Conflict(
                    "DAY_CLOSED_LOCKED",
                    "The clinic business day is closed. Reopen the day before posting financial charges."));
            }

            decimal standardSessionPrice = await ResolveStandardSessionPriceAsync(clinicId, input.AppointmentId, input.StandardSessionPriceAmount, cancellationToken);

            var lineInputs = new List<ChargeToolLineInput>();
            var resolvedLineSpecs = new List<(ChargeLineKind Kind, Guid? PriceItemId, Guid? InventoryItemId, string Description, decimal Qty, decimal UnitCost, decimal UnitCharged)>();
            if (input.Lines != null)
            {
                foreach (ChargeLineInput line in input.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0m || line.UnitCostAmount < 0m || line.UnitChargedPriceAmount < 0m)
                    {
                        return Result<ChargeResult>.Failure(Error.Validation("CHARGE_LINE_INVALID", "Charge line description, positive quantity, and non-negative amounts are required."));
                    }

                    lineInputs.Add(new ChargeToolLineInput(line.Kind, line.Description, line.Quantity, line.UnitCostAmount, line.UnitChargedPriceAmount));
                    resolvedLineSpecs.Add((line.Kind, line.PriceListItemId, line.InventoryItemId, line.Description, line.Quantity, line.UnitCostAmount, line.UnitChargedPriceAmount));
                }
            }

            ChargeCalculationResult calc;
            try
            {
                calc = BillingCalculator.CalculateCharge(
                    standardSessionPrice,
                    input.IsFreeSession,
                    input.CustomSessionPriceAmount,
                    lineInputs,
                    input.DiscountPercent,
                    input.DiscountFixedAmount,
                    input.ExtraChargeAmount,
                    clinic.RoundingRule);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result<ChargeResult>.Failure(Error.Validation("BILLING_CALCULATION_INVALID", ex.Message));
            }

            Guid chargeId = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            ChargeStatus initialStatus = input.PostImmediately ? ChargeStatus.Posted : ChargeStatus.Draft;

            var charge = new Charge(
                chargeId,
                clinicId,
                input.PatientId,
                input.VisitId,
                input.AppointmentId,
                input.DoctorId,
                initialStatus,
                standardSessionPrice,
                input.IsFreeSession,
                input.FreeSessionReason,
                input.CustomSessionPriceAmount,
                input.CustomPriceReason,
                input.DiscountPercent,
                input.DiscountFixedAmount,
                input.DiscountReason,
                input.ExtraChargeAmount,
                input.ExtraChargeReason,
                calc,
                clinic.Currency,
                clinic.RoundingRule,
                input.Notes,
                userId.ToString(),
                input.OperationId,
                now);

            _dbContext.Charges.Add(charge);

            foreach (var lineSpec in resolvedLineSpecs)
            {
                _dbContext.ChargeLines.Add(new ChargeLine(
                    GuidV7.NewGuid(),
                    clinicId,
                    charge.Id,
                    lineSpec.Kind,
                    lineSpec.PriceItemId,
                    lineSpec.InventoryItemId,
                    lineSpec.Description,
                    lineSpec.Qty,
                    lineSpec.UnitCost,
                    lineSpec.UnitCharged,
                    clinic.Currency,
                    now));
            }

            if (initialStatus == ChargeStatus.Posted)
            {
                clinic.MarkHasFinancialEntries();
                _dbContext.LedgerEntries.Add(new LedgerEntry(
                    GuidV7.NewGuid(),
                    clinicId,
                    input.PatientId,
                    LedgerEntryType.Charge,
                    charge.Id,
                    "Charge",
                    charge.NetTotalAmount,
                    clinic.Currency,
                    null,
                    string.IsNullOrWhiteSpace(input.Notes) ? "Session / Manual Charge" : input.Notes.Trim(),
                    userId.ToString(),
                    now,
                    input.OperationId));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "CreateCharge", charge.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "ChargeCreated",
                "Charge",
                charge.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    charge.Id,
                    charge.PatientId,
                    Status = charge.Status.ToString(),
                    charge.BaseSessionPriceAmount,
                    charge.IsFreeSession,
                    charge.DiscountAmount,
                    charge.ToolsMarkupAmount,
                    charge.NetTotalAmount
                }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            IReadOnlyList<ChargeResult> projected = await ProjectChargesAsync(clinicId, [charge], cancellationToken);
            return Result<ChargeResult>.Success(projected[0]);
        });
    }

    public Task CreateDraftChargeForCompletedVisitAsync(
        Guid clinicId,
        Guid visitId,
        Guid patientId,
        Guid? appointmentId,
        Guid doctorId,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        CreateDraftChargeForCompletedVisitAsync(
            clinicId,
            visitId,
            patientId,
            appointmentId,
            doctorId,
            actorUserId,
            null,
            cancellationToken);

    public async Task CreateDraftChargeForCompletedVisitAsync(
        Guid clinicId,
        Guid visitId,
        Guid patientId,
        Guid? appointmentId,
        Guid doctorId,
        string actorUserId,
        IReadOnlyList<ChargeToolLineInput>? toolLines,
        CancellationToken cancellationToken = default)
    {
        bool billingEnabled = await _dbContext.ClinicModules
            .AsNoTracking()
            .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "billing" && m.Enabled, cancellationToken);
        if (!billingEnabled)
        {
            return;
        }

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool exists = await _dbContext.Charges
                .AsNoTracking()
                .AnyAsync(c => c.ClinicId == clinicId && !c.IsVoided && c.VisitId == visitId, cancellationToken);
            if (exists)
            {
                return true;
            }

            Clinic? clinic = await _dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return false;
            }

            decimal standardPrice = await ResolveStandardSessionPriceAsync(clinicId, appointmentId, null, cancellationToken);
            ChargeCalculationResult calc = BillingCalculator.CalculateCharge(
                standardPrice, false, null, toolLines, null, null, null, clinic.RoundingRule);

            DateTimeOffset now = _clock.UtcNow;
            var draft = new Charge(
                GuidV7.NewGuid(),
                clinicId,
                patientId,
                visitId,
                appointmentId,
                doctorId,
                ChargeStatus.Draft,
                standardPrice,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                calc,
                clinic.Currency,
                clinic.RoundingRule,
                "Auto-created draft charge from completed clinical session",
                actorUserId,
                null,
                now);

            _dbContext.Charges.Add(draft);
            if (toolLines != null)
            {
                foreach (ChargeToolLineInput toolLine in toolLines)
                {
                    _dbContext.ChargeLines.Add(new ChargeLine(
                        GuidV7.NewGuid(),
                        clinicId,
                        draft.Id,
                        toolLine.Kind,
                        null,
                        toolLine.InventoryItemId,
                        toolLine.Description,
                        toolLine.Quantity,
                        toolLine.UnitCost,
                        toolLine.UnitChargedPrice,
                        clinic.Currency,
                        now));
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    public async Task<Result<ChargeResult>> PostDraftChargeAsync(
        Guid userId,
        Guid clinicId,
        Guid chargeId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ChargeResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<ChargeResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            Charge? charge = await _dbContext.Charges
                .FirstOrDefaultAsync(c => c.ClinicId == clinicId && !c.IsVoided && c.Id == chargeId, cancellationToken);
            if (charge == null)
            {
                return Result<ChargeResult>.Failure(Error.NotFound("CHARGE_NOT_FOUND", "Charge was not found."));
            }

            if (charge.Status == ChargeStatus.Posted)
            {
                IReadOnlyList<ChargeResult> already = await ProjectChargesAsync(clinicId, [charge], cancellationToken);
                return Result<ChargeResult>.Success(already[0]);
            }

            DateTimeOffset now = _clock.UtcNow;
            if (await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, now, cancellationToken))
            {
                return Result<ChargeResult>.Failure(Error.Conflict("DAY_CLOSED_LOCKED", "This business date is closed. Reopen before posting charges."));
            }

            charge.Post(now);
            clinic.MarkHasFinancialEntries();

            _dbContext.LedgerEntries.Add(new LedgerEntry(
                GuidV7.NewGuid(),
                clinicId,
                charge.PatientId,
                LedgerEntryType.Charge,
                charge.Id,
                "Charge",
                charge.NetTotalAmount,
                charge.Currency,
                null,
                string.IsNullOrWhiteSpace(charge.Notes) ? "Posted Session Charge" : charge.Notes,
                userId.ToString(),
                now,
                null));

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "ChargePosted",
                "Charge",
                charge.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { charge.Id, charge.NetTotalAmount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            IReadOnlyList<ChargeResult> projected = await ProjectChargesAsync(clinicId, [charge], cancellationToken);
            return Result<ChargeResult>.Success(projected[0]);
        });
    }

    public async Task<Result<IReadOnlyList<PaymentResult>>> RecordPaymentAsync(
        Guid userId,
        Guid clinicId,
        RecordPaymentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PaymentResult>>.Failure(gate.Error);
        }

        if (input.PatientId == Guid.Empty || input.Splits == null || input.Splits.Count == 0)
        {
            return Result<IReadOnlyList<PaymentResult>>.Failure(Error.Validation("PAYMENT_INVALID", "PatientId and at least one payment split are required."));
        }

        if (input.Splits.Any(s => s.Amount <= 0m))
        {
            return Result<IReadOnlyList<PaymentResult>>.Failure(Error.Validation("PAYMENT_AMOUNT_INVALID", "Every payment split amount must be greater than zero."));
        }

        string effectiveDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "web-default" : deviceId.Trim();
        Guid? parsedDeviceGuid = Guid.TryParse(deviceId, out Guid dg) ? dg : null;

        List<Payment> createdPayments = new();
        Result<IReadOnlyList<PaymentResult>>? earlyFailure = null;

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                earlyFailure = Result<IReadOnlyList<PaymentResult>>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
                return false;
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    List<Payment> prior = await _dbContext.Payments
                        .AsNoTracking()
                        .Where(p => p.ClinicId == clinicId && p.OperationId == input.OperationId.Value)
                        .OrderBy(p => p.ReceiptNumber)
                        .ToListAsync(cancellationToken);
                    if (prior.Count > 0)
                    {
                        createdPayments = prior;
                        return true;
                    }
                }
            }

            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == input.PatientId, cancellationToken);
            if (!patientExists)
            {
                earlyFailure = Result<IReadOnlyList<PaymentResult>>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found in this clinic."));
                return false;
            }

            DateTimeOffset now = _clock.UtcNow;
            if (await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, now, cancellationToken))
            {
                earlyFailure = Result<IReadOnlyList<PaymentResult>>.Failure(Error.Conflict(
                    "DAY_CLOSED_LOCKED",
                    "The clinic business day is closed. Reopen the day before collecting payments."));
                return false;
            }

            Charge? targetCharge = null;
            if (input.ChargeId.HasValue && input.ChargeId.Value != Guid.Empty)
            {
                targetCharge = await _dbContext.Charges
                    .FirstOrDefaultAsync(c => c.ClinicId == clinicId && !c.IsVoided && c.Id == input.ChargeId.Value && c.PatientId == input.PatientId, cancellationToken);
                if (targetCharge == null)
                {
                    earlyFailure = Result<IReadOnlyList<PaymentResult>>.Failure(Error.NotFound("CHARGE_NOT_FOUND", "Charge was not found for this patient."));
                    return false;
                }

                if (targetCharge.Status == ChargeStatus.Draft)
                {
                    targetCharge.Post(now);
                    _dbContext.LedgerEntries.Add(new LedgerEntry(
                        GuidV7.NewGuid(),
                        clinicId,
                        targetCharge.PatientId,
                        LedgerEntryType.Charge,
                        targetCharge.Id,
                        "Charge",
                        targetCharge.NetTotalAmount,
                        targetCharge.Currency,
                        null,
                        string.IsNullOrWhiteSpace(targetCharge.Notes) ? "Posted Session Charge" : targetCharge.Notes,
                        userId.ToString(),
                        now,
                        input.OperationId));
                }
            }

            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            string lockResource = $"NumberBlock_{clinicId:N}_Receipt";
            await _dbContext.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;",
                [lockResource],
                cancellationToken);

            Guid groupId = GuidV7.NewGuid();
            clinic.MarkHasFinancialEntries();

            foreach (PaymentSplitInput split in input.Splits)
            {
                string receiptNumber = await AllocateNextReceiptNumberAsync(clinicId, effectiveDeviceId, now, cancellationToken);
                Guid paymentId = input.Splits.Count == 1 && input.Id.HasValue && input.Id.Value != Guid.Empty
                    ? input.Id.Value
                    : GuidV7.NewGuid();

                var payment = new Payment(
                    paymentId,
                    clinicId,
                    input.PatientId,
                    parsedDeviceGuid,
                    receiptNumber,
                    split.Method,
                    new Money(split.Amount, clinic.Currency),
                    input.IsDeposit,
                    groupId,
                    split.Notes,
                    userId.ToString(),
                    now,
                    input.OperationId);

                _dbContext.Payments.Add(payment);
                createdPayments.Add(payment);

                if (targetCharge != null)
                {
                    decimal remainingDue = targetCharge.GetRemainingDueAmount();
                    decimal toAllocate = Math.Min(remainingDue, payment.Amount);
                    if (toAllocate > 0m)
                    {
                        targetCharge.ApplyAllocation(toAllocate, now);
                        _dbContext.PaymentAllocations.Add(new PaymentAllocation(
                            GuidV7.NewGuid(),
                            clinicId,
                            payment.Id,
                            targetCharge.Id,
                            new Money(toAllocate, clinic.Currency),
                            now));
                    }
                }

                _dbContext.LedgerEntries.Add(new LedgerEntry(
                    GuidV7.NewGuid(),
                    clinicId,
                    input.PatientId,
                    LedgerEntryType.Payment,
                    payment.Id,
                    "Payment",
                    -payment.Amount,
                    clinic.Currency,
                    payment.Method,
                    $"Receipt {receiptNumber} ({payment.Method})",
                    userId.ToString(),
                    now,
                    input.OperationId));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(),
                    clinicId,
                    input.OperationId.Value,
                    "RecordPayment",
                    createdPayments[0].Id,
                    now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return true;
        });

        if (earlyFailure != null)
        {
            return earlyFailure;
        }

        foreach (Payment p in createdPayments)
        {
            await _auditService.LogAsync(
                "PaymentRecorded",
                "Payment",
                p.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { p.Id, p.PatientId, p.ReceiptNumber, Method = p.Method.ToString(), p.Amount, p.IsDeposit }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);
        }

        IReadOnlyList<PaymentResult> mapped = await ProjectPaymentsAsync(clinicId, createdPayments, cancellationToken);
        return Result<IReadOnlyList<PaymentResult>>.Success(mapped);
    }

    public async Task<Result<RefundResult>> CreateRefundAsync(
        Guid userId,
        Guid clinicId,
        CreateRefundInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<RefundResult>.Failure(gate.Error);
        }

        var permsResult = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (permsResult.IsFailure)
        {
            return Result<RefundResult>.Failure(permsResult.Error);
        }

        if (!permsResult.Value.EffectivePermissions.Contains("billing.refund", StringComparer.OrdinalIgnoreCase))
        {
            return Result<RefundResult>.Failure(Error.Forbidden("INSUFFICIENT_PERMISSION_REFUND", "Permission 'billing.refund' is required to issue a refund."));
        }

        if (input.Amount <= 0m)
        {
            return Result<RefundResult>.Failure(Error.Validation("REFUND_AMOUNT_INVALID", "Refund amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Result<RefundResult>.Failure(Error.Validation("REFUND_REASON_REQUIRED", "Refund requires a reason."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<RefundResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.ClinicId == clinicId && o.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null && existingOp.ResultEntityId != Guid.Empty)
                {
                    Refund? prior = await _dbContext.Refunds
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.ClinicId == clinicId && r.Id == existingOp.ResultEntityId, cancellationToken);
                    if (prior != null)
                    {
                        return Result<RefundResult>.Success(MapRefund(prior));
                    }
                }
            }

            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == input.PatientId, cancellationToken);
            if (!patientExists)
            {
                return Result<RefundResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            if (await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, now, cancellationToken))
            {
                return Result<RefundResult>.Failure(Error.Conflict("DAY_CLOSED_LOCKED", "This business day is closed. Reopen the day before issuing refunds."));
            }

            string? refReceipt = null;
            if (input.OriginalPaymentId.HasValue && input.OriginalPaymentId.Value != Guid.Empty)
            {
                Payment? origPayment = await _dbContext.Payments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == input.OriginalPaymentId.Value && p.PatientId == input.PatientId, cancellationToken);
                if (origPayment == null)
                {
                    return Result<RefundResult>.Failure(Error.NotFound("PAYMENT_NOT_FOUND", "Original payment was not found."));
                }

                decimal alreadyRefunded = await _dbContext.Refunds
                    .AsNoTracking()
                    .Where(r => r.ClinicId == clinicId && r.OriginalPaymentId == origPayment.Id)
                    .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;
                if (alreadyRefunded + input.Amount > origPayment.Amount)
                {
                    return Result<RefundResult>.Failure(Error.Validation("REFUND_EXCEEDS_PAYMENT", "Total refunded amount cannot exceed the original payment amount."));
                }

                refReceipt = $"RF-{origPayment.ReceiptNumber}";
            }

            if (input.ChargeId.HasValue && input.ChargeId.Value != Guid.Empty)
            {
                Charge? charge = await _dbContext.Charges
                    .FirstOrDefaultAsync(c => c.ClinicId == clinicId && !c.IsVoided && c.Id == input.ChargeId.Value && c.PatientId == input.PatientId, cancellationToken);
                if (charge == null)
                {
                    return Result<RefundResult>.Failure(Error.NotFound("CHARGE_NOT_FOUND", "Charge was not found."));
                }

                charge.RecordRefund(input.Amount, now);
            }

            Guid refundId = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            var refund = new Refund(
                refundId,
                clinicId,
                input.PatientId,
                input.OriginalPaymentId,
                input.ChargeId,
                input.Method,
                new Money(input.Amount, clinic.Currency),
                input.Reason,
                refReceipt,
                userId.ToString(),
                now,
                input.OperationId);

            _dbContext.Refunds.Add(refund);
            clinic.MarkHasFinancialEntries();

            _dbContext.LedgerEntries.Add(new LedgerEntry(
                GuidV7.NewGuid(),
                clinicId,
                input.PatientId,
                LedgerEntryType.Refund,
                refund.Id,
                "Refund",
                refund.Amount,
                clinic.Currency,
                refund.Method,
                $"Refund ({refund.Method}): {refund.Reason}",
                userId.ToString(),
                now,
                input.OperationId));

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "CreateRefund", refund.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "RefundIssued",
                "Refund",
                refund.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { refund.Id, refund.PatientId, refund.OriginalPaymentId, refund.Amount, Method = refund.Method.ToString(), refund.Reason }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<RefundResult>.Success(MapRefund(refund));
        });
    }

    public async Task<Result<SkippedReceiptNumberResult>> RecordSkippedReceiptAsync(
        Guid userId,
        Guid clinicId,
        RecordSkippedReceiptInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SkippedReceiptNumberResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.ReceiptNumber) || string.IsNullOrWhiteSpace(input.Reason))
        {
            return Result<SkippedReceiptNumberResult>.Failure(Error.Validation("SKIPPED_RECEIPT_INVALID", "ReceiptNumber and Reason are required."));
        }

        string normReceipt = input.ReceiptNumber.Trim().ToUpperInvariant();
        Guid? parsedDevice = Guid.TryParse(deviceId, out Guid dg) ? dg : null;

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool usedByPayment = await _dbContext.Payments
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && p.ReceiptNumber == normReceipt, cancellationToken);
            if (usedByPayment)
            {
                return Result<SkippedReceiptNumberResult>.Failure(Error.Conflict("RECEIPT_ALREADY_USED", "This receipt number has already been issued to a payment."));
            }

            SkippedReceiptNumber? existing = await _dbContext.SkippedReceiptNumbers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && s.ReceiptNumber == normReceipt, cancellationToken);
            if (existing != null)
            {
                return Result<SkippedReceiptNumberResult>.Success(new SkippedReceiptNumberResult(
                    existing.Id, existing.ClinicId, existing.DeviceId, existing.ReceiptNumber, existing.Reason, existing.RecordedByUserId, existing.RecordedAtUtc));
            }

            DateTimeOffset now = _clock.UtcNow;
            var skipped = new SkippedReceiptNumber(
                GuidV7.NewGuid(),
                clinicId,
                parsedDevice,
                null,
                normReceipt,
                input.Reason,
                userId.ToString(),
                now,
                input.OperationId);

            _dbContext.SkippedReceiptNumbers.Add(skipped);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "SkippedReceiptRecorded",
                "SkippedReceiptNumber",
                skipped.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { skipped.ReceiptNumber, skipped.Reason }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<SkippedReceiptNumberResult>.Success(new SkippedReceiptNumberResult(
                skipped.Id, skipped.ClinicId, skipped.DeviceId, skipped.ReceiptNumber, skipped.Reason, skipped.RecordedByUserId, skipped.RecordedAtUtc));
        });
    }

    public async Task<Result<IReadOnlyList<SkippedReceiptNumberResult>>> ListSkippedReceiptsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<SkippedReceiptNumberResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<SkippedReceiptNumber> rows = await _dbContext.SkippedReceiptNumbers
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId)
                .OrderByDescending(s => s.RecordedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<SkippedReceiptNumberResult>>.Success(
                rows.Select(s => new SkippedReceiptNumberResult(
                    s.Id, s.ClinicId, s.DeviceId, s.ReceiptNumber, s.Reason, s.RecordedByUserId, s.RecordedAtUtc)).ToList());
        });
    }

    public async Task<Result<PatientLedgerResult>> GetPatientLedgerAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientLedgerResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<PatientLedgerResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == patientId, cancellationToken);
            if (!patientExists)
            {
                return Result<PatientLedgerResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found in this clinic."));
            }

            List<Charge> charges = await _dbContext.Charges
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.PatientId == patientId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            List<Payment> payments = await _dbContext.Payments
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && p.PatientId == patientId)
                .OrderByDescending(p => p.PaidAtUtc)
                .ToListAsync(cancellationToken);

            List<Refund> refunds = await _dbContext.Refunds
                .AsNoTracking()
                .Where(r => r.ClinicId == clinicId && r.PatientId == patientId)
                .OrderByDescending(r => r.RefundedAtUtc)
                .ToListAsync(cancellationToken);

            List<LedgerEntry> entries = await _dbContext.LedgerEntries
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && e.PatientId == patientId)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(cancellationToken);

            decimal balance = BillingCalculator.DeriveLedgerBalance(entries.Select(e => e.SignedAmount));

            return Result<PatientLedgerResult>.Success(new PatientLedgerResult(
                ClinicId: clinicId,
                PatientId: patientId,
                CurrentBalance: balance,
                Currency: clinic.Currency,
                Charges: await ProjectChargesAsync(clinicId, charges, cancellationToken),
                Payments: await ProjectPaymentsAsync(clinicId, payments, cancellationToken),
                Refunds: refunds.Select(MapRefund).ToList(),
                Entries: entries.Select(e => new LedgerEntryResult(
                    e.Id, e.EntryType, e.ReferenceId, e.ReferenceKind, e.SignedAmount, e.Currency, e.Method, e.Description, e.ActorUserId, e.OccurredAtUtc)).ToList()));
        });
    }

    public async Task<Result<ReceiptPdfResult>> GenerateReceiptPdfAsync(
        Guid userId,
        Guid clinicId,
        Guid paymentId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ReceiptPdfResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            Payment? payment = await _dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == paymentId, cancellationToken);
            if (clinic == null || payment == null)
            {
                return Result<ReceiptPdfResult>.Failure(Error.NotFound("PAYMENT_NOT_FOUND", "Payment receipt was not found."));
            }

            Patient? patient = await _dbContext.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == payment.PatientId, cancellationToken);
            if (patient == null)
            {
                return Result<ReceiptPdfResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            List<decimal> signedAmounts = await _dbContext.LedgerEntries
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && e.PatientId == patient.Id)
                .Select(e => e.SignedAmount)
                .ToListAsync(cancellationToken);
            decimal currentBalance = BillingCalculator.DeriveLedgerBalance(signedAmounts);

            IReadOnlyList<PaymentResult> projected = await ProjectPaymentsAsync(clinicId, [payment], cancellationToken);
            byte[] pdfBytes = BillingReceiptPdfGenerator.GenerateReceiptPdf(
                clinic.Name,
                patient.FileNumber,
                patient.FullName,
                projected[0],
                currentBalance);

            return Result<ReceiptPdfResult>.Success(new ReceiptPdfResult(
                $"receipt-{payment.ReceiptNumber}.pdf",
                "application/pdf",
                pdfBytes));
        });
    }

    public async Task<Result<LabWorkBillingResult>> CreateLabWorkAsync(
        Guid userId,
        Guid clinicId,
        CreateLabWorkBillingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<LabWorkBillingResult>.Failure(gate.Error);
        }

        if (input.PatientId == Guid.Empty || string.IsNullOrWhiteSpace(input.WorkDescription))
        {
            return Result<LabWorkBillingResult>.Failure(Error.Validation("LAB_WORK_INVALID", "PatientId and WorkDescription are required."));
        }

        if (input.LabCostAmount < 0m || input.PatientPriceAmount < 0m)
        {
            return Result<LabWorkBillingResult>.Failure(Error.Validation("NEGATIVE_AMOUNT", "Lab cost and patient price cannot be negative."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<LabWorkBillingResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == input.PatientId, cancellationToken);
            if (!patientExists)
            {
                return Result<LabWorkBillingResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            if (await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, now, cancellationToken))
            {
                return Result<LabWorkBillingResult>.Failure(Error.Conflict("DAY_CLOSED_LOCKED", "This business day is closed."));
            }

            bool financeEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "finance" && m.Enabled, cancellationToken);

            // Patient-facing Charge containing only the patient price
            var lineInput = new ChargeToolLineInput(
                ChargeLineKind.LabWork,
                input.WorkDescription,
                1m,
                input.LabCostAmount,
                input.PatientPriceAmount);
            ChargeCalculationResult calc = BillingCalculator.CalculateCharge(
                0m, false, null, [lineInput], null, null, null, clinic.RoundingRule);

            Guid chargeId = GuidV7.NewGuid();
            var charge = new Charge(
                chargeId,
                clinicId,
                input.PatientId,
                input.VisitId,
                null,
                input.DoctorId,
                ChargeStatus.Posted,
                0m,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                calc,
                clinic.Currency,
                clinic.RoundingRule,
                $"Lab Work: {input.WorkDescription}",
                userId.ToString(),
                input.OperationId,
                now);
            _dbContext.Charges.Add(charge);

            _dbContext.ChargeLines.Add(new ChargeLine(
                GuidV7.NewGuid(),
                clinicId,
                charge.Id,
                ChargeLineKind.LabWork,
                null,
                null,
                input.WorkDescription,
                1m,
                input.LabCostAmount,
                input.PatientPriceAmount,
                clinic.Currency,
                now));

            _dbContext.LedgerEntries.Add(new LedgerEntry(
                GuidV7.NewGuid(),
                clinicId,
                input.PatientId,
                LedgerEntryType.Charge,
                charge.Id,
                "LabWorkCharge",
                charge.NetTotalAmount,
                clinic.Currency,
                null,
                $"Lab Work: {input.WorkDescription}",
                userId.ToString(),
                now,
                input.OperationId));

            clinic.MarkHasFinancialEntries();

            Guid labWorkId = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            Guid? expenseId = null;

            var labOrder = new LabWorkOrder(
                labWorkId,
                clinicId,
                input.PatientId,
                input.VisitId,
                input.DoctorId,
                input.SupplierId,
                input.LabName,
                input.WorkDescription,
                input.ToothReference,
                new Money(input.LabCostAmount, clinic.Currency),
                new Money(input.PatientPriceAmount, clinic.Currency),
                charge.Id,
                null,
                input.OperationId,
                now);

            // When finance is enabled, record the lab cost as an expense / supplier payable
            if (financeEnabled && input.LabCostAmount > 0m)
            {
                Guid createdExpenseId = GuidV7.NewGuid();
                bool paidImmediately = input.ImmediateExpenseMethod.HasValue;
                var expense = new Expense(
                    createdExpenseId,
                    clinicId,
                    "LabWork",
                    $"Lab Cost ({input.LabName}): {input.WorkDescription}",
                    input.ImmediateExpenseMethod,
                    new Money(input.LabCostAmount, clinic.Currency),
                    input.SupplierId,
                    labOrder.Id,
                    paidImmediately,
                    userId.ToString(),
                    now,
                    input.OperationId);
                _dbContext.Expenses.Add(expense);
                labOrder.AttachExpense(createdExpenseId);
                expenseId = createdExpenseId;
            }

            _dbContext.LabWorkOrders.Add(labOrder);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "LabWorkRecorded",
                "LabWorkOrder",
                labOrder.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { labOrder.Id, labOrder.PatientId, labOrder.LabCostAmount, labOrder.PatientPriceAmount, ExpenseId = expenseId }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<LabWorkBillingResult>.Success(MapLabWork(labOrder));
        });
    }

    public async Task<Result<IReadOnlyList<LabWorkBillingResult>>> ListLabWorkAsync(
        Guid userId,
        Guid clinicId,
        Guid? patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<LabWorkBillingResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<LabWorkOrder> query = _dbContext.LabWorkOrders
                .AsNoTracking()
                .Where(l => l.ClinicId == clinicId && !l.IsVoided);

            if (patientId.HasValue && patientId.Value != Guid.Empty)
            {
                query = query.Where(l => l.PatientId == patientId.Value);
            }

            List<LabWorkOrder> rows = await query.OrderByDescending(l => l.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<LabWorkBillingResult>>.Success(rows.Select(MapLabWork).ToList());
        });
    }

    public async Task<Result<IReadOnlyList<SupplierResult>>> ListSuppliersAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<SupplierResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string currency = clinic?.Currency ?? Money.DefaultCurrency;

            List<Supplier> suppliers = await _dbContext.Suppliers
                .AsNoTracking()
                .Where(s => s.ClinicId == clinicId && !s.IsVoided)
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);

            List<Expense> supplierAccruals = await _dbContext.Expenses
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && !e.IsVoided && e.SupplierId.HasValue && !e.CountsAsCashOut)
                .ToListAsync(cancellationToken);

            List<SupplierPayment> supplierPayments = await _dbContext.SupplierPayments
                .AsNoTracking()
                .Where(sp => sp.ClinicId == clinicId)
                .ToListAsync(cancellationToken);

            List<SupplierResult> result = suppliers.Select(s =>
            {
                decimal accrued = Math.Round(supplierAccruals.Where(e => e.SupplierId == s.Id).Sum(e => e.Amount), 2);
                decimal paid = Math.Round(supplierPayments.Where(sp => sp.SupplierId == s.Id).Sum(sp => sp.Amount), 2);
                decimal balanceDue = Math.Round(accrued - paid, 2);
                return new SupplierResult(s.Id, s.ClinicId, s.Name, s.Type, s.Phone, s.Notes, accrued, paid, balanceDue, currency, s.IsActive, s.CreatedAtUtc);
            }).ToList();

            return Result<IReadOnlyList<SupplierResult>>.Success(result);
        });
    }

    public async Task<Result<SupplierResult>> CreateSupplierAsync(
        Guid userId,
        Guid clinicId,
        CreateSupplierInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SupplierResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return Result<SupplierResult>.Failure(Error.Validation("SUPPLIER_NAME_REQUIRED", "Supplier name is required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<SupplierResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            Guid id = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            var supplier = new Supplier(id, clinicId, input.Name, input.Type, input.Phone, input.Notes, input.OperationId, _clock.UtcNow);
            _dbContext.Suppliers.Add(supplier);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "SupplierCreated",
                "Supplier",
                supplier.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { supplier.Id, supplier.Name, Type = supplier.Type.ToString() }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<SupplierResult>.Success(new SupplierResult(
                supplier.Id, supplier.ClinicId, supplier.Name, supplier.Type, supplier.Phone, supplier.Notes,
                0m, 0m, 0m, clinic.Currency, supplier.IsActive, supplier.CreatedAtUtc));
        });
    }

    public async Task<Result<SupplierPaymentResult>> RecordSupplierPaymentAsync(
        Guid userId,
        Guid clinicId,
        RecordSupplierPaymentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SupplierPaymentResult>.Failure(gate.Error);
        }

        if (input.SupplierId == Guid.Empty || input.Amount <= 0m)
        {
            return Result<SupplierPaymentResult>.Failure(Error.Validation("SUPPLIER_PAYMENT_INVALID", "SupplierId and positive Amount are required."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<SupplierPaymentResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            bool supplierExists = await _dbContext.Suppliers
                .AsNoTracking()
                .AnyAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.Id == input.SupplierId, cancellationToken);
            if (!supplierExists)
            {
                return Result<SupplierPaymentResult>.Failure(Error.NotFound("SUPPLIER_NOT_FOUND", "Supplier was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            if (await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, now, cancellationToken))
            {
                return Result<SupplierPaymentResult>.Failure(Error.Conflict("DAY_CLOSED_LOCKED", "This business day is closed."));
            }

            Guid id = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            var payment = new SupplierPayment(
                id,
                clinicId,
                input.SupplierId,
                input.Method,
                new Money(input.Amount, clinic.Currency),
                input.ReferenceNote,
                userId.ToString(),
                now,
                input.OperationId);

            _dbContext.SupplierPayments.Add(payment);
            clinic.MarkHasFinancialEntries();
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "SupplierPaymentRecorded",
                "SupplierPayment",
                payment.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { payment.Id, payment.SupplierId, Method = payment.Method.ToString(), payment.Amount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<SupplierPaymentResult>.Success(new SupplierPaymentResult(
                payment.Id, payment.ClinicId, payment.SupplierId, payment.Method, payment.Amount, payment.Currency,
                payment.ReferenceNote, payment.PaidByUserId, payment.PaidAtUtc));
        });
    }

    public async Task<Result<IReadOnlyList<ExpenseResult>>> ListExpensesAsync(
        Guid userId,
        Guid clinicId,
        DateOnly? date,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ExpenseResult>>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            IQueryable<Expense> query = _dbContext.Expenses
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && !e.IsVoided);

            if (date.HasValue && clinic != null)
            {
                (DateTimeOffset startUtc, DateTimeOffset endUtc) = ResolveClinicDateWindowUtc(date.Value, clinic.TimeZone);
                query = query.Where(e => e.OccurredAtUtc >= startUtc && e.OccurredAtUtc <= endUtc);
            }

            List<Expense> rows = await query.OrderByDescending(e => e.OccurredAtUtc).Take(200).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<ExpenseResult>>.Success(rows.Select(MapExpense).ToList());
        });
    }

    public async Task<Result<ExpenseResult>> CreateExpenseAsync(
        Guid userId,
        Guid clinicId,
        CreateExpenseInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ExpenseResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Category) || string.IsNullOrWhiteSpace(input.Description) || input.Amount <= 0m)
        {
            return Result<ExpenseResult>.Failure(Error.Validation("EXPENSE_INVALID", "Category, Description, and positive Amount are required."));
        }

        if (input.CountsAsCashOut && !input.Method.HasValue)
        {
            return Result<ExpenseResult>.Failure(Error.Validation("EXPENSE_METHOD_REQUIRED", "Payment method is required when CountsAsCashOut is true."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<ExpenseResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            DateTimeOffset occurredAt = (input.OccurredAtUtc ?? _clock.UtcNow).ToUniversalTime();
            if (await IsDateClosedLockedAsync(clinicId, clinic.TimeZone, occurredAt, cancellationToken))
            {
                return Result<ExpenseResult>.Failure(Error.Conflict("DAY_CLOSED_LOCKED", "This business day is closed. Reopen the day before adding expenses."));
            }

            Guid id = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            var expense = new Expense(
                id,
                clinicId,
                input.Category,
                input.Description,
                input.Method,
                new Money(input.Amount, clinic.Currency),
                input.SupplierId,
                null,
                input.CountsAsCashOut,
                userId.ToString(),
                occurredAt,
                input.OperationId);

            _dbContext.Expenses.Add(expense);
            clinic.MarkHasFinancialEntries();
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "ExpenseRecorded",
                "Expense",
                expense.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { expense.Id, expense.Category, expense.Amount, expense.CountsAsCashOut }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ExpenseResult>.Success(MapExpense(expense));
        });
    }

    public async Task<Result<DailyClosingResult>> GetDailyClosingPreviewAsync(
        Guid userId,
        Guid clinicId,
        DateOnly date,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        bool financeEnabled = await ExecuteForClinicAsync(
            clinicId,
            () => _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "finance" && m.Enabled, cancellationToken));

        var gate = financeEnabled
            ? await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken)
            : await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.read", "billing", deviceId, cancellationToken);

        if (gate.IsFailure)
        {
            return Result<DailyClosingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<DailyClosingResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            DailyClosing? savedClosing = await _dbContext.DailyClosings
                .AsNoTracking()
                .FirstOrDefaultAsync(dc => dc.ClinicId == clinicId && dc.ClosingDate == date, cancellationToken);
            if (savedClosing != null && savedClosing.Status == DailyClosingStatus.Closed)
            {
                List<ClosingMethodTotal> savedMethods = await _dbContext.ClosingMethodTotals
                    .AsNoTracking()
                    .Where(m => m.ClinicId == clinicId && m.DailyClosingId == savedClosing.Id)
                    .ToListAsync(cancellationToken);
                return Result<DailyClosingResult>.Success(MapDailyClosing(savedClosing, savedMethods));
            }

            IReadOnlyList<ClosingMethodSummary> breakdown = await ComputeDayBreakdownAsync(
                clinicId, date, clinic.TimeZone, includeFinanceExpenses: financeEnabled, null, cancellationToken);

            decimal totalExpected = Math.Round(breakdown.Sum(b => b.ExpectedAmount), 2);
            var preview = new DailyClosingResult(
                Id: savedClosing?.Id ?? Guid.Empty,
                ClinicId: clinicId,
                ClosingDate: date,
                Status: savedClosing?.Status ?? DailyClosingStatus.Reopened,
                FinanceExpensesExcluded: !financeEnabled,
                TotalExpectedAmount: totalExpected,
                TotalCountedAmount: totalExpected,
                TotalVarianceAmount: 0m,
                Currency: clinic.Currency,
                ClosedByUserId: savedClosing?.ClosedByUserId ?? string.Empty,
                ClosedAtUtc: savedClosing?.ClosedAtUtc ?? _clock.UtcNow,
                CloseNotes: savedClosing?.CloseNotes,
                ReopenedByUserId: savedClosing?.ReopenedByUserId,
                ReopenedAtUtc: savedClosing?.ReopenedAtUtc,
                ReopenReason: savedClosing?.ReopenReason,
                Methods: breakdown.Select(b => new ClosingMethodTotalResult(
                    b.Method, b.PaymentsTotal, b.RefundsTotal, b.ExpensesTotal, b.ExpectedAmount, b.CountedAmount, b.Variance, clinic.Currency)).ToList());

            return Result<DailyClosingResult>.Success(preview);
        });
    }

    public async Task<Result<DailyClosingResult>> CloseDayAsync(
        Guid userId,
        Guid clinicId,
        CloseDayInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        bool financeEnabled = await ExecuteForClinicAsync(
            clinicId,
            () => _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "finance" && m.Enabled, cancellationToken));

        var gate = financeEnabled
            ? await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.close_day", "finance", deviceId, cancellationToken)
            : await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "billing", deviceId, cancellationToken);

        if (gate.IsFailure)
        {
            return Result<DailyClosingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<DailyClosingResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            DailyClosing? existing = await _dbContext.DailyClosings
                .FirstOrDefaultAsync(dc => dc.ClinicId == clinicId && dc.ClosingDate == input.ClosingDate, cancellationToken);

            if (existing != null && existing.Status == DailyClosingStatus.Closed)
            {
                return Result<DailyClosingResult>.Failure(Error.Conflict("DAY_ALREADY_CLOSED", "This closing date is already closed."));
            }

            IReadOnlyList<ClosingMethodSummary> breakdown = await ComputeDayBreakdownAsync(
                clinicId, input.ClosingDate, clinic.TimeZone, includeFinanceExpenses: financeEnabled, input.CountedByMethod, cancellationToken);

            decimal totalExpected = Math.Round(breakdown.Sum(b => b.ExpectedAmount), 2);
            decimal totalCounted = Math.Round(breakdown.Sum(b => b.CountedAmount), 2);
            DateTimeOffset now = _clock.UtcNow;

            if (existing == null)
            {
                existing = new DailyClosing(
                    GuidV7.NewGuid(),
                    clinicId,
                    input.ClosingDate,
                    financeExpensesExcluded: !financeEnabled,
                    totalExpected,
                    totalCounted,
                    clinic.Currency,
                    userId.ToString(),
                    now,
                    input.CloseNotes,
                    input.OperationId);
                _dbContext.DailyClosings.Add(existing);
            }
            else
            {
                List<ClosingMethodTotal> oldRows = await _dbContext.ClosingMethodTotals
                    .Where(m => m.ClinicId == clinicId && m.DailyClosingId == existing.Id)
                    .ToListAsync(cancellationToken);
                _dbContext.ClosingMethodTotals.RemoveRange(oldRows);

                existing.Reclose(!financeEnabled, totalExpected, totalCounted, userId.ToString(), now, input.CloseNotes);
            }

            var methodRows = new List<ClosingMethodTotal>(breakdown.Count);
            foreach (ClosingMethodSummary item in breakdown)
            {
                var row = new ClosingMethodTotal(GuidV7.NewGuid(), clinicId, existing.Id, item, clinic.Currency);
                _dbContext.ClosingMethodTotals.Add(row);
                methodRows.Add(row);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "DayClosed",
                "DailyClosing",
                existing.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    existing.ClosingDate,
                    existing.TotalExpectedAmount,
                    existing.TotalCountedAmount,
                    existing.TotalVarianceAmount,
                    existing.FinanceExpensesExcluded
                }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<DailyClosingResult>.Success(MapDailyClosing(existing, methodRows));
        });
    }

    public async Task<Result<DailyClosingResult>> ReopenDayAsync(
        Guid userId,
        Guid clinicId,
        ReopenDayInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        bool financeEnabled = await ExecuteForClinicAsync(
            clinicId,
            () => _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "finance" && m.Enabled, cancellationToken));

        var gate = financeEnabled
            ? await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.reopen_day", "finance", deviceId, cancellationToken)
            : await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.refund", "billing", deviceId, cancellationToken);

        if (gate.IsFailure)
        {
            return Result<DailyClosingResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Result<DailyClosingResult>.Failure(Error.Validation("REOPEN_REASON_REQUIRED", "Reopening a closed day requires a reason."));
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            DailyClosing? existing = await _dbContext.DailyClosings
                .FirstOrDefaultAsync(dc => dc.ClinicId == clinicId && dc.ClosingDate == input.ClosingDate, cancellationToken);
            if (existing == null)
            {
                return Result<DailyClosingResult>.Failure(Error.NotFound("CLOSING_NOT_FOUND", "Daily closing record was not found for this date."));
            }

            if (existing.Status == DailyClosingStatus.Reopened)
            {
                return Result<DailyClosingResult>.Failure(Error.Conflict("DAY_ALREADY_REOPENED", "Daily closing is already reopened."));
            }

            DateTimeOffset now = _clock.UtcNow;
            existing.Reopen(userId.ToString(), input.Reason, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "DayReopened",
                "DailyClosing",
                existing.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { existing.ClosingDate, existing.ReopenReason }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            List<ClosingMethodTotal> rows = await _dbContext.ClosingMethodTotals
                .AsNoTracking()
                .Where(m => m.ClinicId == clinicId && m.DailyClosingId == existing.Id)
                .ToListAsync(cancellationToken);

            return Result<DailyClosingResult>.Success(MapDailyClosing(existing, rows));
        });
    }

    public async Task<Result<ClinicBillingInspectResult>> InspectClinicBillingAsync(
        Guid adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        return await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                int priceCount = await _dbContext.PriceListItems.AsNoTracking().CountAsync(x => x.ClinicId == clinicId && !x.IsVoided, cancellationToken);
                List<Charge> charges = await _dbContext.Charges.AsNoTracking()
                    .Where(c => c.ClinicId == clinicId && !c.IsVoided)
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                decimal totalCollected = await _dbContext.Payments.AsNoTracking()
                    .Where(p => p.ClinicId == clinicId)
                    .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
                decimal totalRefunded = await _dbContext.Refunds.AsNoTracking()
                    .Where(r => r.ClinicId == clinicId)
                    .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;
                decimal totalExpenses = await _dbContext.Expenses.AsNoTracking()
                    .Where(e => e.ClinicId == clinicId && !e.IsVoided)
                    .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
                int suppliersCount = await _dbContext.Suppliers.AsNoTracking()
                    .CountAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

                List<DailyClosing> closings = await _dbContext.DailyClosings.AsNoTracking()
                    .Where(dc => dc.ClinicId == clinicId)
                    .OrderByDescending(dc => dc.ClosingDate)
                    .Take(10)
                    .ToListAsync(cancellationToken);

                var recentClosingResults = new List<DailyClosingResult>(closings.Count);
                foreach (DailyClosing dc in closings)
                {
                    List<ClosingMethodTotal> rows = await _dbContext.ClosingMethodTotals
                        .AsNoTracking()
                        .Where(m => m.ClinicId == clinicId && m.DailyClosingId == dc.Id)
                        .ToListAsync(cancellationToken);
                    recentClosingResults.Add(MapDailyClosing(dc, rows));
                }

                return Result<ClinicBillingInspectResult>.Success(new ClinicBillingInspectResult(
                    ClinicId: clinicId,
                    PriceListCount: priceCount,
                    ChargesCount: charges.Count,
                    TotalNetBilledAmount: Math.Round(charges.Where(c => c.Status == ChargeStatus.Posted).Sum(c => c.NetTotalAmount), 2),
                    TotalCollectedAmount: Math.Round(totalCollected, 2),
                    TotalRefundedAmount: Math.Round(totalRefunded, 2),
                    TotalExpensesAmount: Math.Round(totalExpenses, 2),
                    ClosingsCount: closings.Count,
                    SuppliersCount: suppliersCount,
                    RecentCharges: await ProjectChargesAsync(clinicId, charges.Take(10).ToList(), cancellationToken),
                    RecentClosings: recentClosingResults));
            },
            reason: "Super Admin inspect clinic billing & finance overview",
            actorId: adminId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task<Result<PortalBillingSummaryResult>> GetPatientBillingSummaryAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<PortalBillingSummaryResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            List<Charge> charges = await _dbContext.Charges
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.Status == ChargeStatus.Posted && c.PatientId == patientId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            List<Payment> payments = await _dbContext.Payments
                .AsNoTracking()
                .Where(p => p.ClinicId == clinicId && p.PatientId == patientId)
                .OrderByDescending(p => p.PaidAtUtc)
                .ToListAsync(cancellationToken);

            List<Refund> refunds = await _dbContext.Refunds
                .AsNoTracking()
                .Where(r => r.ClinicId == clinicId && r.PatientId == patientId)
                .ToListAsync(cancellationToken);

            List<LedgerEntry> entries = await _dbContext.LedgerEntries
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && e.PatientId == patientId)
                .ToListAsync(cancellationToken);

            decimal balance = BillingCalculator.DeriveLedgerBalance(entries.Select(e => e.SignedAmount));

            // Strict portal privacy: emit ONLY patient-facing charged totals, paid, remaining, and receipt numbers.
            // Never emit tool costs, markup, lab costs, or internal discount reasons.
            List<PortalBillingChargeItemResult> portalCharges = charges.Select(c => new PortalBillingChargeItemResult(
                ChargeId: c.Id,
                CreatedAtUtc: c.CreatedAtUtc,
                Description: string.IsNullOrWhiteSpace(c.Notes) ? "Clinical Visit Charge" : c.Notes,
                TotalChargedAmount: c.NetTotalAmount,
                PaidAmount: c.PaidAmount,
                RemainingDueAmount: c.GetRemainingDueAmount(),
                Currency: c.Currency)).ToList();

            return Result<PortalBillingSummaryResult>.Success(new PortalBillingSummaryResult(
                ClinicId: clinicId,
                PatientId: patientId,
                TotalChargedAmount: Math.Round(charges.Sum(c => c.NetTotalAmount), 2),
                TotalPaidAmount: Math.Round(payments.Sum(p => p.Amount), 2),
                TotalRefundedAmount: Math.Round(refunds.Sum(r => r.Amount), 2),
                CurrentBalanceDue: balance,
                Currency: clinic.Currency,
                Charges: portalCharges,
                ReceiptNumbers: payments.Select(p => p.ReceiptNumber).ToList()));
        });
    }

    private async Task<string> AllocateNextReceiptNumberAsync(
        Guid clinicId,
        string effectiveDeviceId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        NumberBlock? block = _dbContext.NumberBlocks.Local
            .Where(n => n.ClinicId == clinicId && n.Kind == "Receipt" && n.Status == "Active" && n.DeviceId == effectiveDeviceId && n.NextNumber <= n.ToNumber)
            .OrderBy(n => n.FromNumber)
            .FirstOrDefault()
            ?? await _dbContext.NumberBlocks
                .Where(n => n.ClinicId == clinicId && n.Kind == "Receipt" && n.Status == "Active" && n.DeviceId == effectiveDeviceId)
                .OrderBy(n => n.FromNumber)
                .FirstOrDefaultAsync(cancellationToken);

        if (block == null)
        {
            block = _dbContext.NumberBlocks.Local
                .Where(n => n.ClinicId == clinicId && n.Kind == "Receipt" && n.Status == "Active" && n.NextNumber <= n.ToNumber)
                .OrderBy(n => n.FromNumber)
                .FirstOrDefault()
                ?? await _dbContext.NumberBlocks
                    .Where(n => n.ClinicId == clinicId && n.Kind == "Receipt" && n.Status == "Active")
                    .OrderBy(n => n.FromNumber)
                    .FirstOrDefaultAsync(cancellationToken);
        }

        while (true)
        {
            if (block == null || block.NextNumber > block.ToNumber)
            {
                long? maxDbTo = await _dbContext.NumberBlocks
                    .Where(n => n.ClinicId == clinicId && n.Kind == "Receipt")
                    .MaxAsync(n => (long?)n.ToNumber, cancellationToken);
                long? maxLocalTo = _dbContext.NumberBlocks.Local
                    .Where(n => n.ClinicId == clinicId && n.Kind == "Receipt")
                    .Select(n => (long?)n.ToNumber)
                    .DefaultIfEmpty()
                    .Max();
                long maxTo = Math.Max(maxDbTo ?? 0L, maxLocalTo ?? 0L);

                long fromNum = maxTo + 1L;
                long toNum = fromNum + NumberBlock.DefaultBlockSize - 1L;
                block = new NumberBlock(
                    GuidV7.NewGuid(),
                    clinicId,
                    effectiveDeviceId,
                    "Receipt",
                    fromNum,
                    toNum,
                    nowUtc);
                _dbContext.NumberBlocks.Add(block);
            }

            var consumed = block.ConsumeNext();
            if (consumed.IsFailure)
            {
                block = null;
                continue;
            }

            string candidate = $"R-{consumed.Value:D6}";
            bool isSkipped = _dbContext.SkippedReceiptNumbers.Local
                .Any(s => s.ClinicId == clinicId && s.ReceiptNumber == candidate)
                || await _dbContext.SkippedReceiptNumbers
                    .AsNoTracking()
                    .AnyAsync(s => s.ClinicId == clinicId && s.ReceiptNumber == candidate, cancellationToken);
            bool isAlreadyIssued = _dbContext.Payments.Local
                .Any(p => p.ClinicId == clinicId && p.ReceiptNumber == candidate)
                || _dbContext.Refunds.Local
                    .Any(r => r.ClinicId == clinicId && r.ReceiptNumber == candidate)
                || await _dbContext.Payments
                    .AsNoTracking()
                    .AnyAsync(p => p.ClinicId == clinicId && p.ReceiptNumber == candidate, cancellationToken)
                || await _dbContext.Refunds
                    .AsNoTracking()
                    .AnyAsync(r => r.ClinicId == clinicId && r.ReceiptNumber == candidate, cancellationToken);

            if (!isSkipped && !isAlreadyIssued)
            {
                return candidate;
            }
        }
    }

    private async Task<decimal> ResolveStandardSessionPriceAsync(
        Guid clinicId,
        Guid? appointmentId,
        decimal? explicitStandardPrice,
        CancellationToken cancellationToken)
    {
        if (explicitStandardPrice.HasValue)
        {
            return Math.Max(0m, Math.Round(explicitStandardPrice.Value, 2, MidpointRounding.AwayFromZero));
        }

        if (appointmentId.HasValue && appointmentId.Value != Guid.Empty)
        {
            Appointment? appointment = await _dbContext.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.Id == appointmentId.Value, cancellationToken);
            if (appointment != null)
            {
                AppointmentCategory? category = await _dbContext.AppointmentCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClinicId == clinicId && !c.IsVoided && c.Id == appointment.CategoryId, cancellationToken);
                if (category != null)
                {
                    return category.DefaultPriceAmount;
                }
            }
        }

        return 0m;
    }

    private async Task<bool> IsDateClosedLockedAsync(
        Guid clinicId,
        string clinicTimeZone,
        DateTimeOffset instantUtc,
        CancellationToken cancellationToken)
    {
        DateOnly clinicDate = ToClinicDate(instantUtc, clinicTimeZone);
        return await _dbContext.DailyClosings
            .AsNoTracking()
            .AnyAsync(dc => dc.ClinicId == clinicId && dc.ClosingDate == clinicDate && dc.Status == DailyClosingStatus.Closed, cancellationToken);
    }

    private async Task<IReadOnlyList<ClosingMethodSummary>> ComputeDayBreakdownAsync(
        Guid clinicId,
        DateOnly clinicDate,
        string clinicTimeZone,
        bool includeFinanceExpenses,
        IReadOnlyDictionary<PaymentMethod, decimal>? countedByMethod,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset startUtc, DateTimeOffset endUtc) = ResolveClinicDateWindowUtc(clinicDate, clinicTimeZone);

        List<Payment> payments = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.ClinicId == clinicId && p.PaidAtUtc >= startUtc && p.PaidAtUtc <= endUtc)
            .ToListAsync(cancellationToken);

        List<Refund> refunds = await _dbContext.Refunds
            .AsNoTracking()
            .Where(r => r.ClinicId == clinicId && r.RefundedAtUtc >= startUtc && r.RefundedAtUtc <= endUtc)
            .ToListAsync(cancellationToken);

        Dictionary<PaymentMethod, decimal> paymentsMap = payments
            .GroupBy(p => p.Method)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Amount), 2));

        Dictionary<PaymentMethod, decimal> refundsMap = refunds
            .GroupBy(r => r.Method)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Amount), 2));

        var expensesMap = new Dictionary<PaymentMethod, decimal>();
        if (includeFinanceExpenses)
        {
            List<Expense> cashExpenses = await _dbContext.Expenses
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && !e.IsVoided && e.CountsAsCashOut && e.Method.HasValue && e.OccurredAtUtc >= startUtc && e.OccurredAtUtc <= endUtc)
                .ToListAsync(cancellationToken);

            List<SupplierPayment> supplierPayments = await _dbContext.SupplierPayments
                .AsNoTracking()
                .Where(sp => sp.ClinicId == clinicId && sp.PaidAtUtc >= startUtc && sp.PaidAtUtc <= endUtc)
                .ToListAsync(cancellationToken);

            foreach (PaymentMethod m in Enum.GetValues<PaymentMethod>())
            {
                decimal eSum = cashExpenses.Where(e => e.Method == m).Sum(e => e.Amount);
                decimal sSum = supplierPayments.Where(sp => sp.Method == m).Sum(sp => sp.Amount);
                expensesMap[m] = Math.Round(eSum + sSum, 2);
            }
        }

        return BillingCalculator.ComputeClosingBreakdown(
            paymentsMap,
            refundsMap,
            expensesMap,
            countedByMethod ?? new Dictionary<PaymentMethod, decimal>(),
            includeFinanceExpenses);
    }

    private static DateOnly ToClinicDate(DateTimeOffset utc, string timeZoneId)
    {
        TimeZoneInfo tz = ResolveTimeZone(timeZoneId);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ResolveClinicDateWindowUtc(DateOnly date, string timeZoneId)
    {
        TimeZoneInfo tz = ResolveTimeZone(timeZoneId);
        DateTime localStart = date.ToDateTime(TimeOnly.MinValue);
        DateTime localEnd = date.ToDateTime(TimeOnly.MaxValue);
        DateTimeOffset startUtc = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart)).ToUniversalTime();
        DateTimeOffset endUtc = new DateTimeOffset(localEnd, tz.GetUtcOffset(localEnd)).ToUniversalTime();
        return (startUtc, endUtc);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private async Task<IReadOnlyList<ChargeResult>> ProjectChargesAsync(
        Guid clinicId,
        List<Charge> charges,
        CancellationToken cancellationToken)
    {
        if (charges.Count == 0)
        {
            return Array.Empty<ChargeResult>();
        }

        List<Guid> chargeIds = charges.Select(c => c.Id).ToList();
        List<ChargeLine> lines = await _dbContext.ChargeLines
            .AsNoTracking()
            .Where(l => l.ClinicId == clinicId && chargeIds.Contains(l.ChargeId))
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        ILookup<Guid, ChargeLine> linesByCharge = lines.ToLookup(l => l.ChargeId);

        return charges.Select(c => new ChargeResult(
            c.Id,
            c.ClinicId,
            c.PatientId,
            c.VisitId,
            c.AppointmentId,
            c.DoctorId,
            c.Status,
            c.StandardSessionPriceAmount,
            c.IsFreeSession,
            c.FreeSessionReason,
            c.CustomSessionPriceAmount,
            c.CustomPriceReason,
            c.BaseSessionPriceAmount,
            c.ToolsTotalCostAmount,
            c.ToolsTotalChargedAmount,
            c.ToolsMarkupAmount,
            c.ExtraChargesAmount,
            c.ExtraChargeReason,
            c.GrossSubtotalAmount,
            c.DiscountAmount,
            c.DiscountReason,
            c.UnroundedNetTotalAmount,
            c.NetTotalAmount,
            c.PaidAmount,
            c.RefundedAmount,
            c.GetRemainingDueAmount(),
            c.Currency,
            c.RoundingRuleApplied,
            c.Notes,
            c.CreatedAtUtc,
            linesByCharge[c.Id].Select(l => new ChargeLineResult(
                l.Id, l.Kind, l.PriceListItemId, l.InventoryItemId, l.Description, l.Quantity,
                l.UnitCostAmount, l.UnitChargedPriceAmount, l.LineCostAmount, l.LineTotalAmount, l.MarkupAmount, l.Currency)).ToList())).ToList();
    }

    private async Task<IReadOnlyList<PaymentResult>> ProjectPaymentsAsync(
        Guid clinicId,
        List<Payment> payments,
        CancellationToken cancellationToken)
    {
        if (payments.Count == 0)
        {
            return Array.Empty<PaymentResult>();
        }

        List<Guid> paymentIds = payments.Select(p => p.Id).ToList();
        Dictionary<Guid, Guid> chargeMap = await _dbContext.PaymentAllocations
            .AsNoTracking()
            .Where(a => a.ClinicId == clinicId && paymentIds.Contains(a.PaymentId))
            .GroupBy(a => a.PaymentId)
            .ToDictionaryAsync(g => g.Key, g => g.First().ChargeId, cancellationToken);

        return payments.Select(p => new PaymentResult(
            p.Id,
            p.ClinicId,
            p.PatientId,
            chargeMap.TryGetValue(p.Id, out Guid cid) ? cid : null,
            p.DeviceId,
            p.ReceiptNumber,
            p.Method,
            p.Amount,
            p.Currency,
            p.IsDeposit,
            p.PaymentGroupId,
            p.Notes,
            p.CollectedByUserId,
            p.PaidAtUtc)).ToList();
    }

    private static PriceListItemResult MapPriceListItem(PriceListItem x) => new(
        x.Id, x.ClinicId, x.PriceCode, x.NameAr, x.NameEn, x.Kind, x.UnitCostAmount, x.DefaultPriceAmount, x.Currency, x.IsActive, x.UpdatedAtUtc);

    private static RefundResult MapRefund(Refund r) => new(
        r.Id, r.ClinicId, r.PatientId, r.OriginalPaymentId, r.ChargeId, r.Method, r.Amount, r.Currency, r.Reason, r.ReceiptNumber, r.IssuedByUserId, r.RefundedAtUtc);

    private static LabWorkBillingResult MapLabWork(LabWorkOrder l) => new(
        l.Id, l.ClinicId, l.PatientId, l.VisitId, l.DoctorId, l.SupplierId, l.LabName, l.WorkDescription, l.ToothReference,
        l.LabCostAmount, l.PatientPriceAmount, l.Currency, l.ChargeId, l.ExpenseId, l.CreatedAtUtc);

    private static ExpenseResult MapExpense(Expense e) => new(
        e.Id, e.ClinicId, e.Category, e.Description, e.Method, e.Amount, e.Currency, e.SupplierId, e.LabWorkOrderId, e.CountsAsCashOut, e.RecordedByUserId, e.OccurredAtUtc);

    private static DailyClosingResult MapDailyClosing(DailyClosing dc, IReadOnlyList<ClosingMethodTotal> rows) => new(
        dc.Id,
        dc.ClinicId,
        dc.ClosingDate,
        dc.Status,
        dc.FinanceExpensesExcluded,
        dc.TotalExpectedAmount,
        dc.TotalCountedAmount,
        dc.TotalVarianceAmount,
        dc.Currency,
        dc.ClosedByUserId,
        dc.ClosedAtUtc,
        dc.CloseNotes,
        dc.ReopenedByUserId,
        dc.ReopenedAtUtc,
        dc.ReopenReason,
        rows.Select(m => new ClosingMethodTotalResult(
            m.Method, m.PaymentsTotal, m.RefundsTotal, m.ExpensesTotal, m.ExpectedTotal, m.CountedTotal, m.VarianceAmount, m.Currency)).ToList());

    public async Task<Result<bool>> RecordSupplierPurchaseInvoiceExpenseAsync(
        Guid clinicId,
        Guid supplierId,
        Guid purchaseInvoiceId,
        string invoiceNumber,
        string supplierName,
        Money totalAmount,
        Money paidAmount,
        PaymentMethod? paymentMethod,
        string recordedByUserId,
        Guid? operationId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool financeEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "finance" && m.Enabled, cancellationToken);
            if (!financeEnabled)
            {
                return Result<bool>.Success(false);
            }

            Supplier? supplier = await _dbContext.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.Id == supplierId, cancellationToken);
            if (supplier == null)
            {
                return Result<bool>.Failure(Error.NotFound("SUPPLIER_NOT_FOUND", "Supplier was not found in this clinic."));
            }

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<bool>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            var expense = new Expense(
                GuidV7.NewGuid(),
                clinicId,
                "SupplierInvoice",
                $"Purchase Invoice #{invoiceNumber.Trim()} ({supplierName.Trim()}) [{purchaseInvoiceId:D}]",
                null,
                totalAmount,
                supplierId,
                null,
                false,
                recordedByUserId,
                now,
                operationId);
            _dbContext.Expenses.Add(expense);

            if (paidAmount.Amount > 0m)
            {
                PaymentMethod method = paymentMethod ?? PaymentMethod.Cash;
                var supplierPayment = new SupplierPayment(
                    GuidV7.NewGuid(),
                    clinicId,
                    supplierId,
                    method,
                    paidAmount,
                    $"Purchase Invoice #{invoiceNumber.Trim()}",
                    recordedByUserId,
                    now,
                    operationId);
                _dbContext.SupplierPayments.Add(supplierPayment);
            }

            clinic.MarkHasFinancialEntries();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        });
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
