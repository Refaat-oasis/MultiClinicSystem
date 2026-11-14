using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 14 extended clinic finance: FixedCosts with recurrence, Employees, PayrollRuns/Adjustments, and Income vs Expenses.
/// Strictly excludes any doctor commission.
/// </summary>
public sealed class FinanceExtendedService : IFinanceExtendedService
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public FinanceExtendedService(
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

    public async Task<Result<IReadOnlyList<FixedCostResult>>> ListFixedCostsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<FixedCostResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<FixedCost> rows = await _dbContext.FixedCosts
                .AsNoTracking()
                .Where(f => f.ClinicId == clinicId && !f.IsVoided)
                .OrderBy(f => f.Category)
                .ThenBy(f => f.TitleEn)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<FixedCostResult>>.Success(rows.Select(MapFixedCost).ToList());
        });
    }

    public async Task<Result<FixedCostResult>> UpsertFixedCostAsync(
        Guid userId,
        Guid clinicId,
        UpsertFixedCostInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<FixedCostResult>.Failure(gate.Error);
        }

        if ((string.IsNullOrWhiteSpace(input.TitleAr) && string.IsNullOrWhiteSpace(input.TitleEn)) || input.Amount <= 0m)
        {
            return Result<FixedCostResult>.Failure(Error.Validation("FIXED_COST_INVALID", "Title and positive Amount are required."));
        }

        if (input.EffectiveToDate.HasValue && input.EffectiveToDate.Value < input.EffectiveFromDate)
        {
            return Result<FixedCostResult>.Failure(Error.Validation("FIXED_COST_DATE_RANGE_INVALID", "EffectiveToDate cannot precede EffectiveFromDate."));
        }

        FixedCostResult saved = await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    FixedCost? existingRow = await _dbContext.FixedCosts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(f => f.ClinicId == clinicId && f.Id == existingOp.ResultEntityId && !f.IsVoided, cancellationToken);
                    if (existingRow != null)
                    {
                        return MapFixedCost(existingRow);
                    }
                }
            }

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string currency = clinic?.Currency ?? Money.DefaultCurrency;
            var money = new Money(input.Amount, currency);

            FixedCost? entity = null;
            if (input.FixedCostId.HasValue && input.FixedCostId.Value != Guid.Empty)
            {
                entity = await _dbContext.FixedCosts
                    .FirstOrDefaultAsync(f => f.ClinicId == clinicId && f.Id == input.FixedCostId.Value && !f.IsVoided, cancellationToken);
            }

            if (entity == null)
            {
                entity = new FixedCost(
                    input.FixedCostId ?? GuidV7.NewGuid(),
                    clinicId,
                    input.Category,
                    input.TitleAr,
                    input.TitleEn,
                    money,
                    input.Recurrence,
                    input.PaidFromMethod,
                    input.EffectiveFromDate,
                    input.EffectiveToDate,
                    input.Notes,
                    input.OperationId);
                entity.SetActive(input.IsActive);
                _dbContext.FixedCosts.Add(entity);
            }
            else
            {
                entity.Update(
                    input.Category,
                    input.TitleAr,
                    input.TitleEn,
                    money,
                    input.Recurrence,
                    input.PaidFromMethod,
                    input.EffectiveFromDate,
                    input.EffectiveToDate,
                    input.IsActive,
                    input.Notes);
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "UpsertFixedCost", entity.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "FixedCostUpserted",
                "FixedCost",
                entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { entity.Id, entity.Category, entity.Amount, entity.Recurrence }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return MapFixedCost(entity);
        });

        return Result<FixedCostResult>.Success(saved);
    }

    public async Task<Result<bool>> VoidFixedCostAsync(
        Guid userId,
        Guid clinicId,
        Guid fixedCostId,
        string reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<bool>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<bool>.Failure(Error.Validation("VOID_REASON_REQUIRED", "Void reason is required."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            FixedCost? entity = await _dbContext.FixedCosts
                .FirstOrDefaultAsync(f => f.ClinicId == clinicId && f.Id == fixedCostId && !f.IsVoided, cancellationToken);
            if (entity == null)
            {
                return Result<bool>.Failure(Error.NotFound("FIXED_COST_NOT_FOUND", "Fixed cost was not found."));
            }

            entity.IsVoided = true;
            entity.VoidedAtUtc = _clock.UtcNow;
            entity.VoidedBy = userId.ToString();
            entity.VoidReason = reason.Trim();
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "FixedCostVoided",
                "FixedCost",
                entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { entity.Id, entity.VoidReason }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<bool>.Success(true);
        });
    }

    public async Task<Result<IReadOnlyList<FixedCostOccurrenceResult>>> ListFixedCostOccurrencesAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<FixedCostOccurrenceResult>>.Failure(gate.Error);
        }

        if (toDate < fromDate)
        {
            return Result<IReadOnlyList<FixedCostOccurrenceResult>>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<FixedCost> defs = await _dbContext.FixedCosts
                .AsNoTracking()
                .Where(f => f.ClinicId == clinicId && !f.IsVoided && f.IsActive)
                .ToListAsync(cancellationToken);

            var occ = FinanceCalculator.ExpandFixedCosts(defs, fromDate, toDate)
                .Select(o => new FixedCostOccurrenceResult(
                    o.FixedCostId, o.Category, o.TitleAr, o.TitleEn, o.OccurrenceDate, o.Amount, o.Currency, o.PaidFromMethod))
                .ToList();

            return Result<IReadOnlyList<FixedCostOccurrenceResult>>.Success(occ);
        });
    }

    public async Task<Result<IReadOnlyList<EmployeeResult>>> ListEmployeesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<EmployeeResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<Employee> rows = await _dbContext.Employees
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId && !e.IsVoided)
                .OrderBy(e => e.FullName)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<EmployeeResult>>.Success(rows.Select(MapEmployee).ToList());
        });
    }

    public async Task<Result<EmployeeResult>> UpsertEmployeeAsync(
        Guid userId,
        Guid clinicId,
        UpsertEmployeeInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<EmployeeResult>.Failure(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(input.FullName) || input.BaseSalaryAmount < 0m)
        {
            return Result<EmployeeResult>.Failure(Error.Validation("EMPLOYEE_INVALID", "FullName and non-negative BaseSalaryAmount are required."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    Employee? existingEmp = await _dbContext.Employees
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.ClinicId == clinicId && e.Id == existingOp.ResultEntityId && !e.IsVoided, cancellationToken);
                    if (existingEmp != null)
                    {
                        return Result<EmployeeResult>.Success(MapEmployee(existingEmp));
                    }
                }
            }

            if (input.DoctorId.HasValue)
            {
                bool doctorExists = await _dbContext.ClinicDoctors
                    .AsNoTracking()
                    .AnyAsync(d => d.ClinicId == clinicId && d.Id == input.DoctorId.Value && !d.IsVoided, cancellationToken);
                if (!doctorExists)
                {
                    return Result<EmployeeResult>.Failure(Error.NotFound("DOCTOR_NOT_FOUND", "Linked clinic doctor was not found."));
                }
            }

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string currency = clinic?.Currency ?? Money.DefaultCurrency;
            var baseSalary = new Money(input.BaseSalaryAmount, currency);

            Employee? entity = null;
            if (input.EmployeeId.HasValue && input.EmployeeId.Value != Guid.Empty)
            {
                entity = await _dbContext.Employees
                    .FirstOrDefaultAsync(e => e.ClinicId == clinicId && e.Id == input.EmployeeId.Value && !e.IsVoided, cancellationToken);
            }

            if (entity == null)
            {
                entity = new Employee(
                    input.EmployeeId ?? GuidV7.NewGuid(),
                    clinicId,
                    input.FullName,
                    input.RoleTitle,
                    input.DoctorId,
                    input.Phone,
                    baseSalary,
                    input.PaidFromMethod,
                    input.HiredOnDate,
                    input.OperationId);
                entity.Update(input.FullName, input.RoleTitle, input.DoctorId, input.Phone, baseSalary, input.PaidFromMethod, input.IsActive);
                _dbContext.Employees.Add(entity);
            }
            else
            {
                entity.Update(
                    input.FullName,
                    input.RoleTitle,
                    input.DoctorId,
                    input.Phone,
                    baseSalary,
                    input.PaidFromMethod,
                    input.IsActive);
            }

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "UpsertEmployee", entity.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "EmployeeUpserted",
                "Employee",
                entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { entity.Id, entity.RoleTitle, entity.BaseSalaryAmount, entity.IsActive }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<EmployeeResult>.Success(MapEmployee(entity));
        });
    }

    public async Task<Result<IReadOnlyList<PayrollAdjustmentResult>>> ListPayrollAdjustmentsAsync(
        Guid userId,
        Guid clinicId,
        int? periodYear,
        int? periodMonth,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayrollAdjustmentResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            IQueryable<PayrollAdjustment> q = _dbContext.PayrollAdjustments
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided);
            if (periodYear.HasValue)
            {
                q = q.Where(a => a.PeriodYear == periodYear.Value);
            }

            if (periodMonth.HasValue)
            {
                q = q.Where(a => a.PeriodMonth == periodMonth.Value);
            }

            List<PayrollAdjustment> adjustments = await q
                .OrderByDescending(a => a.OccurredOnDate)
                .Take(300)
                .ToListAsync(cancellationToken);

            Dictionary<Guid, string> empNames = await _dbContext.Employees
                .AsNoTracking()
                .Where(e => e.ClinicId == clinicId)
                .ToDictionaryAsync(e => e.Id, e => e.FullName, cancellationToken);

            var rows = adjustments.Select(a => new PayrollAdjustmentResult(
                a.Id,
                a.ClinicId,
                a.EmployeeId,
                empNames.TryGetValue(a.EmployeeId, out string? name) ? name : "Unknown",
                a.PayrollRunId,
                a.PeriodYear,
                a.PeriodMonth,
                a.AdjustmentType,
                a.Amount,
                a.Currency,
                a.Reason,
                a.OccurredOnDate)).ToList();

            return Result<IReadOnlyList<PayrollAdjustmentResult>>.Success(rows);
        });
    }

    public async Task<Result<PayrollAdjustmentResult>> RecordPayrollAdjustmentAsync(
        Guid userId,
        Guid clinicId,
        RecordPayrollAdjustmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PayrollAdjustmentResult>.Failure(gate.Error);
        }

        if (input.EmployeeId == Guid.Empty || input.Amount <= 0m || input.PeriodMonth is < 1 or > 12)
        {
            return Result<PayrollAdjustmentResult>.Failure(Error.Validation("PAYROLL_ADJUSTMENT_INVALID", "Valid EmployeeId, PeriodMonth (1-12), and positive Amount are required."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    PayrollAdjustment? existing = await _dbContext.PayrollAdjustments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.ClinicId == clinicId && a.Id == existingOp.ResultEntityId && !a.IsVoided, cancellationToken);
                    if (existing != null)
                    {
                        string empName = await _dbContext.Employees
                            .AsNoTracking()
                            .Where(e => e.ClinicId == clinicId && e.Id == existing.EmployeeId)
                            .Select(e => e.FullName)
                            .FirstOrDefaultAsync(cancellationToken) ?? "Staff";

                        return Result<PayrollAdjustmentResult>.Success(new PayrollAdjustmentResult(
                            existing.Id, existing.ClinicId, existing.EmployeeId, empName, existing.PayrollRunId,
                            existing.PeriodYear, existing.PeriodMonth, existing.AdjustmentType, existing.Amount,
                            existing.Currency, existing.Reason, existing.OccurredOnDate));
                    }
                }
            }

            Employee? employee = await _dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ClinicId == clinicId && e.Id == input.EmployeeId && !e.IsVoided, cancellationToken);
            if (employee == null)
            {
                return Result<PayrollAdjustmentResult>.Failure(Error.NotFound("EMPLOYEE_NOT_FOUND", "Employee was not found."));
            }

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string currency = clinic?.Currency ?? employee.Currency;

            var adjustment = new PayrollAdjustment(
                GuidV7.NewGuid(),
                clinicId,
                employee.Id,
                input.PeriodYear,
                input.PeriodMonth,
                input.AdjustmentType,
                new Money(input.Amount, currency),
                input.Reason,
                input.OccurredOnDate,
                userId.ToString(),
                input.OperationId);
            _dbContext.PayrollAdjustments.Add(adjustment);

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "RecordPayrollAdjustment", adjustment.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PayrollAdjustmentRecorded",
                "PayrollAdjustment",
                adjustment.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { adjustment.Id, adjustment.EmployeeId, adjustment.AdjustmentType, adjustment.Amount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PayrollAdjustmentResult>.Success(new PayrollAdjustmentResult(
                adjustment.Id, adjustment.ClinicId, adjustment.EmployeeId, employee.FullName, adjustment.PayrollRunId,
                adjustment.PeriodYear, adjustment.PeriodMonth, adjustment.AdjustmentType, adjustment.Amount,
                adjustment.Currency, adjustment.Reason, adjustment.OccurredOnDate));
        });
    }

    public async Task<Result<IReadOnlyList<PayrollRunResult>>> ListPayrollRunsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayrollRunResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<PayrollRun> runs = await _dbContext.PayrollRuns
                .AsNoTracking()
                .Where(r => r.ClinicId == clinicId && !r.IsVoided)
                .OrderByDescending(r => r.PeriodYear)
                .ThenByDescending(r => r.PeriodMonth)
                .ThenByDescending(r => r.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            List<Guid> runIds = runs.Select(r => r.Id).ToList();
            List<PayrollLine> lines = runIds.Count == 0
                ? []
                : await _dbContext.PayrollLines
                    .AsNoTracking()
                    .Where(l => l.ClinicId == clinicId && !l.IsVoided && runIds.Contains(l.PayrollRunId))
                    .OrderBy(l => l.EmployeeNameSnapshot)
                    .ToListAsync(cancellationToken);

            var mapped = runs.Select(r => MapPayrollRun(r, lines.Where(l => l.PayrollRunId == r.Id).ToList())).ToList();
            return Result<IReadOnlyList<PayrollRunResult>>.Success(mapped);
        });
    }

    public async Task<Result<PayrollRunResult>> CreatePayrollRunAsync(
        Guid userId,
        Guid clinicId,
        CreatePayrollRunInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PayrollRunResult>.Failure(gate.Error);
        }

        if (input.PeriodMonth is < 1 or > 12 || input.PeriodYear is < 2020 or > 2100)
        {
            return Result<PayrollRunResult>.Failure(Error.Validation("PAYROLL_PERIOD_INVALID", "Valid PeriodYear and PeriodMonth (1-12) are required."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == input.OperationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    PayrollRun? existingRun = await _dbContext.PayrollRuns
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.ClinicId == clinicId && r.Id == existingOp.ResultEntityId && !r.IsVoided, cancellationToken);
                    if (existingRun != null)
                    {
                        List<PayrollLine> existingLines = await _dbContext.PayrollLines
                            .AsNoTracking()
                            .Where(l => l.ClinicId == clinicId && l.PayrollRunId == existingRun.Id && !l.IsVoided)
                            .ToListAsync(cancellationToken);
                        return Result<PayrollRunResult>.Success(MapPayrollRun(existingRun, existingLines));
                    }
                }
            }

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string currency = clinic?.Currency ?? Money.DefaultCurrency;

            var run = new PayrollRun(
                GuidV7.NewGuid(),
                clinicId,
                input.PeriodYear,
                input.PeriodMonth,
                input.PaidFromMethod,
                currency,
                userId.ToString(),
                now,
                input.Notes,
                input.OperationId);
            _dbContext.PayrollRuns.Add(run);

            List<PayrollLine> generatedLines = await RebuildPayrollLinesAsync(clinicId, run, attachAdjustments: false, cancellationToken);

            if (input.OperationId.HasValue && input.OperationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, input.OperationId.Value, "CreatePayrollRun", run.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PayrollRunCreated",
                "PayrollRun",
                run.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { run.Id, run.PeriodYear, run.PeriodMonth, Lines = generatedLines.Count }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PayrollRunResult>.Success(MapPayrollRun(run, generatedLines));
        });
    }

    public async Task<Result<PayrollRunResult>> FinalizePayrollRunAsync(
        Guid userId,
        Guid clinicId,
        Guid payrollRunId,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.write", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PayrollRunResult>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;

            if (operationId.HasValue && operationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == operationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    PayrollRun? existingRun = await _dbContext.PayrollRuns
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.ClinicId == clinicId && r.Id == existingOp.ResultEntityId && !r.IsVoided, cancellationToken);
                    if (existingRun != null)
                    {
                        List<PayrollLine> existingLines = await _dbContext.PayrollLines
                            .AsNoTracking()
                            .Where(l => l.ClinicId == clinicId && l.PayrollRunId == existingRun.Id && !l.IsVoided)
                            .ToListAsync(cancellationToken);
                        return Result<PayrollRunResult>.Success(MapPayrollRun(existingRun, existingLines));
                    }
                }
            }

            PayrollRun? run = await _dbContext.PayrollRuns
                .FirstOrDefaultAsync(r => r.ClinicId == clinicId && r.Id == payrollRunId && !r.IsVoided, cancellationToken);
            if (run == null)
            {
                return Result<PayrollRunResult>.Failure(Error.NotFound("PAYROLL_RUN_NOT_FOUND", "Payroll run was not found."));
            }

            if (run.Status == PayrollRunStatus.Finalized)
            {
                List<PayrollLine> finalizedLines = await _dbContext.PayrollLines
                    .AsNoTracking()
                    .Where(l => l.ClinicId == clinicId && l.PayrollRunId == run.Id && !l.IsVoided)
                    .ToListAsync(cancellationToken);
                return Result<PayrollRunResult>.Success(MapPayrollRun(run, finalizedLines));
            }

            // Void previous draft lines cleanly before regenerating finalized lines
            List<PayrollLine> oldDraftLines = await _dbContext.PayrollLines
                .Where(l => l.ClinicId == clinicId && l.PayrollRunId == run.Id && !l.IsVoided)
                .ToListAsync(cancellationToken);
            foreach (var oldLine in oldDraftLines)
            {
                oldLine.IsVoided = true;
                oldLine.VoidedAtUtc = now;
                oldLine.VoidedBy = userId.ToString();
                oldLine.VoidReason = "Recalculated at payroll finalization";
            }

            List<PayrollLine> finalLines = await RebuildPayrollLinesAsync(clinicId, run, attachAdjustments: true, cancellationToken);

            decimal totalBase = finalLines.Sum(l => l.BaseSalaryAmount);
            decimal totalBonuses = finalLines.Sum(l => l.BonusTotalAmount);
            decimal totalDeductions = finalLines.Sum(l => l.DeductionTotalAmount);
            decimal totalAdvances = finalLines.Sum(l => l.AdvanceTotalAmount);
            decimal totalNet = finalLines.Sum(l => l.NetPayAmount);

            run.FinalizeRun(totalBase, totalBonuses, totalDeductions, totalAdvances, totalNet, userId.ToString(), now);

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            clinic?.MarkHasFinancialEntries();

            if (operationId.HasValue && operationId.Value != Guid.Empty)
            {
                _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                    GuidV7.NewGuid(), clinicId, operationId.Value, "FinalizePayrollRun", run.Id, now));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PayrollRunFinalized",
                "PayrollRun",
                run.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { run.Id, run.PeriodYear, run.PeriodMonth, run.TotalNetPay }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PayrollRunResult>.Success(MapPayrollRun(run, finalLines));
        });
    }

    public async Task<Result<IncomeVsExpensesReportResult>> GetIncomeVsExpensesAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "finance.read", "finance", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IncomeVsExpensesReportResult>.Failure(gate.Error);
        }

        if (toDate < fromDate)
        {
            return Result<IncomeVsExpensesReportResult>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            var summary = await ReportsDapperQueries.QueryIncomeVsExpensesAsync(_dbContext, clinicId, fromDate, toDate, cancellationToken);
            return Result<IncomeVsExpensesReportResult>.Success(summary);
        });
    }

    private async Task<List<PayrollLine>> RebuildPayrollLinesAsync(
        Guid clinicId,
        PayrollRun run,
        bool attachAdjustments,
        CancellationToken cancellationToken)
    {
        List<Employee> employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.ClinicId == clinicId && !e.IsVoided && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);

        List<PayrollAdjustment> adjustments = await _dbContext.PayrollAdjustments
            .Where(a => a.ClinicId == clinicId &&
                        !a.IsVoided &&
                        a.PeriodYear == run.PeriodYear &&
                        a.PeriodMonth == run.PeriodMonth &&
                        (a.PayrollRunId == null || a.PayrollRunId == run.Id))
            .ToListAsync(cancellationToken);

        var lines = new List<PayrollLine>(employees.Count);
        foreach (var emp in employees)
        {
            PayrollLineComputation calc = FinanceCalculator.ComputePayrollLine(emp, adjustments);
            var line = new PayrollLine(
                GuidV7.NewGuid(),
                clinicId,
                run.Id,
                emp.Id,
                calc.EmployeeName,
                calc.DoctorId,
                calc.BaseSalaryAmount,
                calc.BonusTotalAmount,
                calc.DeductionTotalAmount,
                calc.AdvanceTotalAmount,
                calc.NetPayAmount,
                calc.Currency,
                run.OperationId);
            _dbContext.PayrollLines.Add(line);
            lines.Add(line);

            if (attachAdjustments)
            {
                foreach (var adj in adjustments.Where(a => a.EmployeeId == emp.Id))
                {
                    adj.AttachToPayrollRun(run.Id);
                }
            }
        }

        return lines;
    }

    internal static FixedCostResult MapFixedCost(FixedCost f) => new(
        f.Id, f.ClinicId, f.Category, f.TitleAr, f.TitleEn, f.Amount, f.Currency,
        f.Recurrence, f.PaidFromMethod, f.EffectiveFromDate, f.EffectiveToDate, f.IsActive, f.Notes);

    internal static EmployeeResult MapEmployee(Employee e) => new(
        e.Id, e.ClinicId, e.FullName, e.RoleTitle, e.DoctorId, e.Phone, e.BaseSalaryAmount,
        e.Currency, e.PaidFromMethod, e.HiredOnDate, e.IsActive);

    internal static PayrollRunResult MapPayrollRun(PayrollRun r, IReadOnlyList<PayrollLine> lines)
    {
        bool useDraftLines = r.Status == PayrollRunStatus.Draft && lines.Count > 0;
        decimal totalBase = useDraftLines ? lines.Sum(l => l.BaseSalaryAmount) : r.TotalBaseSalary;
        decimal totalBonuses = useDraftLines ? lines.Sum(l => l.BonusTotalAmount) : r.TotalBonuses;
        decimal totalDeductions = useDraftLines ? lines.Sum(l => l.DeductionTotalAmount) : r.TotalDeductions;
        decimal totalAdvances = useDraftLines ? lines.Sum(l => l.AdvanceTotalAmount) : r.TotalAdvances;
        decimal totalNet = useDraftLines ? lines.Sum(l => l.NetPayAmount) : r.TotalNetPay;

        return new PayrollRunResult(
            r.Id,
            r.ClinicId,
            r.PeriodYear,
            r.PeriodMonth,
            r.Status,
            r.PaidFromMethod,
            totalBase,
            totalBonuses,
            totalDeductions,
            totalAdvances,
            totalNet,
            r.Currency,
            r.CreatedAtUtc,
            r.FinalizedAtUtc,
            r.Notes,
            lines.Select(l => new PayrollLineResult(
                l.Id, l.PayrollRunId, l.EmployeeId, l.EmployeeNameSnapshot, l.DoctorId,
                l.BaseSalaryAmount, l.BonusTotalAmount, l.DeductionTotalAmount, l.AdvanceTotalAmount,
                l.NetPayAmount, l.Currency)).ToList());
    }
}
