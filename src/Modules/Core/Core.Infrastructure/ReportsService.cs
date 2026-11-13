using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 14 Dapper-backed clinic reporting, Excel/PDF exports, scheduled report configs,
/// Super Admin cross-clinic reports, and Super Admin finance/reports inspect view.
/// </summary>
public sealed class ReportsService : IReportsService
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IPlatformAccess _platformAccess;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ReportsService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IPlatformAccess platformAccess,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _platformAccess = platformAccess ?? throw new ArgumentNullException(nameof(platformAccess));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IncomeVsExpensesReportResult>> GetIncomeVsExpensesReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.read", "reports", deviceId, cancellationToken);
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
            var report = await ReportsDapperQueries.QueryIncomeVsExpensesAsync(_dbContext, clinicId, fromDate, toDate, cancellationToken);
            return Result<IncomeVsExpensesReportResult>.Success(report);
        });
    }

    public async Task<Result<IReadOnlyList<DoctorFinancialReportRowResult>>> GetDoctorSummaryReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.read", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<DoctorFinancialReportRowResult>>.Failure(gate.Error);
        }

        if (toDate < fromDate)
        {
            return Result<IReadOnlyList<DoctorFinancialReportRowResult>>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            var rows = await ReportsDapperQueries.QueryDoctorSummaryAsync(_dbContext, clinicId, fromDate, toDate, doctorId, cancellationToken);
            return Result<IReadOnlyList<DoctorFinancialReportRowResult>>.Success(rows);
        });
    }

    public async Task<Result<IReadOnlyList<SessionCategoryReportRowResult>>> GetSessionsAndCategoriesReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.read", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<SessionCategoryReportRowResult>>.Failure(gate.Error);
        }

        if (toDate < fromDate)
        {
            return Result<IReadOnlyList<SessionCategoryReportRowResult>>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            var rows = await ReportsDapperQueries.QuerySessionsAndCategoriesAsync(_dbContext, clinicId, fromDate, toDate, doctorId, cancellationToken);
            return Result<IReadOnlyList<SessionCategoryReportRowResult>>.Success(rows);
        });
    }

    public async Task<Result<IReadOnlyList<MethodTotalReportRowResult>>> GetPaymentMethodTotalsReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.read", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<MethodTotalReportRowResult>>.Failure(gate.Error);
        }

        if (toDate < fromDate)
        {
            return Result<IReadOnlyList<MethodTotalReportRowResult>>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            var rows = await ReportsDapperQueries.QueryPaymentMethodTotalsAsync(_dbContext, clinicId, fromDate, toDate, cancellationToken);
            return Result<IReadOnlyList<MethodTotalReportRowResult>>.Success(rows);
        });
    }

    public async Task<Result<ExportedReportFileResult>> ExportReportAsync(
        Guid userId,
        Guid clinicId,
        ReportExportKind reportKind,
        ReportExportFormat format,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.export", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ExportedReportFileResult>.Failure(gate.Error);
        }

        if (toDate < fromDate)
        {
            return Result<ExportedReportFileResult>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        if (reportKind == ReportExportKind.PlatformPortfolio)
        {
            return Result<ExportedReportFileResult>.Failure(Error.Validation("UNSUPPORTED_CLINIC_EXPORT_KIND", "PlatformPortfolio export is only available on the admin site."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            Clinic? clinic = await _dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            string clinicName = clinic?.Name ?? "Clinic";

            byte[] fileBytes;
            int rowCount;

            switch (reportKind)
            {
                case ReportExportKind.IncomeVsExpenses:
                    IncomeVsExpensesReportResult incomeVsExp = await ReportsDapperQueries.QueryIncomeVsExpensesAsync(_dbContext, clinicId, fromDate, toDate, cancellationToken);
                    fileBytes = format == ReportExportFormat.Excel
                        ? ReportsExcelGenerator.GenerateIncomeVsExpensesExcel(incomeVsExp)
                        : ReportsPdfGenerator.GenerateIncomeVsExpensesPdf(clinicName, incomeVsExp);
                    rowCount = 10;
                    break;

                case ReportExportKind.DoctorSummary:
                    IReadOnlyList<DoctorFinancialReportRowResult> doctors = await ReportsDapperQueries.QueryDoctorSummaryAsync(_dbContext, clinicId, fromDate, toDate, doctorId, cancellationToken);
                    fileBytes = format == ReportExportFormat.Excel
                        ? ReportsExcelGenerator.GenerateDoctorSummaryExcel(doctors)
                        : ReportsPdfGenerator.GenerateDoctorSummaryPdf(clinicName, fromDate, toDate, doctors);
                    rowCount = doctors.Count;
                    break;

                case ReportExportKind.SessionsAndCategories:
                    IReadOnlyList<SessionCategoryReportRowResult> sessions = await ReportsDapperQueries.QuerySessionsAndCategoriesAsync(_dbContext, clinicId, fromDate, toDate, doctorId, cancellationToken);
                    fileBytes = format == ReportExportFormat.Excel
                        ? ReportsExcelGenerator.GenerateSessionsAndCategoriesExcel(sessions)
                        : ReportsPdfGenerator.GenerateSessionsAndCategoriesPdf(clinicName, fromDate, toDate, sessions);
                    rowCount = sessions.Count;
                    break;

                case ReportExportKind.MethodTotals:
                    IReadOnlyList<MethodTotalReportRowResult> methods = await ReportsDapperQueries.QueryPaymentMethodTotalsAsync(_dbContext, clinicId, fromDate, toDate, cancellationToken);
                    fileBytes = format == ReportExportFormat.Excel
                        ? ReportsExcelGenerator.GenerateMethodTotalsExcel(methods)
                        : ReportsPdfGenerator.GenerateMethodTotalsPdf(clinicName, fromDate, toDate, methods);
                    rowCount = methods.Count;
                    break;

                default:
                    return Result<ExportedReportFileResult>.Failure(Error.Validation("UNSUPPORTED_EXPORT_KIND", "Unsupported report export kind."));
            }

            string ext = format == ReportExportFormat.Excel ? "xlsx" : "pdf";
            string contentType = format == ReportExportFormat.Excel
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "application/pdf";
            string fileName = $"report-{reportKind}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.{ext}";

            DateTimeOffset now = _clock.UtcNow;
            var log = new ReportExportLog(
                GuidV7.NewGuid(),
                clinicId,
                reportKind,
                format,
                fromDate,
                toDate,
                rowCount,
                fileName,
                userId.ToString(),
                now);
            _dbContext.ReportExportLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "ReportExported",
                "ReportExportLog",
                log.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { log.Id, log.ReportKind, log.Format, log.RowCount }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ExportedReportFileResult>.Success(new ExportedReportFileResult(
                fileName, contentType, fileBytes, reportKind, format, rowCount));
        });
    }

    public async Task<Result<IReadOnlyList<ReportExportLogResult>>> ListExportLogsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.read", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ReportExportLogResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<ReportExportLog> rows = await _dbContext.ReportExportLogs
                .AsNoTracking()
                .Where(l => l.ClinicId == clinicId)
                .OrderByDescending(l => l.ExportedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<ReportExportLogResult>>.Success(rows.Select(l => new ReportExportLogResult(
                l.Id, l.ReportKind, l.Format, l.FromDate, l.ToDate, l.RowCount, l.FileName, l.ExportedAtUtc)).ToList());
        });
    }

    public async Task<Result<IReadOnlyList<ScheduledReportConfigResult>>> ListScheduledReportsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.read", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ScheduledReportConfigResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<ScheduledReportConfig> rows = await _dbContext.ScheduledReportConfigs
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided)
                .OrderBy(c => c.ReportKind)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<ScheduledReportConfigResult>>.Success(rows.Select(MapSchedule).ToList());
        });
    }

    public async Task<Result<ScheduledReportConfigResult>> UpsertScheduledReportAsync(
        Guid userId,
        Guid clinicId,
        UpsertScheduledReportInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "reports.export", "reports", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ScheduledReportConfigResult>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            DateTimeOffset now = _clock.UtcNow;
            DateTimeOffset nextRun = input.Frequency.Trim().Equals("Daily", StringComparison.OrdinalIgnoreCase)
                ? now.AddDays(1)
                : input.Frequency.Trim().Equals("Weekly", StringComparison.OrdinalIgnoreCase)
                    ? now.AddDays(7)
                    : now.AddMonths(1);

            ScheduledReportConfig? config = await _dbContext.ScheduledReportConfigs
                .FirstOrDefaultAsync(c => c.ClinicId == clinicId && c.ReportKind == input.ReportKind && c.Format == input.Format && !c.IsVoided, cancellationToken);

            if (config == null)
            {
                config = new ScheduledReportConfig(
                    GuidV7.NewGuid(), clinicId, input.ReportKind, input.Format, input.Frequency, nextRun, userId.ToString(), input.OperationId);
                config.SetEnabled(input.IsEnabled);
                _dbContext.ScheduledReportConfigs.Add(config);
            }
            else
            {
                config.SetEnabled(input.IsEnabled);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "ScheduledReportUpdated",
                "ScheduledReportConfig",
                config.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { config.Id, config.ReportKind, config.Format, config.IsEnabled }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ScheduledReportConfigResult>.Success(MapSchedule(config));
        });
    }

    public async Task<Result<PlatformCrossClinicReportResult>> GetPlatformCrossClinicReportAsync(
        Guid platformAdminId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            return Result<PlatformCrossClinicReportResult>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        PlatformCrossClinicReportResult report = await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                var data = await ReportsDapperQueries.QueryPlatformCrossClinicAsync(_dbContext, fromDate, toDate, cancellationToken);
                _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                    GuidV7.NewGuid(),
                    actorId: platformAdminId.ToString(),
                    reason: $"PlatformCrossClinicReport {fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}..{toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
                    timestampUtc: _clock.UtcNow,
                    clinicId: null,
                    details: "/api/v1/platform/reports/cross-clinic",
                    actorType: "SuperAdmin"));
                await _dbContext.SaveChangesAsync(cancellationToken);
                return data;
            },
            $"Super Admin cross-clinic financial report {fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}",
            platformAdminId.ToString(),
            cancellationToken);

        return Result<PlatformCrossClinicReportResult>.Success(report);
    }

    public async Task<Result<ClinicFinanceReportsInspectResult>> InspectClinicFinanceReportsAsync(
        Guid platformAdminId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            return Result<ClinicFinanceReportsInspectResult>.Failure(Error.Validation("DATE_RANGE_INVALID", "toDate must be >= fromDate."));
        }

        ClinicFinanceReportsInspectResult? inspect = await _platformAccess.ExecuteAsPlatformAdminAsync(
            async () =>
            {
                bool clinicExists = await _dbContext.Clinics.AsNoTracking().AnyAsync(c => c.Id == clinicId, cancellationToken);
                if (!clinicExists)
                {
                    return null;
                }

                IncomeVsExpensesReportResult incomeVsExp = await ReportsDapperQueries.QueryIncomeVsExpensesAsync(_dbContext, clinicId, fromDate, toDate, cancellationToken);

                List<FixedCost> fixedCosts = await _dbContext.FixedCosts
                    .AsNoTracking()
                    .Where(f => f.ClinicId == clinicId && !f.IsVoided)
                    .OrderBy(f => f.Category)
                    .ToListAsync(cancellationToken);

                List<Employee> employees = await _dbContext.Employees
                    .AsNoTracking()
                    .Where(e => e.ClinicId == clinicId && !e.IsVoided)
                    .OrderBy(e => e.FullName)
                    .ToListAsync(cancellationToken);

                List<PayrollRun> runs = await _dbContext.PayrollRuns
                    .AsNoTracking()
                    .Where(r => r.ClinicId == clinicId && !r.IsVoided)
                    .OrderByDescending(r => r.PeriodYear)
                    .ThenByDescending(r => r.PeriodMonth)
                    .Take(12)
                    .ToListAsync(cancellationToken);

                List<Guid> runIds = runs.Select(r => r.Id).ToList();
                List<PayrollLine> lines = runIds.Count == 0
                    ? []
                    : await _dbContext.PayrollLines
                        .AsNoTracking()
                        .Where(l => l.ClinicId == clinicId && !l.IsVoided && runIds.Contains(l.PayrollRunId))
                        .ToListAsync(cancellationToken);

                List<ReportExportLog> exports = await _dbContext.ReportExportLogs
                    .AsNoTracking()
                    .Where(l => l.ClinicId == clinicId)
                    .OrderByDescending(l => l.ExportedAtUtc)
                    .Take(20)
                    .ToListAsync(cancellationToken);

                List<ImportBatchLog> imports = await _dbContext.ImportBatchLogs
                    .AsNoTracking()
                    .Where(b => b.ClinicId == clinicId)
                    .OrderByDescending(b => b.ExecutedAtUtc)
                    .Take(20)
                    .ToListAsync(cancellationToken);

                _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                    GuidV7.NewGuid(),
                    actorId: platformAdminId.ToString(),
                    reason: $"Inspect clinic finance/reports ({fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}..{toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})",
                    timestampUtc: _clock.UtcNow,
                    clinicId: clinicId,
                    details: $"/api/v1/platform/clinics/{clinicId}/inspect/finance-reports",
                    actorType: "SuperAdmin"));
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new ClinicFinanceReportsInspectResult(
                    clinicId,
                    incomeVsExp,
                    fixedCosts.Select(FinanceExtendedService.MapFixedCost).ToList(),
                    employees.Select(FinanceExtendedService.MapEmployee).ToList(),
                    runs.Select(r => FinanceExtendedService.MapPayrollRun(r, lines.Where(l => l.PayrollRunId == r.Id).ToList())).ToList(),
                    exports.Select(l => new ReportExportLogResult(l.Id, l.ReportKind, l.Format, l.FromDate, l.ToDate, l.RowCount, l.FileName, l.ExportedAtUtc)).ToList(),
                    imports.Select(b => new ImportBatchLogResult(b.Id, b.TargetKind, b.FileName, b.TotalRows, b.AcceptedRows, b.RejectedRows, b.IsCommitted, b.ExecutedAtUtc)).ToList());
            },
            $"Inspect finance & reports for clinic {clinicId}",
            platformAdminId.ToString(),
            cancellationToken);

        return inspect == null
            ? Result<ClinicFinanceReportsInspectResult>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."))
            : Result<ClinicFinanceReportsInspectResult>.Success(inspect);
    }

    private static ScheduledReportConfigResult MapSchedule(ScheduledReportConfig c) => new(
        c.Id, c.ReportKind, c.Format, c.Frequency, c.IsEnabled, c.NextRunAtUtc, c.LastRunAtUtc);
}

