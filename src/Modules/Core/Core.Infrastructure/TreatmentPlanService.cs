using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

public sealed class TreatmentPlanService : ITreatmentPlanService, ITreatmentPlanPortalQueries
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly ISchedulingService _schedulingService;
    private readonly IBillingService _billingService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public TreatmentPlanService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        ISchedulingService schedulingService,
        IBillingService billingService,
        IPlatformAccess platformAccess,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext;
        _currentClinicContext = currentClinicContext;
        _authorizationService = authorizationService;
        _schedulingService = schedulingService;
        _billingService = billingService;
        _platformAccess = platformAccess;
        _auditService = auditService;
        _clock = clock;
    }

    public async Task<Result<TreatmentPlanResult>> CreatePlanAsync(
        Guid userId,
        Guid clinicId,
        CreateTreatmentPlanInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "treatment_plans.write", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Name) || input.Sessions.Count == 0)
        {
            return Result<TreatmentPlanResult>.Failure(Error.Validation("PLAN_INVALID", "Treatment plan name and at least one session are required."));
        }

        Result<TreatmentPlanResult>? idempotentResult = null;
        Result<TreatmentPlanResult> txOutcome = await ExecuteForClinicAsync(clinicId, async () =>
        {
            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? processed = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == input.OperationId.Value, cancellationToken);
                if (processed != null)
                {
                    TreatmentPlan? existing = await _dbContext.TreatmentPlans
                        .AsNoTracking()
                        .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && tp.Id == processed.ResultEntityId, cancellationToken);
                    if (existing != null)
                    {
                        idempotentResult = Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, existing, cancellationToken));
                        return idempotentResult;
                    }
                }
            }

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            bool patientExists = await _dbContext.Patients
                .AsNoTracking()
                .AnyAsync(p => p.ClinicId == clinicId && !p.IsVoided && p.Id == input.PatientId, cancellationToken);
            if (!patientExists)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PATIENT_NOT_FOUND", "Patient was not found."));
            }

            bool doctorExists = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .AnyAsync(d => d.ClinicId == clinicId && d.Id == input.DoctorId, cancellationToken);
            if (!doctorExists)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Doctor was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            DateOnly todayClinic = ToClinicDate(now, clinic.TimeZone);
            Guid planId = input.Id.HasValue && input.Id.Value != Guid.Empty ? input.Id.Value : GuidV7.NewGuid();
            bool showCost = input.ShowCostToPatient ?? clinic.PortalShowPlanCostsByDefault;

            var specs = input.Sessions
                .OrderBy(s => s.SequenceNumber)
                .Select(s => (s.SequenceNumber, s.IntervalDaysFromPrevious, s.WindowMinusDays, s.WindowPlusDays))
                .ToList();

            IReadOnlyList<PlanSessionWindowCalculation> windows = TreatmentPlanCalculator.CalculateSessionWindows(input.StartDate, specs);
            Dictionary<int, PlanSessionWindowCalculation> windowMap = windows.ToDictionary(w => w.SequenceNumber);

            decimal initialMaterialsCost = input.Mode == TreatmentPlanMode.FixedTotal
                ? Math.Max(input.IncludedMaterialsCostAmount, input.Sessions.Sum(s => Math.Max(0m, s.MaterialsCostAmount)))
                : input.Sessions.Sum(s => Math.Max(0m, s.MaterialsCostAmount));

            var plan = new TreatmentPlan(
                planId,
                clinicId,
                input.PatientId,
                input.DoctorId,
                input.Name,
                input.Mode,
                input.FixedTotalAmount,
                initialMaterialsCost,
                input.UpfrontAmount,
                clinic.Currency,
                input.Sessions.Count,
                showCost,
                input.Notes,
                userId.ToString(),
                input.OperationId,
                now);

            _dbContext.TreatmentPlans.Add(plan);

            var sessionEntities = new List<PlanSession>(input.Sessions.Count);
            foreach (CreatePlanSessionInput s in input.Sessions.OrderBy(x => x.SequenceNumber))
            {
                PlanSessionWindowCalculation win = windowMap[s.SequenceNumber];
                decimal price = input.Mode == TreatmentPlanMode.FixedTotal ? 0m : Math.Max(0m, s.SessionPriceAmount);
                var session = new PlanSession(
                    GuidV7.NewGuid(),
                    clinicId,
                    plan.Id,
                    s.SequenceNumber,
                    s.Title,
                    win,
                    price,
                    s.MaterialsCostAmount,
                    s.Notes,
                    now);
                _dbContext.PlanSessions.Add(session);
                sessionEntities.Add(session);
            }

            var installmentEntities = new List<PlanPaymentScheduleItem>();
            bool hasExplicitUpfront = input.Installments.Any(i => i.IsUpfront || i.InstallmentNumber == 0);
            if (input.UpfrontAmount > 0m && !hasExplicitUpfront)
            {
                var upfrontItem = new PlanPaymentScheduleItem(
                    GuidV7.NewGuid(),
                    clinicId,
                    plan.Id,
                    0,
                    true,
                    input.StartDate,
                    input.UpfrontAmount,
                    clinic.Currency,
                    todayClinic,
                    now);
                _dbContext.PlanPaymentScheduleItems.Add(upfrontItem);
                installmentEntities.Add(upfrontItem);
            }

            foreach (CreatePlanInstallmentInput inst in input.Installments.OrderBy(i => i.InstallmentNumber))
            {
                if (inst.Amount <= 0m)
                {
                    continue;
                }
                var item = new PlanPaymentScheduleItem(
                    GuidV7.NewGuid(),
                    clinicId,
                    plan.Id,
                    inst.InstallmentNumber,
                    inst.IsUpfront || inst.InstallmentNumber == 0,
                    inst.DueDate,
                    inst.Amount,
                    clinic.Currency,
                    todayClinic,
                    now);
                _dbContext.PlanPaymentScheduleItems.Add(item);
                installmentEntities.Add(item);
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "CreateTreatmentPlan", plan.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "TreatmentPlanCreated",
                "TreatmentPlan",
                plan.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { plan.Id, plan.PatientId, Mode = plan.Mode.ToString(), plan.FixedTotalAmount, plan.TotalSessionsCount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<TreatmentPlanResult>.Success(MapPlan(plan, sessionEntities, installmentEntities));
        });

        return idempotentResult ?? txOutcome;
    }

    public async Task<Result<TreatmentPlanResult>> GetPlanAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "treatment_plans.read", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            TreatmentPlan? plan = await _dbContext.TreatmentPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null || (scopedDoctorId.HasValue && plan.DoctorId != scopedDoctorId.Value))
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, plan, cancellationToken));
        });
    }

    public async Task<Result<IReadOnlyList<TreatmentPlanResult>>> ListPlansAsync(
        Guid userId,
        Guid clinicId,
        Guid? patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "treatment_plans.read", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<TreatmentPlanResult>>.Failure(gate.Error);
        }

        Guid? scopedDoctorId = await ResolveScopedDoctorIdAsync(userId, clinicId, cancellationToken);
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<TreatmentPlan> query = _dbContext.TreatmentPlans
                .AsNoTracking()
                .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided);

            if (patientId.HasValue && patientId.Value != Guid.Empty)
            {
                query = query.Where(tp => tp.PatientId == patientId.Value);
            }
            if (scopedDoctorId.HasValue)
            {
                query = query.Where(tp => tp.DoctorId == scopedDoctorId.Value);
            }

            List<TreatmentPlan> plans = await query.OrderByDescending(tp => tp.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);
            var results = new List<TreatmentPlanResult>(plans.Count);
            foreach (TreatmentPlan p in plans)
            {
                results.Add(await BuildPlanResultAsync(clinicId, p, cancellationToken));
            }
            return Result<IReadOnlyList<TreatmentPlanResult>>.Success(results);
        });
    }

    public async Task<Result<PlanSuggestedSlotResult>> SuggestOrBookSessionAppointmentAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        SchedulePlanSessionInput? bookingInput,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        string permission = bookingInput != null ? "treatment_plans.write" : "treatment_plans.read";
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, permission, "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PlanSuggestedSlotResult>.Failure(gate.Error);
        }

        TreatmentPlan? plan = null;
        PlanSession? session = null;
        List<DateOnly> candidates = [];

        Result<PlanSuggestedSlotResult>? failure = await ExecuteForClinicAsync(clinicId, async () =>
        {
            plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null)
            {
                return Result<PlanSuggestedSlotResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            session = await _dbContext.PlanSessions
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.TreatmentPlanId == planId && s.Id == sessionId, cancellationToken);
            if (session == null)
            {
                return Result<PlanSuggestedSlotResult>.Failure(Error.NotFound("PLAN_SESSION_NOT_FOUND", "Plan session was not found."));
            }

            List<ClinicDayOff> daysOff = await _dbContext.ClinicDaysOff
                .AsNoTracking()
                .Where(d => d.ClinicId == clinicId && !d.IsVoided && (d.DoctorId == null || d.DoctorId == plan.DoctorId))
                .ToListAsync(cancellationToken);

            List<ClinicWorkingHour> workingHours = await _dbContext.ClinicWorkingHours
                .AsNoTracking()
                .Where(w => w.ClinicId == clinicId && !w.IsVoided && !w.IsClosed)
                .ToListAsync(cancellationToken);

            HashSet<DayOfWeek> openDays = workingHours.Count == 0
                ? new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }
                : workingHours.Select(w => (DayOfWeek)w.DayOfWeek).ToHashSet();

            DateOnly cursor = session.EarliestAllowedDate;
            while (cursor <= session.LatestAllowedDate)
            {
                string cursorIso = cursor.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                bool blockedOff = daysOff.Any(d =>
                    string.CompareOrdinal(cursorIso, d.StartDate) >= 0 &&
                    string.CompareOrdinal(cursorIso, d.EndDate) <= 0);
                if (openDays.Contains(cursor.DayOfWeek) && !blockedOff)
                {
                    candidates.Add(cursor);
                }
                cursor = cursor.AddDays(1);
            }

            if (candidates.Count == 0)
            {
                candidates.Add(session.TargetDate);
            }
            candidates = candidates
                .OrderBy(d => Math.Abs(d.DayNumber - session.TargetDate.DayNumber))
                .ToList();

            return null;
        });

        if (failure != null)
        {
            return failure;
        }

        AppointmentItemResult? bookedAppt = null;
        if (bookingInput != null && bookingInput.CategoryId != Guid.Empty)
        {
            DateOnly chosenDate = candidates[0];
            DateTimeOffset startUtc = bookingInput.PreferredStartUtc
                ?? new DateTimeOffset(chosenDate.Year, chosenDate.Month, chosenDate.Day, 10, 0, 0, TimeSpan.Zero);

            Result<AppointmentItemResult> bookRes = await _schedulingService.BookAppointmentAsync(
                userId,
                clinicId,
                new BookAppointmentInput(
                    Id: null,
                    PatientId: plan!.PatientId,
                    DoctorId: plan.DoctorId,
                    RoomId: bookingInput.RoomId,
                    CategoryId: bookingInput.CategoryId,
                    StartUtc: startUtc,
                    EndUtc: null,
                    Priority: "Normal",
                    PlanSessionId: session!.Id,
                    Notes: $"Plan session #{session.SequenceNumber}: {session.Title}",
                    OperationId: bookingInput.OperationId),
                deviceId,
                ipAddress,
                requestId,
                cancellationToken);

            if (bookRes.IsFailure)
            {
                return Result<PlanSuggestedSlotResult>.Failure(bookRes.Error);
            }

            bookedAppt = bookRes.Value;
            await ExecuteForClinicAsync(clinicId, async () =>
            {
                PlanSession? trackedSession = await _dbContext.PlanSessions
                    .FirstOrDefaultAsync(s => s.ClinicId == clinicId && s.Id == session.Id, cancellationToken);
                if (trackedSession != null)
                {
                    trackedSession.LinkAppointment(bookedAppt.Id, _clock.UtcNow);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                return true;
            });
        }

        return Result<PlanSuggestedSlotResult>.Success(new PlanSuggestedSlotResult(
            session!.Id,
            session.SequenceNumber,
            session.TargetDate,
            session.EarliestAllowedDate,
            session.LatestAllowedDate,
            candidates,
            bookedAppt));
    }

    public async Task<Result<TreatmentPlanResult>> RecordSessionSlipAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        RecordPlanSessionSlipInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "treatment_plans.write", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            TreatmentPlan? plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            List<PlanSession> sessions = await _dbContext.PlanSessions
                .Where(s => s.ClinicId == clinicId && !s.IsVoided && s.TreatmentPlanId == planId)
                .OrderBy(s => s.SequenceNumber)
                .ToListAsync(cancellationToken);

            PlanSession? slipped = sessions.FirstOrDefault(s => s.Id == sessionId);
            if (slipped == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_SESSION_NOT_FOUND", "Plan session was not found."));
            }

            var calcInputs = sessions
                .Select(s => new PlanSessionWindowCalculation(
                    s.SequenceNumber,
                    s.IntervalDaysFromPrevious,
                    s.WindowMinusDays,
                    s.WindowPlusDays,
                    s.TargetDate,
                    s.EarliestAllowedDate,
                    s.LatestAllowedDate))
                .ToList();

            IReadOnlyList<PlanSessionWindowCalculation> recalculated =
                TreatmentPlanCalculator.RecalculateAfterSlip(calcInputs, slipped.SequenceNumber, input.ActualOrSlippedDate);

            Dictionary<int, PlanSessionWindowCalculation> map = recalculated.ToDictionary(x => x.SequenceNumber);
            DateTimeOffset now = _clock.UtcNow;

            foreach (PlanSession s in sessions)
            {
                if (s.SequenceNumber < slipped.SequenceNumber || s.Status == PlanSessionStatus.Completed)
                {
                    continue;
                }

                PlanSessionWindowCalculation nextWin = map[s.SequenceNumber];
                bool isSlippedOne = s.Id == slipped.Id;
                s.ApplyRecalculatedWindow(
                    nextWin,
                    markDelayed: isSlippedOne,
                    slippedDate: isSlippedOne ? input.ActualOrSlippedDate : null,
                    nowUtc: now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PlanSessionSlipped",
                "PlanSession",
                slipped.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { planId, slipped.SequenceNumber, input.ActualOrSlippedDate, input.Reason }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, plan, cancellationToken));
        });
    }

    public async Task<Result<TreatmentPlanResult>> CompletePlanSessionAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        CompletePlanSessionInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "treatment_plans.write", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        TreatmentPlan? plan = null;
        PlanSession? session = null;

        Result<TreatmentPlanResult>? preError = await ExecuteForClinicAsync(clinicId, async () =>
        {
            plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            session = await _dbContext.PlanSessions
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.TreatmentPlanId == planId && s.Id == sessionId, cancellationToken);
            if (session == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_SESSION_NOT_FOUND", "Plan session was not found."));
            }

            if (session.Status == PlanSessionStatus.Completed)
            {
                return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, plan, cancellationToken));
            }

            return null;
        });

        if (preError != null)
        {
            return preError;
        }

        Guid? createdChargeId = null;
        if (plan!.Mode == TreatmentPlanMode.PerSession && session!.SessionPriceAmount > 0m)
        {
            Result<ChargeResult> chargeRes = await _billingService.CreateChargeAsync(
                userId,
                clinicId,
                new CreateChargeInput(
                    Id: null,
                    PatientId: plan.PatientId,
                    VisitId: input.VisitId,
                    AppointmentId: session.LinkedAppointmentId,
                    DoctorId: plan.DoctorId,
                    StandardSessionPriceAmount: session.SessionPriceAmount,
                    IsFreeSession: false,
                    FreeSessionReason: null,
                    CustomSessionPriceAmount: null,
                    CustomPriceReason: null,
                    DiscountPercent: null,
                    DiscountFixedAmount: null,
                    DiscountReason: null,
                    ExtraChargeAmount: null,
                    ExtraChargeReason: null,
                    Lines: null,
                    Notes: $"Treatment Plan '{plan.Name}' Session #{session.SequenceNumber}",
                    PostImmediately: true,
                    OperationId: input.OperationId),
                deviceId,
                ipAddress,
                requestId,
                cancellationToken);

            if (chargeRes.IsFailure)
            {
                return Result<TreatmentPlanResult>.Failure(chargeRes.Error);
            }

            createdChargeId = chargeRes.Value.Id;
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            TreatmentPlan? trackedPlan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && tp.Id == planId, cancellationToken);
            List<PlanSession> allSessions = await _dbContext.PlanSessions
                .Where(s => s.ClinicId == clinicId && !s.IsVoided && s.TreatmentPlanId == planId)
                .OrderBy(s => s.SequenceNumber)
                .ToListAsync(cancellationToken);

            PlanSession? trackedSession = allSessions.FirstOrDefault(s => s.Id == sessionId);
            if (trackedPlan == null || trackedSession == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            DateTimeOffset now = _clock.UtcNow;
            trackedSession.Complete(input.CompletedDate, input.VisitId, createdChargeId, input.AdditionalMaterialsCostAmount, now);
            if (input.AdditionalMaterialsCostAmount.HasValue && input.AdditionalMaterialsCostAmount.Value > 0m)
            {
                trackedPlan.AddMaterialsCost(input.AdditionalMaterialsCostAmount.Value, now);
            }

            int completedCount = allSessions.Count(s => s.Status == PlanSessionStatus.Completed);
            trackedPlan.MarkSessionCompleted(completedCount, now);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PlanSessionCompleted",
                "PlanSession",
                trackedSession.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { planId, trackedSession.SequenceNumber, Mode = trackedPlan.Mode.ToString(), ChargeId = createdChargeId }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, trackedPlan, cancellationToken));
        });
    }

    public async Task<Result<TreatmentPlanResult>> PayInstallmentAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid installmentId,
        PayPlanInstallmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.write", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        if (input.Amount <= 0m)
        {
            return Result<TreatmentPlanResult>.Failure(Error.Validation("INSTALLMENT_AMOUNT_INVALID", "Payment amount must be positive."));
        }

        TreatmentPlan? plan = null;
        PlanPaymentScheduleItem? installment = null;
        string clinicTimeZone = "Africa/Cairo";

        Result<TreatmentPlanResult>? preError = await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic != null)
            {
                clinicTimeZone = clinic.TimeZone;
            }

            plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            installment = await _dbContext.PlanPaymentScheduleItems
                .FirstOrDefaultAsync(i => i.ClinicId == clinicId && !i.IsVoided && i.TreatmentPlanId == planId && i.Id == installmentId, cancellationToken);
            if (installment == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("INSTALLMENT_NOT_FOUND", "Plan installment was not found."));
            }

            return null;
        });

        if (preError != null)
        {
            return preError;
        }

        Result<IReadOnlyList<PaymentResult>> payRes = await _billingService.RecordPaymentAsync(
            userId,
            clinicId,
            new RecordPaymentInput(
                Id: null,
                PatientId: plan!.PatientId,
                ChargeId: null,
                IsDeposit: installment!.IsUpfront,
                Splits: [new PaymentSplitInput(input.Method, input.Amount, input.Notes ?? $"Plan '{plan.Name}' Installment #{installment.InstallmentNumber}")],
                OperationId: input.OperationId),
            deviceId,
            ipAddress,
            requestId,
            cancellationToken);

        if (payRes.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(payRes.Error);
        }

        PaymentResult recordedPayment = payRes.Value[0];
        DateTimeOffset now = _clock.UtcNow;
        DateOnly todayClinic = ToClinicDate(now, clinicTimeZone);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            TreatmentPlan? trackedPlan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && tp.Id == planId, cancellationToken);
            PlanPaymentScheduleItem? trackedInst = await _dbContext.PlanPaymentScheduleItems
                .FirstOrDefaultAsync(i => i.ClinicId == clinicId && i.Id == installmentId, cancellationToken);
            if (trackedPlan == null || trackedInst == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            trackedInst.ApplyPayment(input.Amount, recordedPayment.Id, todayClinic, now);
            trackedPlan.RecordPayment(input.Amount, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, trackedPlan, cancellationToken));
        });
    }

    public async Task<Result<TreatmentPlanResult>> RefundInstallmentAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid installmentId,
        RefundPlanDepositInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "billing.refund", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        if (input.Amount <= 0m || string.IsNullOrWhiteSpace(input.Reason))
        {
            return Result<TreatmentPlanResult>.Failure(Error.Validation("REFUND_INVALID", "Refund amount and reason are required."));
        }

        TreatmentPlan? plan = null;
        PlanPaymentScheduleItem? installment = null;
        Payment? originalPayment = null;
        string clinicTimeZone = "Africa/Cairo";

        Result<TreatmentPlanResult>? preError = await ExecuteForClinicAsync(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic != null)
            {
                clinicTimeZone = clinic.TimeZone;
            }

            plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            installment = await _dbContext.PlanPaymentScheduleItems
                .FirstOrDefaultAsync(i => i.ClinicId == clinicId && !i.IsVoided && i.TreatmentPlanId == planId && i.Id == installmentId, cancellationToken);
            if (installment == null || !installment.LinkedPaymentId.HasValue)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("INSTALLMENT_PAYMENT_NOT_FOUND", "Installment has no recorded payment to refund."));
            }

            originalPayment = await _dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.Id == installment.LinkedPaymentId.Value, cancellationToken);
            if (originalPayment == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PAYMENT_NOT_FOUND", "Original payment was not found."));
            }

            return null;
        });

        if (preError != null)
        {
            return preError;
        }

        Result<RefundResult> refundRes = await _billingService.CreateRefundAsync(
            userId,
            clinicId,
            new CreateRefundInput(
                Id: null,
                PatientId: plan!.PatientId,
                OriginalPaymentId: originalPayment!.Id,
                ChargeId: null,
                Method: originalPayment.Method,
                Amount: input.Amount,
                Reason: input.Reason,
                OperationId: input.OperationId),
            deviceId,
            ipAddress,
            requestId,
            cancellationToken);

        if (refundRes.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(refundRes.Error);
        }

        DateTimeOffset now = _clock.UtcNow;
        DateOnly todayClinic = ToClinicDate(now, clinicTimeZone);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            TreatmentPlan? trackedPlan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && tp.Id == planId, cancellationToken);
            PlanPaymentScheduleItem? trackedInst = await _dbContext.PlanPaymentScheduleItems
                .FirstOrDefaultAsync(i => i.ClinicId == clinicId && i.Id == installmentId, cancellationToken);
            if (trackedPlan == null || trackedInst == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            trackedInst.ReversePayment(input.Amount, todayClinic, now);
            trackedPlan.RecordRefund(input.Amount, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, trackedPlan, cancellationToken));
        });
    }

    public async Task<Result<TreatmentPlanResult>> SetPortalCostVisibilityAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        bool showCostToPatient,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "treatment_plans.write", "treatment-plans", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<TreatmentPlanResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            TreatmentPlan? plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.Id == planId, cancellationToken);
            if (plan == null)
            {
                return Result<TreatmentPlanResult>.Failure(Error.NotFound("PLAN_NOT_FOUND", "Treatment plan was not found."));
            }

            plan.SetShowCostToPatient(showCostToPatient, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PlanPortalCostVisibilityChanged",
                "TreatmentPlan",
                plan.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { plan.Id, plan.ShowCostToPatient }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<TreatmentPlanResult>.Success(await BuildPlanResultAsync(clinicId, plan, cancellationToken));
        });
    }

    public async Task<Result<IReadOnlyList<PortalTreatmentPlanItemResult>>> GetPatientTreatmentPlansForPortalAsync(
        Guid clinicId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            bool moduleEnabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == "treatment-plans" && m.Enabled, cancellationToken);

            if (!moduleEnabled)
            {
                return Result<IReadOnlyList<PortalTreatmentPlanItemResult>>.Success(Array.Empty<PortalTreatmentPlanItemResult>());
            }

            List<TreatmentPlan> plans = await _dbContext.TreatmentPlans
                .AsNoTracking()
                .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided && tp.PatientId == patientId)
                .OrderByDescending(tp => tp.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var portalPlans = new List<PortalTreatmentPlanItemResult>(plans.Count);
            foreach (TreatmentPlan plan in plans)
            {
                List<PlanSession> sessions = await _dbContext.PlanSessions
                    .AsNoTracking()
                    .Where(s => s.ClinicId == clinicId && !s.IsVoided && s.TreatmentPlanId == plan.Id)
                    .OrderBy(s => s.SequenceNumber)
                    .ToListAsync(cancellationToken);

                List<decimal> postedCharges = await ResolvePostedPerSessionAmountsAsync(clinicId, plan, sessions, cancellationToken);
                PlanBalanceSummary balance = plan.ComputeBalance(postedCharges);

                // Strictly hide all material costs from the portal; expose patient cost only if ShowCostToPatient == true.
                List<PortalPlanSessionItemResult> portalSessions = sessions.Select(s => new PortalPlanSessionItemResult(
                    SessionId: s.Id,
                    SequenceNumber: s.SequenceNumber,
                    Title: s.Title,
                    TargetDate: s.TargetDate,
                    EarliestAllowedDate: s.EarliestAllowedDate,
                    LatestAllowedDate: s.LatestAllowedDate,
                    Status: s.Status,
                    LinkedAppointmentId: s.LinkedAppointmentId,
                    SessionPriceAmount: plan.ShowCostToPatient && plan.Mode == TreatmentPlanMode.PerSession ? s.SessionPriceAmount : null)).ToList();

                portalPlans.Add(new PortalTreatmentPlanItemResult(
                    PlanId: plan.Id,
                    Name: plan.Name,
                    Mode: plan.Mode,
                    Status: plan.Status,
                    TotalSessionsCount: plan.TotalSessionsCount,
                    CompletedSessionsCount: plan.CompletedSessionsCount,
                    CompletionPercentage: plan.GetCompletionPercentage(),
                    ShowCostToPatient: plan.ShowCostToPatient,
                    TotalCostToPatient: plan.ShowCostToPatient ? balance.TotalBilledOrContractedAmount : null,
                    PaidAmountToPatient: plan.ShowCostToPatient ? balance.NetPaidAmount : null,
                    RemainingBalanceToPatient: plan.ShowCostToPatient ? balance.RemainingBalanceAmount : null,
                    Currency: plan.ShowCostToPatient ? plan.Currency : null,
                    Sessions: portalSessions));
            }

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                $"Patient:{patientId}",
                "PortalViewTreatmentPlans",
                _clock.UtcNow,
                clinicId,
                JsonSerializer.Serialize(new { PatientId = patientId, PlansCount = portalPlans.Count }),
                actorType: "Patient"));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<IReadOnlyList<PortalTreatmentPlanItemResult>>.Success(portalPlans);
        });
    }

    public async Task<Result<ClinicTreatmentPlansInspectResult>> InspectClinicTreatmentPlansAsSuperAdminAsync(
        Guid superAdminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        return await _platformAccess.ExecuteAsPlatformAdminAsync(async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null)
            {
                return Result<ClinicTreatmentPlansInspectResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
            }

            List<TreatmentPlan> plans = await _dbContext.TreatmentPlans
                .AsNoTracking()
                .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided)
                .OrderByDescending(tp => tp.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            int delayedSessions = await _dbContext.PlanSessions
                .AsNoTracking()
                .CountAsync(s => s.ClinicId == clinicId && !s.IsVoided && s.Status == PlanSessionStatus.Delayed, cancellationToken);

            var recent = new List<TreatmentPlanResult>();
            foreach (TreatmentPlan p in plans.Take(20))
            {
                recent.Add(await BuildPlanResultAsync(clinicId, p, cancellationToken));
            }

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                superAdminId.ToString(),
                "SuperAdminInspectTreatmentPlans",
                _clock.UtcNow,
                clinicId,
                JsonSerializer.Serialize(new { PlansCount = plans.Count })));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ClinicTreatmentPlansInspectResult>.Success(new ClinicTreatmentPlansInspectResult(
                ClinicId: clinicId,
                TotalPlansCount: plans.Count,
                ActivePlansCount: plans.Count(p => p.Status == TreatmentPlanStatus.Active),
                CompletedPlansCount: plans.Count(p => p.Status == TreatmentPlanStatus.Completed),
                DelayedSessionsCount: delayedSessions,
                RecentPlans: recent));
        }, "SuperAdminInspectTreatmentPlans", superAdminId.ToString(), cancellationToken);
    }

    private async Task<TreatmentPlanResult> BuildPlanResultAsync(
        Guid clinicId,
        TreatmentPlan plan,
        CancellationToken cancellationToken)
    {
        List<PlanSession> sessions = await _dbContext.PlanSessions
            .AsNoTracking()
            .Where(s => s.ClinicId == clinicId && !s.IsVoided && s.TreatmentPlanId == plan.Id)
            .OrderBy(s => s.SequenceNumber)
            .ToListAsync(cancellationToken);

        List<PlanPaymentScheduleItem> installments = await _dbContext.PlanPaymentScheduleItems
            .AsNoTracking()
            .Where(i => i.ClinicId == clinicId && !i.IsVoided && i.TreatmentPlanId == plan.Id)
            .OrderBy(i => i.InstallmentNumber)
            .ToListAsync(cancellationToken);

        return MapPlan(plan, sessions, installments, await ResolvePostedPerSessionAmountsAsync(clinicId, plan, sessions, cancellationToken));
    }

    private async Task<List<decimal>> ResolvePostedPerSessionAmountsAsync(
        Guid clinicId,
        TreatmentPlan plan,
        IReadOnlyList<PlanSession> sessions,
        CancellationToken cancellationToken)
    {
        if (plan.Mode != TreatmentPlanMode.PerSession)
        {
            return [];
        }

        List<Guid> linkedChargeIds = sessions
            .Where(s => s.LinkedChargeId.HasValue)
            .Select(s => s.LinkedChargeId!.Value)
            .ToList();

        if (linkedChargeIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Charges
            .AsNoTracking()
            .Where(c => c.ClinicId == clinicId && !c.IsVoided && linkedChargeIds.Contains(c.Id))
            .Select(c => c.NetTotalAmount)
            .ToListAsync(cancellationToken);
    }

    private static TreatmentPlanResult MapPlan(
        TreatmentPlan plan,
        IReadOnlyList<PlanSession> sessions,
        IReadOnlyList<PlanPaymentScheduleItem> installments,
        IReadOnlyList<decimal>? perSessionCharges = null)
    {
        PlanBalanceSummary balance = plan.ComputeBalance(perSessionCharges ?? Array.Empty<decimal>());
        return new TreatmentPlanResult(
            Id: plan.Id,
            ClinicId: plan.ClinicId,
            PatientId: plan.PatientId,
            DoctorId: plan.DoctorId,
            Name: plan.Name,
            Mode: plan.Mode,
            FixedTotalAmount: plan.FixedTotalAmount,
            IncludedMaterialsCostAmount: plan.IncludedMaterialsCostAmount,
            UpfrontAmount: plan.UpfrontAmount,
            PaidAmount: plan.PaidAmount,
            RefundedAmount: plan.RefundedAmount,
            TotalBilledOrContractedAmount: balance.TotalBilledOrContractedAmount,
            RemainingBalanceAmount: balance.RemainingBalanceAmount,
            Currency: plan.Currency,
            Status: plan.Status,
            TotalSessionsCount: plan.TotalSessionsCount,
            CompletedSessionsCount: plan.CompletedSessionsCount,
            RemainingSessionsCount: Math.Max(0, plan.TotalSessionsCount - plan.CompletedSessionsCount),
            CompletionPercentage: plan.GetCompletionPercentage(),
            ShowCostToPatient: plan.ShowCostToPatient,
            Notes: plan.Notes,
            CreatedAtUtc: plan.CreatedAtUtc,
            Sessions: sessions.Select(s => new PlanSessionResult(
                s.Id, s.SequenceNumber, s.Title, s.IntervalDaysFromPrevious, s.WindowMinusDays, s.WindowPlusDays,
                s.TargetDate, s.EarliestAllowedDate, s.LatestAllowedDate, s.ActualOrSlippedDate,
                s.SessionPriceAmount, s.MaterialsCostAmount, s.LinkedAppointmentId, s.LinkedVisitId, s.LinkedChargeId, s.Status, s.Notes)).ToList(),
            Installments: installments.Select(i => new PlanInstallmentResult(
                i.Id, i.InstallmentNumber, i.IsUpfront, i.DueDate, i.Amount, i.PaidAmount, i.GetRemainingAmount(), i.Currency, i.Status, i.LinkedPaymentId, i.PaidAtUtc)).ToList());
    }

    private async Task<Guid?> ResolveScopedDoctorIdAsync(Guid userId, Guid clinicId, CancellationToken cancellationToken)
    {
        return await ExecuteForClinicAsync<Guid?>(clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            if (clinic == null || !clinic.ScopePatientsByDoctor)
            {
                return null;
            }

            UserEffectivePermissionsResult perms = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken)
                is { IsSuccess: true } r
                ? r.Value
                : null!;

            if (perms != null && perms.EffectivePermissions.Contains("clinic.settings.edit"))
            {
                return null;
            }

            ClinicDoctor? doc = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ClinicId == clinicId && d.UserId == userId, cancellationToken);
            return doc?.Id;
        });
    }

    private static DateOnly ToClinicDate(DateTimeOffset utcInstant, string timeZoneId)
    {
        try
        {
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Africa/Cairo" : timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcInstant, tz).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(utcInstant.UtcDateTime);
        }
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
