using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Bounded modular dashboard widget query engine enforcing module flags, permissions, device approval,
/// and 366-day range ceiling while returning aggregate-only payloads.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IModuleCatalogService _moduleCatalog;
    private readonly IClock _clock;

    public DashboardService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IModuleCatalogService moduleCatalog,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<DashboardWidgetCatalogResult>> GetAvailableWidgetsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId,
            clinicId,
            requiredPermission: "dashboard.view",
            requiredModuleKey: null,
            deviceId: deviceId,
            cancellationToken: cancellationToken);

        if (access.IsFailure)
        {
            return Result<DashboardWidgetCatalogResult>.Failure(access.Error);
        }

        var permsResult = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (permsResult.IsFailure)
        {
            return Result<DashboardWidgetCatalogResult>.Failure(permsResult.Error);
        }

        var enabledSet = permsResult.Value.EnabledModules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        enabledSet.Add("core");
        var permSet = permsResult.Value.EffectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var visibleWidgets = _moduleCatalog
            .GetAllWidgetDefinitions()
            .Where(w => enabledSet.Contains(w.ModuleKey) && permSet.Contains(w.RequiredPermission))
            .ToList();

        var pinned = await ExecuteForClinicAsync(clinicId, async () =>
        {
            var layout = await _dbContext.UserDashboardLayouts
                .FirstOrDefaultAsync(l => l.ClinicId == clinicId && l.UserId == userId, cancellationToken);

            if (layout == null)
            {
                return (IReadOnlyList<string>)visibleWidgets.Select(w => w.WidgetKey).ToList();
            }

            try
            {
                var keys = JsonSerializer.Deserialize<List<string>>(layout.PinnedWidgetsJson) ?? new List<string>();
                return (IReadOnlyList<string>)keys.Where(k => visibleWidgets.Any(v => v.WidgetKey == k)).ToList();
            }
            catch (JsonException)
            {
                return (IReadOnlyList<string>)visibleWidgets.Select(w => w.WidgetKey).ToList();
            }
        });

        return Result<DashboardWidgetCatalogResult>.Success(new DashboardWidgetCatalogResult(
            ClinicId: clinicId,
            Widgets: visibleWidgets,
            PinnedWidgetKeys: pinned));
    }

    public async Task<Result<DashboardWidgetDataResult>> QueryWidgetAsync(
        Guid userId,
        Guid clinicId,
        string widgetKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? scope,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var def = _moduleCatalog.GetAllWidgetDefinitions()
            .FirstOrDefault(w => string.Equals(w.WidgetKey, widgetKey?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (def == null)
        {
            return Result<DashboardWidgetDataResult>.Failure(Error.NotFound("WIDGET_NOT_FOUND", $"Dashboard widget '{widgetKey}' was not found."));
        }

        DateTimeOffset end = toUtc ?? _clock.UtcNow;
        DateTimeOffset start = fromUtc ?? end.AddDays(-30);

        if (end < start || (end - start).TotalDays > 366)
        {
            return Result<DashboardWidgetDataResult>.Failure(Error.Validation(
                "WIDGET_DATE_RANGE_EXCEEDED",
                "Dashboard widget date range must be valid and not exceed 366 days."));
        }

        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId,
            clinicId,
            requiredPermission: def.RequiredPermission,
            requiredModuleKey: def.ModuleKey,
            deviceId: deviceId,
            cancellationToken: cancellationToken);

        if (access.IsFailure)
        {
            return Result<DashboardWidgetDataResult>.Failure(access.Error);
        }

        string effectiveScope = string.IsNullOrWhiteSpace(scope) ? def.AllowedScope : scope.Trim();

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            decimal primary = 0m;
            string unit = "count";
            var breakdown = new Dictionary<string, decimal>();

            switch (def.WidgetKey)
            {
                case "core.staff-count":
                {
                    int staffCount = await _dbContext.UserClinics.CountAsync(uc => uc.ClinicId == clinicId, cancellationToken);
                    primary = staffCount;
                    breakdown["staff"] = staffCount;
                    break;
                }
                case "core.roles-count":
                {
                    int rolesCount = await _dbContext.ClinicRoles.CountAsync(r => r.ClinicId == clinicId && !r.IsVoided, cancellationToken);
                    primary = rolesCount;
                    breakdown["roles"] = rolesCount;
                    break;
                }
                case "core.devices-pending":
                {
                    var memberIds = await _dbContext.UserClinics
                        .Where(uc => uc.ClinicId == clinicId)
                        .Select(uc => uc.UserId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    int pendingDevices = await _dbContext.AppDevices
                        .CountAsync(d => memberIds.Contains(d.UserId) && !d.IsApproved && !d.IsRevoked, cancellationToken);
                    primary = pendingDevices;
                    breakdown["pending"] = pendingDevices;
                    break;
                }
                case "core.rooms-count":
                {
                    int rooms = await _dbContext.ClinicRooms.CountAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.IsActive, cancellationToken);
                    primary = rooms;
                    breakdown["rooms"] = rooms;
                    break;
                }
                case "core.doctors-count":
                {
                    int doctors = await _dbContext.ClinicDoctors.CountAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.IsActive, cancellationToken);
                    primary = doctors;
                    breakdown["doctors"] = doctors;
                    break;
                }
                case "patients.summary":
                {
                    int totalPatients = await _dbContext.Patients.CountAsync(p => p.ClinicId == clinicId && !p.IsVoided, cancellationToken);
                    int newPatients = await _dbContext.Patients.CountAsync(
                        p => p.ClinicId == clinicId && !p.IsVoided && p.CreatedAtUtc >= start && p.CreatedAtUtc <= end,
                        cancellationToken);
                    primary = totalPatients;
                    breakdown["total"] = totalPatients;
                    breakdown["newInRange"] = newPatients;
                    break;
                }
                case "patient-history.allergy-count":
                {
                    int totalAllergies = await _dbContext.PatientAllergies.CountAsync(a => a.ClinicId == clinicId && !a.IsVoided, cancellationToken);
                    int severeAllergies = await _dbContext.PatientAllergies.CountAsync(
                        a => a.ClinicId == clinicId && !a.IsVoided && a.Severity == "Severe",
                        cancellationToken);
                    primary = totalAllergies;
                    breakdown["totalAllergies"] = totalAllergies;
                    breakdown["severeAllergies"] = severeAllergies;
                    break;
                }
                case "attachments.uploads-summary":
                {
                    int totalFiles = await _dbContext.PatientAttachments.CountAsync(
                        a => a.ClinicId == clinicId && !a.IsVoided,
                        cancellationToken);
                    long totalBytes = await _dbContext.PatientAttachments
                        .Where(a => a.ClinicId == clinicId && !a.IsVoided)
                        .Select(a => (long?)a.SizeBytes)
                        .SumAsync(cancellationToken) ?? 0L;
                    primary = totalFiles;
                    breakdown["files"] = totalFiles;
                    breakdown["totalKilobytes"] = Math.Round(totalBytes / 1024m, 2);
                    break;
                }
                case "scheduling.appointments-summary":
                {
                    unit = "appointments";
                    List<Appointment> appts = await _dbContext.Appointments
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId && !a.IsVoided && a.StartUtc >= start && a.StartUtc <= end)
                        .ToListAsync(cancellationToken);

                    primary = appts.Count;
                    breakdown["booked"] = appts.Count(a => a.Status == AppointmentStatus.Booked);
                    breakdown["arrived"] = appts.Count(a => a.Status == AppointmentStatus.Arrived);
                    breakdown["inSession"] = appts.Count(a => a.Status == AppointmentStatus.InSession);
                    breakdown["completed"] = appts.Count(a => a.Status == AppointmentStatus.Completed);
                    breakdown["cancelled"] = appts.Count(a => a.Status == AppointmentStatus.Cancelled);
                    breakdown["noShow"] = appts.Count(a => a.Status == AppointmentStatus.NoShow);
                    breakdown["late"] = appts.Count(a => a.Status == AppointmentStatus.Late);
                    breakdown["staffSource"] = appts.Count(a => a.Source == AppointmentSource.Staff);
                    breakdown["portalSource"] = appts.Count(a => a.Source == AppointmentSource.Portal);
                    DateTimeOffset next14End = _clock.UtcNow.AddDays(14);
                    breakdown["next14DaysLoad"] = await _dbContext.Appointments
                        .AsNoTracking()
                        .CountAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.StartUtc >= _clock.UtcNow && a.StartUtc <= next14End, cancellationToken);
                    break;
                }
                case "scheduling.utilization":
                {
                    unit = "minutes";
                    List<Appointment> activeAppts = await _dbContext.Appointments
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId
                                    && !a.IsVoided
                                    && a.StartUtc >= start
                                    && a.StartUtc <= end
                                    && a.Status != AppointmentStatus.Cancelled
                                    && a.Status != AppointmentStatus.NoShow)
                        .ToListAsync(cancellationToken);

                    decimal totalMinutes = activeAppts.Sum(a => (decimal)(a.EndUtc - a.StartUtc).TotalMinutes);
                    int activeDoctors = await _dbContext.ClinicDoctors.AsNoTracking().CountAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.IsActive, cancellationToken);
                    int activeRooms = await _dbContext.ClinicRooms.AsNoTracking().CountAsync(r => r.ClinicId == clinicId && !r.IsVoided && r.IsActive, cancellationToken);
                    primary = totalMinutes;
                    breakdown["bookedMinutes"] = totalMinutes;
                    breakdown["activeDoctors"] = activeDoctors;
                    breakdown["activeRooms"] = activeRooms;
                    breakdown["roomAssignedSessions"] = activeAppts.Count(a => a.RoomId.HasValue);
                    break;
                }
                case "queue.waiting-count":
                {
                    unit = "patients";
                    List<QueueEntry> entries = await _dbContext.QueueEntries
                        .AsNoTracking()
                        .Where(q => q.ClinicId == clinicId && !q.IsVoided && q.CreatedAtUtc >= start && q.CreatedAtUtc <= end)
                        .ToListAsync(cancellationToken);

                    List<QueueEntry> waitingNow = entries.Where(q => q.Status == QueueEntryStatus.Waiting).ToList();
                    primary = waitingNow.Count;
                    breakdown["waiting"] = waitingNow.Count;
                    breakdown["inSession"] = entries.Count(q => q.Status == QueueEntryStatus.InSession);
                    breakdown["served"] = entries.Count(q => q.Status == QueueEntryStatus.Served);
                    breakdown["lateOrNoShow"] = entries.Count(q => q.Status is QueueEntryStatus.Late or QueueEntryStatus.NoShow);
                    breakdown["totalPushes"] = entries.Sum(q => q.PushCount);
                    break;
                }
                case "queue.waiting-list":
                {
                    unit = "entries";
                    int unfulfilledWaitingList = await _dbContext.WaitingListEntries
                        .AsNoTracking()
                        .CountAsync(w => w.ClinicId == clinicId && !w.IsVoided && !w.IsFulfilled, cancellationToken);
                    List<SlotOffer> offers = await _dbContext.SlotOffers
                        .AsNoTracking()
                        .Where(o => o.ClinicId == clinicId && !o.IsVoided && o.OfferedAtUtc >= start && o.OfferedAtUtc <= end)
                        .ToListAsync(cancellationToken);

                    primary = unfulfilledWaitingList;
                    breakdown["waitingListActive"] = unfulfilledWaitingList;
                    breakdown["offersPending"] = offers.Count(o => o.Status == SlotOfferStatus.Pending);
                    breakdown["offersAccepted"] = offers.Count(o => o.Status == SlotOfferStatus.Accepted);
                    break;
                }
                case "clinical.sessions-summary":
                case "clinical.avg-duration":
                case "clinical.overtime-sessions":
                case "clinical.top-diagnoses":
                {
                    Guid? doctorScopeId = await ResolveDoctorScopeIdIfDoctorAsync(userId, clinicId, effectiveScope, cancellationToken);
                    List<Visit> visits = await _dbContext.Visits
                        .AsNoTracking()
                        .Where(v => v.ClinicId == clinicId
                                    && !v.IsVoided
                                    && v.StartedAtUtc >= start
                                    && v.StartedAtUtc <= end
                                    && (!doctorScopeId.HasValue || v.DoctorId == doctorScopeId.Value))
                        .ToListAsync(cancellationToken);

                    if (def.WidgetKey == "clinical.sessions-summary")
                    {
                        unit = "visits";
                        primary = visits.Count;
                        breakdown["inProgress"] = visits.Count(v => v.Status == VisitStatus.InProgress);
                        breakdown["completed"] = visits.Count(v => v.Status == VisitStatus.Completed);
                        breakdown["dentalVisits"] = visits.Count(v => v.SpecialtyCode == "dental");
                    }
                    else if (def.WidgetKey == "clinical.avg-duration")
                    {
                        unit = "minutes";
                        List<Visit> completed = visits.Where(v => v.Status == VisitStatus.Completed && v.EndedAtUtc.HasValue).ToList();
                        decimal avg = completed.Count == 0
                            ? 0m
                            : Math.Round((decimal)completed.Average(v => Math.Max(0, (v.EndedAtUtc!.Value - v.StartedAtUtc).TotalMinutes)), 2);
                        primary = avg;
                        breakdown["completedCount"] = completed.Count;
                        breakdown["averageMinutes"] = avg;
                    }
                    else if (def.WidgetKey == "clinical.overtime-sessions")
                    {
                        unit = "visits";
                        List<Visit> overtimeVisits = visits.Where(v => v.OvertimeMinutes > 0).ToList();
                        primary = overtimeVisits.Count;
                        breakdown["overtimeVisits"] = overtimeVisits.Count;
                        breakdown["excusedCount"] = overtimeVisits.Count(v => v.IsOvertimeExcused);
                        breakdown["totalOvertimeMinutes"] = overtimeVisits.Sum(v => v.OvertimeMinutes);
                    }
                    else
                    {
                        unit = "entries";
                        List<Guid> visitIds = visits.Select(v => v.Id).ToList();
                        int diagnosesCount = await _dbContext.VisitDiagnoses
                            .AsNoTracking()
                            .CountAsync(d => d.ClinicId == clinicId && !d.IsVoided && visitIds.Contains(d.VisitId), cancellationToken);
                        int proceduresCount = await _dbContext.VisitProcedures
                            .AsNoTracking()
                            .CountAsync(p => p.ClinicId == clinicId && !p.IsVoided && visitIds.Contains(p.VisitId), cancellationToken);
                        primary = diagnosesCount + proceduresCount;
                        breakdown["diagnoses"] = diagnosesCount;
                        breakdown["procedures"] = proceduresCount;
                    }

                    break;
                }
                case "billing.revenue-summary":
                {
                    unit = "amount";
                    List<Payment> payments = await _dbContext.Payments
                        .AsNoTracking()
                        .Where(p => p.ClinicId == clinicId && p.PaidAtUtc >= start && p.PaidAtUtc <= end)
                        .ToListAsync(cancellationToken);

                    decimal totalCollected = Math.Round(payments.Sum(p => p.Amount), 2);
                    primary = totalCollected;
                    breakdown["cash"] = Math.Round(payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount), 2);
                    breakdown["visa"] = Math.Round(payments.Where(p => p.Method == PaymentMethod.Visa).Sum(p => p.Amount), 2);
                    breakdown["instapay"] = Math.Round(payments.Where(p => p.Method == PaymentMethod.InstaPay).Sum(p => p.Amount), 2);
                    breakdown["wallet"] = Math.Round(payments.Where(p => p.Method == PaymentMethod.Wallet).Sum(p => p.Amount), 2);
                    breakdown["receiptCount"] = payments.Count;
                    break;
                }
                case "billing.outstanding-debts":
                {
                    unit = "amount";
                    List<Charge> postedCharges = await _dbContext.Charges
                        .AsNoTracking()
                        .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.Status == ChargeStatus.Posted)
                        .ToListAsync(cancellationToken);

                    List<Charge> withDebt = postedCharges.Where(c => c.GetRemainingDueAmount() > 0m).ToList();
                    decimal totalDue = Math.Round(withDebt.Sum(c => c.GetRemainingDueAmount()), 2);
                    DateTimeOffset overdueCutoff = _clock.UtcNow.AddDays(-7);
                    primary = totalDue;
                    breakdown["openChargesCount"] = withDebt.Count;
                    breakdown["overdueChargesCount"] = withDebt.Count(c => c.CreatedAtUtc <= overdueCutoff);
                    break;
                }
                case "billing.refunds-discounts":
                {
                    unit = "amount";
                    List<Refund> refunds = await _dbContext.Refunds
                        .AsNoTracking()
                        .Where(r => r.ClinicId == clinicId && r.RefundedAtUtc >= start && r.RefundedAtUtc <= end)
                        .ToListAsync(cancellationToken);
                    List<Charge> charges = await _dbContext.Charges
                        .AsNoTracking()
                        .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.CreatedAtUtc >= start && c.CreatedAtUtc <= end)
                        .ToListAsync(cancellationToken);

                    decimal refundsTotal = Math.Round(refunds.Sum(r => r.Amount), 2);
                    decimal discountsTotal = Math.Round(charges.Sum(c => c.DiscountAmount), 2);
                    primary = Math.Round(refundsTotal + discountsTotal, 2);
                    breakdown["refundsTotal"] = refundsTotal;
                    breakdown["refundCount"] = refunds.Count;
                    breakdown["discountsTotal"] = discountsTotal;
                    breakdown["freeSessionsCount"] = charges.Count(c => c.IsFreeSession);
                    break;
                }
                case "finance.cash-summary":
                {
                    unit = "amount";
                    decimal cashIn = await _dbContext.Payments
                        .AsNoTracking()
                        .Where(p => p.ClinicId == clinicId && p.Method == PaymentMethod.Cash && p.PaidAtUtc >= start && p.PaidAtUtc <= end)
                        .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
                    decimal cashRefund = await _dbContext.Refunds
                        .AsNoTracking()
                        .Where(r => r.ClinicId == clinicId && r.Method == PaymentMethod.Cash && r.RefundedAtUtc >= start && r.RefundedAtUtc <= end)
                        .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;
                    decimal cashExpenses = await _dbContext.Expenses
                        .AsNoTracking()
                        .Where(e => e.ClinicId == clinicId && !e.IsVoided && e.CountsAsCashOut && e.Method == PaymentMethod.Cash && e.OccurredAtUtc >= start && e.OccurredAtUtc <= end)
                        .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
                    decimal cashSupplierPayments = await _dbContext.SupplierPayments
                        .AsNoTracking()
                        .Where(sp => sp.ClinicId == clinicId && sp.Method == PaymentMethod.Cash && sp.PaidAtUtc >= start && sp.PaidAtUtc <= end)
                        .SumAsync(sp => (decimal?)sp.Amount, cancellationToken) ?? 0m;

                    decimal netCash = Math.Round(cashIn - cashRefund - cashExpenses - cashSupplierPayments, 2);
                    primary = netCash;
                    breakdown["cashIn"] = Math.Round(cashIn, 2);
                    breakdown["cashRefunds"] = Math.Round(cashRefund, 2);
                    breakdown["cashExpenses"] = Math.Round(cashExpenses + cashSupplierPayments, 2);
                    break;
                }
                case "finance.closing-status":
                {
                    unit = "closings";
                    List<DailyClosing> closings = await _dbContext.DailyClosings
                        .AsNoTracking()
                        .Where(dc => dc.ClinicId == clinicId)
                        .OrderByDescending(dc => dc.ClosingDate)
                        .Take(30)
                        .ToListAsync(cancellationToken);

                    primary = closings.Count(dc => dc.Status == DailyClosingStatus.Closed);
                    breakdown["closedDays"] = closings.Count(dc => dc.Status == DailyClosingStatus.Closed);
                    breakdown["reopenedDays"] = closings.Count(dc => dc.Status == DailyClosingStatus.Reopened);
                    breakdown["totalVariance"] = Math.Round(closings.Sum(dc => dc.TotalVarianceAmount), 2);
                    break;
                }
                case "treatment-plans.active-plans":
                {
                    unit = "plans";
                    List<TreatmentPlan> plans = await _dbContext.TreatmentPlans
                        .AsNoTracking()
                        .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided)
                        .ToListAsync(cancellationToken);

                    int activeCount = plans.Count(p => p.Status == TreatmentPlanStatus.Active);
                    primary = activeCount;
                    breakdown["activePlans"] = activeCount;
                    breakdown["completedPlans"] = plans.Count(p => p.Status == TreatmentPlanStatus.Completed);
                    breakdown["totalPlans"] = plans.Count;
                    break;
                }
                case "treatment-plans.completion-rate":
                {
                    unit = "percent";
                    List<TreatmentPlan> plans = await _dbContext.TreatmentPlans
                        .AsNoTracking()
                        .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided)
                        .ToListAsync(cancellationToken);

                    decimal avgCompletion = plans.Count == 0
                        ? 0m
                        : Math.Round(plans.Average(p => p.GetCompletionPercentage()), 2);
                    primary = avgCompletion;
                    breakdown["plansEvaluated"] = plans.Count;
                    breakdown["completedSessions"] = plans.Sum(p => p.CompletedSessionsCount);
                    breakdown["totalSessions"] = plans.Sum(p => p.TotalSessionsCount);
                    break;
                }
                case "treatment-plans.delayed-sessions":
                {
                    unit = "sessions";
                    DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
                    List<PlanSession> sessions = await _dbContext.PlanSessions
                        .AsNoTracking()
                        .Where(s => s.ClinicId == clinicId && !s.IsVoided && s.Status != PlanSessionStatus.Completed && s.Status != PlanSessionStatus.Cancelled)
                        .ToListAsync(cancellationToken);

                    int delayedCount = sessions.Count(s => s.Status == PlanSessionStatus.Delayed || s.TargetDate < today);
                    primary = delayedCount;
                    breakdown["delayedOrPastTarget"] = delayedCount;
                    breakdown["openSessions"] = sessions.Count;
                    break;
                }
                case "treatment-plans.installments-due":
                {
                    unit = "amount";
                    DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
                    List<PlanPaymentScheduleItem> items = await _dbContext.PlanPaymentScheduleItems
                        .AsNoTracking()
                        .Where(i => i.ClinicId == clinicId && !i.IsVoided && i.Status != PlanInstallmentStatus.Paid && i.Status != PlanInstallmentStatus.Refunded)
                        .ToListAsync(cancellationToken);

                    decimal dueAmount = Math.Round(items.Sum(i => i.GetRemainingAmount()), 2);
                    decimal overdueAmount = Math.Round(items.Where(i => i.DueDate < today || i.Status == PlanInstallmentStatus.Overdue).Sum(i => i.GetRemainingAmount()), 2);
                    primary = dueAmount;
                    breakdown["dueAmount"] = dueAmount;
                    breakdown["overdueAmount"] = overdueAmount;
                    breakdown["openItemsCount"] = items.Count;
                    break;
                }
                case "treatment-plans.plan-revenue":
                {
                    unit = "amount";
                    List<TreatmentPlan> plans = await _dbContext.TreatmentPlans
                        .AsNoTracking()
                        .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided)
                        .ToListAsync(cancellationToken);

                    decimal netCollected = Math.Round(plans.Sum(p => p.PaidAmount - p.RefundedAmount), 2);
                    primary = netCollected;
                    breakdown["totalPaid"] = Math.Round(plans.Sum(p => p.PaidAmount), 2);
                    breakdown["totalRefunded"] = Math.Round(plans.Sum(p => p.RefundedAmount), 2);
                    break;
                }
                case "portal.accounts-summary":
                {
                    unit = "accounts";
                    List<PatientAccount> accounts = await _dbContext.PatientAccounts
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId && !a.IsVoided)
                        .ToListAsync(cancellationToken);

                    int invited = accounts.Count(a => a.Status == PatientAccountStatus.Invited || a.Status == PatientAccountStatus.PendingEmailConfirmation);
                    int active = accounts.Count(a => a.Status == PatientAccountStatus.Active);
                    int disabled = accounts.Count(a => a.Status == PatientAccountStatus.Disabled);
                    primary = accounts.Count;
                    breakdown["invited"] = invited;
                    breakdown["active"] = active;
                    breakdown["disabled"] = disabled;
                    break;
                }
                case "portal.logins-week":
                {
                    unit = "logins";
                    DateTimeOffset weekStart = end.AddDays(-7);
                    int logins = await _dbContext.PatientAccessLogs
                        .AsNoTracking()
                        .CountAsync(
                            l => l.ClinicId == clinicId && l.ActorType == "Patient" && l.Reason == "PortalLogin" && l.TimestampUtc >= weekStart && l.TimestampUtc <= end,
                            cancellationToken);
                    primary = logins;
                    breakdown["loginsWeek"] = logins;
                    break;
                }
                case "portal.failed-logins":
                {
                    unit = "attempts";
                    int failed = await _dbContext.PatientAccessLogs
                        .AsNoTracking()
                        .CountAsync(
                            l => l.ClinicId == clinicId && l.ActorType == "Patient" && l.Reason == "PortalLoginFailed" && l.TimestampUtc >= start && l.TimestampUtc <= end,
                            cancellationToken);
                    primary = failed;
                    breakdown["failedLogins"] = failed;
                    break;
                }
                case "portal.record-views-month":
                {
                    unit = "patients";
                    DateTimeOffset monthStart = new DateTimeOffset(end.Year, end.Month, 1, 0, 0, 0, TimeSpan.Zero);
                    string[] viewReasons =
                    [
                        "PortalViewOverview",
                        "PortalViewRecord",
                        "PortalListVisits",
                        "PortalViewVisit",
                        "PortalViewPlans",
                        "PortalDownloadPrescriptionPdf",
                        "PortalDownloadAttachment"
                    ];
                    List<string> actorIds = await _dbContext.PatientAccessLogs
                        .AsNoTracking()
                        .Where(l => l.ClinicId == clinicId
                                    && l.ActorType == "Patient"
                                    && l.TimestampUtc >= monthStart
                                    && l.TimestampUtc <= end
                                    && viewReasons.Contains(l.Reason))
                        .Select(l => l.ActorId)
                        .ToListAsync(cancellationToken);

                    int distinctPatients = actorIds.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    primary = distinctPatients;
                    breakdown["distinctPatients"] = distinctPatients;
                    breakdown["totalReadEvents"] = actorIds.Count;
                    break;
                }
                case "portal.access-log-summary":
                {
                    unit = "events";
                    List<PatientAccessLog> logs = await _dbContext.PatientAccessLogs
                        .AsNoTracking()
                        .Where(l => l.ClinicId == clinicId && l.ActorType == "Patient" && l.TimestampUtc >= start && l.TimestampUtc <= end)
                        .ToListAsync(cancellationToken);

                    primary = logs.Count;
                    breakdown["logins"] = logs.Count(l => l.Reason == "PortalLogin");
                    breakdown["failedLogins"] = logs.Count(l => l.Reason == "PortalLoginFailed");
                    breakdown["recordViews"] = logs.Count(l => l.Reason == "PortalViewRecord" || l.Reason == "PortalListVisits" || l.Reason == "PortalViewVisit" || l.Reason == "PortalViewPlans" || l.Reason == "PortalViewOverview");
                    breakdown["downloads"] = logs.Count(l => l.Reason == "PortalDownloadPrescriptionPdf" || l.Reason == "PortalDownloadAttachment");
                    break;
                }
                case "portal-booking.daily-count":
                {
                    unit = "bookings";
                    List<Appointment> portalAppts = await _dbContext.Appointments
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId
                                    && !a.IsVoided
                                    && a.Source == AppointmentSource.Portal
                                    && a.StartUtc >= start
                                    && a.StartUtc <= end)
                        .ToListAsync(cancellationToken);

                    primary = portalAppts.Count;
                    breakdown["confirmed"] = portalAppts.Count(a => a.Status is AppointmentStatus.Booked or AppointmentStatus.Arrived or AppointmentStatus.InSession or AppointmentStatus.Completed);
                    breakdown["pending"] = portalAppts.Count(a => a.Status == AppointmentStatus.Requested);
                    breakdown["cancelledOrDeclined"] = portalAppts.Count(a => a.Status is AppointmentStatus.Cancelled or AppointmentStatus.Declined);
                    break;
                }
                case "portal-booking.cancellations":
                {
                    unit = "cancellations";
                    int cancelledCount = await _dbContext.Appointments
                        .AsNoTracking()
                        .CountAsync(
                            a => a.ClinicId == clinicId
                                 && !a.IsVoided
                                 && a.Source == AppointmentSource.Portal
                                 && a.Status == AppointmentStatus.Cancelled
                                 && a.UpdatedAtUtc >= start
                                 && a.UpdatedAtUtc <= end,
                            cancellationToken);

                    primary = cancelledCount;
                    breakdown["cancelledCount"] = cancelledCount;
                    break;
                }
                case "portal-booking.pending-requests":
                {
                    unit = "requests";
                    DateTimeOffset nowUtc = _clock.UtcNow;
                    List<Appointment> requested = await _dbContext.Appointments
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId
                                    && !a.IsVoided
                                    && a.Source == AppointmentSource.Portal
                                    && a.Status == AppointmentStatus.Requested)
                        .ToListAsync(cancellationToken);

                    int activePending = requested.Count(a => !a.IsPortalRequestExpired(nowUtc));
                    int expired = requested.Count(a => a.IsPortalRequestExpired(nowUtc));
                    primary = activePending;
                    breakdown["pending"] = activePending;
                    breakdown["expired"] = expired;
                    break;
                }
                case "portal-booking.no-shows":
                {
                    unit = "no-shows";
                    int portalNoShows = await _dbContext.Appointments
                        .AsNoTracking()
                        .CountAsync(
                            a => a.ClinicId == clinicId
                                 && !a.IsVoided
                                 && a.Source == AppointmentSource.Portal
                                 && a.Status == AppointmentStatus.NoShow
                                 && a.StartUtc >= start
                                 && a.StartUtc <= end,
                            cancellationToken);
                    int blockedAccounts = await _dbContext.PatientAccounts
                        .AsNoTracking()
                        .CountAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.IsOnlineBookingBlocked, cancellationToken);

                    primary = portalNoShows;
                    breakdown["portalNoShows"] = portalNoShows;
                    breakdown["blockedAccounts"] = blockedAccounts;
                    break;
                }
                case "portal-booking.slot-utilization":
                {
                    unit = "%";
                    DateTimeOffset nowUtc = _clock.UtcNow;
                    Clinic? clinic = await _dbContext.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
                    TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(clinic?.TimeZone);
                    DateTimeOffset localNow = TimeZoneInfo.ConvertTime(nowUtc, tz);
                    DateTime localDayStartUnspecified = new(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    DateTimeOffset dayStartUtc = new DateTimeOffset(localDayStartUnspecified, tz.GetUtcOffset(localDayStartUnspecified)).ToUniversalTime();
                    DateTimeOffset dayEndUtc = new DateTimeOffset(localDayStartUnspecified.AddDays(1), tz.GetUtcOffset(localDayStartUnspecified.AddDays(1))).ToUniversalTime();

                    ClinicPortalBookingSetting bookingSettings = await PortalBookingHelper.GetOrBuildDefaultBookingSettingsAsync(
                        _dbContext, clinicId, nowUtc, cancellationToken);
                    List<Appointment> todayPortal = await _dbContext.Appointments
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId
                                    && !a.IsVoided
                                    && a.Source == AppointmentSource.Portal
                                    && a.StartUtc >= dayStartUtc
                                    && a.StartUtc < dayEndUtc)
                        .ToListAsync(cancellationToken);

                    int occupiedToday = todayPortal.Count(a => AppointmentStateMachine.IsOccupying(a, nowUtc));
                    decimal cap = Math.Max(1, bookingSettings.MaxBookingsPerDay);
                    decimal utilization = Math.Min(100m, Math.Round((occupiedToday / cap) * 100m, 2));
                    primary = utilization;
                    breakdown["occupiedToday"] = occupiedToday;
                    breakdown["maxBookingsPerDay"] = bookingSettings.MaxBookingsPerDay;
                    break;
                }
                case "portal-booking.portal-share":
                {
                    unit = "%";
                    List<Appointment> allRange = await _dbContext.Appointments
                        .AsNoTracking()
                        .Where(a => a.ClinicId == clinicId
                                    && !a.IsVoided
                                    && a.StartUtc >= start
                                    && a.StartUtc <= end)
                        .ToListAsync(cancellationToken);

                    int total = allRange.Count;
                    int portalCount = allRange.Count(a => a.Source == AppointmentSource.Portal);
                    decimal sharePct = total == 0 ? 0m : Math.Round((portalCount * 100m) / total, 2);
                    primary = sharePct;
                    breakdown["portalBookings"] = portalCount;
                    breakdown["totalBookings"] = total;
                    break;
                }
                default:
                {
                    breakdown["configured"] = 1m;
                    break;
                }
            }

            return Result<DashboardWidgetDataResult>.Success(new DashboardWidgetDataResult(
                WidgetKey: def.WidgetKey,
                ModuleKey: def.ModuleKey,
                Scope: effectiveScope,
                FromUtc: start,
                ToUtc: end,
                TitleAr: def.TitleAr,
                TitleEn: def.TitleEn,
                PrimaryValue: primary,
                UnitLabel: unit,
                Breakdown: breakdown));
        });
    }

    public async Task<Result<IReadOnlyList<string>>> SaveLayoutAsync(
        Guid userId,
        Guid clinicId,
        IReadOnlyList<string> pinnedWidgetKeys,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var access = await _authorizationService.EnsureClinicAccessAsync(
            userId,
            clinicId,
            requiredPermission: "dashboard.view",
            requiredModuleKey: null,
            deviceId: deviceId,
            cancellationToken: cancellationToken);

        if (access.IsFailure)
        {
            return Result<IReadOnlyList<string>>.Failure(access.Error);
        }

        var validKeys = _moduleCatalog.GetAllWidgetDefinitions()
            .Select(w => w.WidgetKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sanitized = (pinnedWidgetKeys ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k) && validKeys.Contains(k.Trim()))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string json = JsonSerializer.Serialize(sanitized);
        DateTimeOffset now = _clock.UtcNow;

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            var layout = await _dbContext.UserDashboardLayouts
                .FirstOrDefaultAsync(l => l.ClinicId == clinicId && l.UserId == userId, cancellationToken);

            if (layout == null)
            {
                _dbContext.UserDashboardLayouts.Add(new UserDashboardLayout(
                    GuidV7.NewGuid(),
                    clinicId,
                    userId,
                    json,
                    now));
            }
            else
            {
                layout.UpdateWidgets(json, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        });

        return Result<IReadOnlyList<string>>.Success(sanitized);
    }

    private async Task<Guid?> ResolveDoctorScopeIdIfDoctorAsync(
        Guid userId,
        Guid clinicId,
        string effectiveScope,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(effectiveScope, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var perms = await _authorizationService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (perms.IsSuccess && perms.Value.IsOwner)
        {
            return null;
        }

        ClinicDoctor? doc = await _dbContext.ClinicDoctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ClinicId == clinicId && !d.IsVoided && d.UserId == userId, cancellationToken);

        return doc?.Id;
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
