using System.Text.Json;
using Core.Application;
using Core.Domain;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Heavy SQL reporting queries powered by Dapper, executing with SQL Server RLS SESSION_CONTEXT enforced.
/// </summary>
internal static class ReportsDapperQueries
{
    public static async Task<IncomeVsExpensesReportResult> QueryIncomeVsExpensesAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        Clinic? clinic = await dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);

        (DateTimeOffset startUtc, DateTimeOffset endUtc) = ToUtcRange(clinic?.TimeZoneId, fromDate, toDate);
        bool billingEnabled = await IsModuleEnabledAsync(dbContext, clinicId, "billing", cancellationToken);
        bool financeEnabled = await IsModuleEnabledAsync(dbContext, clinicId, "finance", cancellationToken);

        var sqlTotals = await ClinicTenantScopeHelper.WithDapperConnectionAsync(
            dbContext,
            clinicId,
            isPlatformAdmin: false,
            async (conn, tx) =>
            {
                const string sql = """
                    SELECT
                        COALESCE((SELECT SUM(-SignedAmount) FROM billing.LedgerEntries WHERE ClinicId = @ClinicId AND EntryType = 'Payment' AND OccurredAtUtc >= @StartUtc AND OccurredAtUtc <= @EndUtc), 0) AS GrossPayments,
                        COALESCE((SELECT SUM(SignedAmount) FROM billing.LedgerEntries WHERE ClinicId = @ClinicId AND EntryType = 'Refund' AND OccurredAtUtc >= @StartUtc AND OccurredAtUtc <= @EndUtc), 0) AS Refunds,
                        COALESCE((SELECT SUM(Amount) FROM finance.Expenses WHERE ClinicId = @ClinicId AND IsVoided = 0 AND CountsAsCashOut = 1 AND OccurredAtUtc >= @StartUtc AND OccurredAtUtc <= @EndUtc), 0) AS OperatingExpenses,
                        COALESCE((SELECT SUM(Amount) FROM finance.Expenses WHERE ClinicId = @ClinicId AND IsVoided = 0 AND CountsAsCashOut = 0 AND OccurredAtUtc >= @StartUtc AND OccurredAtUtc <= @EndUtc), 0) AS SupplierAccruals,
                        COALESCE((SELECT SUM(Amount) FROM finance.SupplierPayments WHERE ClinicId = @ClinicId AND PaidAtUtc >= @StartUtc AND PaidAtUtc <= @EndUtc), 0) AS SupplierPayments;
                    """;
                return await conn.QuerySingleAsync<IncomeExpenseDapperRow>(
                    new CommandDefinition(sql, new { ClinicId = clinicId, StartUtc = startUtc, EndUtc = endUtc }, transaction: tx, cancellationToken: cancellationToken));
            });

        decimal fixedTotal = 0m;
        decimal payrollTotal = 0m;
        if (financeEnabled)
        {
            List<FixedCost> fixedDefs = await dbContext.FixedCosts
                .AsNoTracking()
                .Where(fc => fc.ClinicId == clinicId && !fc.IsVoided && fc.IsActive)
                .ToListAsync(cancellationToken);
            fixedTotal = FinanceCalculator.ExpandFixedCosts(fixedDefs, fromDate, toDate).Sum(o => o.Amount);

            int startYm = (fromDate.Year * 12) + fromDate.Month;
            int endYm = (toDate.Year * 12) + toDate.Month;
            List<PayrollRun> finalizedRuns = await dbContext.PayrollRuns
                .AsNoTracking()
                .Where(pr => pr.ClinicId == clinicId && !pr.IsVoided && pr.Status == PayrollRunStatus.Finalized)
                .ToListAsync(cancellationToken);
            payrollTotal = finalizedRuns
                .Where(pr => ((pr.PeriodYear * 12) + pr.PeriodMonth) >= startYm && ((pr.PeriodYear * 12) + pr.PeriodMonth) <= endYm)
                .Sum(pr => pr.TotalNetPay);
        }

        var calc = FinanceCalculator.ComputeIncomeVsExpenses(
            billingEnabled ? sqlTotals.GrossPayments : 0m,
            billingEnabled ? sqlTotals.Refunds : 0m,
            financeEnabled ? sqlTotals.OperatingExpenses : 0m,
            financeEnabled ? sqlTotals.SupplierPayments : 0m,
            financeEnabled ? sqlTotals.SupplierAccruals : 0m,
            fixedTotal,
            payrollTotal);

        return new IncomeVsExpensesReportResult(
            clinicId,
            fromDate,
            toDate,
            clinic?.Currency ?? Money.DefaultCurrency,
            calc.GrossPayments,
            calc.Refunds,
            calc.NetIncome,
            calc.OperatingExpenses,
            calc.SupplierPayments,
            calc.SupplierAccruals,
            calc.FixedCostsTotal,
            calc.PayrollTotal,
            calc.TotalExpenses,
            calc.NetProfit);
    }

    public static async Task<IReadOnlyList<DoctorFinancialReportRowResult>> QueryDoctorSummaryAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        CancellationToken cancellationToken)
    {
        Clinic? clinic = await dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        (DateTimeOffset startUtc, DateTimeOffset endUtc) = ToUtcRange(clinic?.TimeZoneId, fromDate, toDate);
        int startYm = (fromDate.Year * 12) + fromDate.Month;
        int endYm = (toDate.Year * 12) + toDate.Month;

        var rows = await ClinicTenantScopeHelper.WithDapperConnectionAsync(
            dbContext,
            clinicId,
            isPlatformAdmin: false,
            async (conn, tx) =>
            {
                const string sql = """
                    SELECT
                        d.Id AS DoctorId,
                        d.FullName AS DoctorName,
                        d.SpecialtyCodesJson AS SpecialtyCodesJson,
                        COALESCE((SELECT COUNT(1) FROM clinical.Visits v WHERE v.ClinicId = @ClinicId AND v.IsVoided = 0 AND v.DoctorId = d.Id AND v.StartedAtUtc >= @StartUtc AND v.StartedAtUtc <= @EndUtc), 0) AS SessionsCount,
                        COALESCE((SELECT COUNT(1) FROM clinical.Visits v WHERE v.ClinicId = @ClinicId AND v.IsVoided = 0 AND v.Status = 'Completed' AND v.DoctorId = d.Id AND v.StartedAtUtc >= @StartUtc AND v.StartedAtUtc <= @EndUtc), 0) AS CompletedSessionsCount,
                        COALESCE((SELECT SUM(c.NetTotalAmount) FROM billing.Charges c WHERE c.ClinicId = @ClinicId AND c.IsVoided = 0 AND c.DoctorId = d.Id AND c.CreatedAtUtc >= @StartUtc AND c.CreatedAtUtc <= @EndUtc), 0) AS BilledTotal,
                        COALESCE((SELECT SUM(c.PaidAmount) FROM billing.Charges c WHERE c.ClinicId = @ClinicId AND c.IsVoided = 0 AND c.DoctorId = d.Id AND c.CreatedAtUtc >= @StartUtc AND c.CreatedAtUtc <= @EndUtc), 0) AS CollectedTotal,
                        COALESCE((SELECT SUM(c.RefundedAmount) FROM billing.Charges c WHERE c.ClinicId = @ClinicId AND c.IsVoided = 0 AND c.DoctorId = d.Id AND c.CreatedAtUtc >= @StartUtc AND c.CreatedAtUtc <= @EndUtc), 0) AS RefundedTotal,
                        COALESCE((SELECT SUM(c.ToolsTotalCostAmount) FROM billing.Charges c WHERE c.ClinicId = @ClinicId AND c.IsVoided = 0 AND c.DoctorId = d.Id AND c.CreatedAtUtc >= @StartUtc AND c.CreatedAtUtc <= @EndUtc), 0) AS ToolsCost,
                        COALESCE((SELECT SUM(l.LabCostAmount) FROM billing.LabWorkOrders l WHERE l.ClinicId = @ClinicId AND l.IsVoided = 0 AND l.DoctorId = d.Id AND l.CreatedAtUtc >= @StartUtc AND l.CreatedAtUtc <= @EndUtc), 0) AS LabCost,
                        COALESCE((SELECT SUM(pl.NetPayAmount)
                                  FROM finance.PayrollLines pl
                                  INNER JOIN finance.PayrollRuns pr ON pr.Id = pl.PayrollRunId AND pr.ClinicId = pl.ClinicId
                                  WHERE pl.ClinicId = @ClinicId
                                    AND pl.IsVoided = 0
                                    AND pr.IsVoided = 0
                                    AND pr.Status = 'Finalized'
                                    AND pl.DoctorId = d.Id
                                    AND ((pr.PeriodYear * 12) + pr.PeriodMonth) BETWEEN @StartYm AND @EndYm), 0) AS PayrollCost
                    FROM core.ClinicDoctors d
                    WHERE d.ClinicId = @ClinicId
                      AND d.IsVoided = 0
                      AND (@DoctorId IS NULL OR d.Id = @DoctorId)
                    ORDER BY d.FullName;
                    """;

                return (await conn.QueryAsync<DoctorSummaryDapperRow>(
                    new CommandDefinition(
                        sql,
                        new { ClinicId = clinicId, DoctorId = doctorId, StartUtc = startUtc, EndUtc = endUtc, StartYm = startYm, EndYm = endYm },
                        transaction: tx,
                        cancellationToken: cancellationToken))).ToList();
            });

        return rows.Select(r =>
        {
            string spec = ExtractFirstSpecialty(r.SpecialtyCodesJson);
            decimal collectedNet = RoundMoney(r.CollectedTotal - r.RefundedTotal);
            decimal netContribution = RoundMoney(collectedNet - r.ToolsCost - r.LabCost - r.PayrollCost);
            return new DoctorFinancialReportRowResult(
                r.DoctorId,
                r.DoctorName,
                spec,
                r.SessionsCount,
                r.CompletedSessionsCount,
                RoundMoney(r.BilledTotal),
                RoundMoney(r.CollectedTotal),
                RoundMoney(r.RefundedTotal),
                RoundMoney(r.ToolsCost),
                RoundMoney(r.LabCost),
                RoundMoney(r.PayrollCost),
                netContribution);
        }).ToList();
    }

    public static async Task<IReadOnlyList<SessionCategoryReportRowResult>> QuerySessionsAndCategoriesAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        CancellationToken cancellationToken)
    {
        Clinic? clinic = await dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        (DateTimeOffset startUtc, DateTimeOffset endUtc) = ToUtcRange(clinic?.TimeZoneId, fromDate, toDate);

        var rows = await ClinicTenantScopeHelper.WithDapperConnectionAsync(
            dbContext,
            clinicId,
            isPlatformAdmin: false,
            async (conn, tx) =>
            {
                const string sql = """
                    SELECT
                        CAST(v.StartedAtUtc AS date) AS VisitDateDt,
                        v.CategoryId AS CategoryId,
                        COALESCE(ac.NameAr, N'عام') AS CategoryNameAr,
                        COALESCE(ac.NameEn, N'General') AS CategoryNameEn,
                        COUNT(1) AS SessionsCount,
                        SUM(CASE WHEN v.Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedSessionsCount,
                        COALESCE(SUM(c.NetTotalAmount), 0) AS BilledTotal
                    FROM clinical.Visits v
                    LEFT JOIN scheduling.AppointmentCategories ac
                        ON ac.Id = v.CategoryId AND ac.ClinicId = v.ClinicId
                    LEFT JOIN billing.Charges c
                        ON c.VisitId = v.Id AND c.ClinicId = v.ClinicId AND c.IsVoided = 0
                    WHERE v.ClinicId = @ClinicId
                      AND v.IsVoided = 0
                      AND v.StartedAtUtc >= @StartUtc
                      AND v.StartedAtUtc <= @EndUtc
                      AND (@DoctorId IS NULL OR v.DoctorId = @DoctorId)
                    GROUP BY CAST(v.StartedAtUtc AS date), v.CategoryId, COALESCE(ac.NameAr, N'عام'), COALESCE(ac.NameEn, N'General')
                    ORDER BY CAST(v.StartedAtUtc AS date), CategoryNameEn;
                    """;

                return (await conn.QueryAsync<SessionCategoryDapperRow>(
                    new CommandDefinition(sql, new { ClinicId = clinicId, StartUtc = startUtc, EndUtc = endUtc, DoctorId = doctorId }, transaction: tx, cancellationToken: cancellationToken))).ToList();
            });

        return rows.Select(r => new SessionCategoryReportRowResult(
            DateOnly.FromDateTime(r.VisitDateDt),
            r.CategoryId,
            r.CategoryNameAr,
            r.CategoryNameEn,
            r.SessionsCount,
            r.CompletedSessionsCount,
            RoundMoney(r.BilledTotal))).ToList();
    }

    public static async Task<IReadOnlyList<MethodTotalReportRowResult>> QueryPaymentMethodTotalsAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        Clinic? clinic = await dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        (DateTimeOffset startUtc, DateTimeOffset endUtc) = ToUtcRange(clinic?.TimeZoneId, fromDate, toDate);

        var dict = new Dictionary<PaymentMethod, (decimal In, decimal Ref, decimal Exp, decimal Sup)>();
        foreach (PaymentMethod m in Enum.GetValues<PaymentMethod>())
        {
            dict[m] = (0m, 0m, 0m, 0m);
        }

        await ClinicTenantScopeHelper.WithDapperConnectionAsync(
            dbContext,
            clinicId,
            isPlatformAdmin: false,
            async (conn, tx) =>
            {
                const string sql = """
                    SELECT Method AS MethodName, 'In' AS Bucket, SUM(Amount) AS TotalAmount
                    FROM billing.Payments
                    WHERE ClinicId = @ClinicId AND PaidAtUtc >= @StartUtc AND PaidAtUtc <= @EndUtc
                    GROUP BY Method
                    UNION ALL
                    SELECT Method AS MethodName, 'Ref' AS Bucket, SUM(Amount) AS TotalAmount
                    FROM billing.Refunds
                    WHERE ClinicId = @ClinicId AND RefundedAtUtc >= @StartUtc AND RefundedAtUtc <= @EndUtc
                    GROUP BY Method
                    UNION ALL
                    SELECT Method AS MethodName, 'Exp' AS Bucket, SUM(Amount) AS TotalAmount
                    FROM finance.Expenses
                    WHERE ClinicId = @ClinicId AND IsVoided = 0 AND CountsAsCashOut = 1 AND Method IS NOT NULL AND OccurredAtUtc >= @StartUtc AND OccurredAtUtc <= @EndUtc
                    GROUP BY Method
                    UNION ALL
                    SELECT Method AS MethodName, 'Sup' AS Bucket, SUM(Amount) AS TotalAmount
                    FROM finance.SupplierPayments
                    WHERE ClinicId = @ClinicId AND PaidAtUtc >= @StartUtc AND PaidAtUtc <= @EndUtc
                    GROUP BY Method;
                    """;

                var rows = await conn.QueryAsync<MethodBucketDapperRow>(
                    new CommandDefinition(sql, new { ClinicId = clinicId, StartUtc = startUtc, EndUtc = endUtc }, transaction: tx, cancellationToken: cancellationToken));

                foreach (var row in rows)
                {
                    if (!Enum.TryParse<PaymentMethod>(row.MethodName, true, out PaymentMethod method))
                    {
                        continue;
                    }

                    var cur = dict[method];
                    dict[method] = row.Bucket switch
                    {
                        "In" => (cur.In + row.TotalAmount, cur.Ref, cur.Exp, cur.Sup),
                        "Ref" => (cur.In, cur.Ref + row.TotalAmount, cur.Exp, cur.Sup),
                        "Exp" => (cur.In, cur.Ref, cur.Exp + row.TotalAmount, cur.Sup),
                        "Sup" => (cur.In, cur.Ref, cur.Exp, cur.Sup + row.TotalAmount),
                        _ => cur
                    };
                }

                return 0;
            });

        return Enum.GetValues<PaymentMethod>().Select(m =>
        {
            var t = dict[m];
            decimal net = RoundMoney(t.In - t.Ref - t.Exp - t.Sup);
            return new MethodTotalReportRowResult(
                m,
                RoundMoney(t.In),
                RoundMoney(t.Ref),
                RoundMoney(t.Exp),
                RoundMoney(t.Sup),
                net);
        }).ToList();
    }

    public static async Task<PlatformCrossClinicReportResult> QueryPlatformCrossClinicAsync(
        CoreDbContext dbContext,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startUtc = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        DateTimeOffset endUtc = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        int startYm = (fromDate.Year * 12) + fromDate.Month;
        int endYm = (toDate.Year * 12) + toDate.Month;

        var dapperClinics = await ClinicTenantScopeHelper.WithDapperConnectionAsync(
            dbContext,
            clinicId: null,
            isPlatformAdmin: true,
            async (conn, tx) =>
            {
                const string sql = """
                    SELECT
                        c.Id AS ClinicId,
                        c.Name AS ClinicName,
                        COALESCE((SELECT TOP 1 cs.Slug FROM core.ClinicSlugs cs WHERE cs.ClinicId = c.Id ORDER BY cs.IsPrimary DESC), '') AS Slug,
                        c.Currency AS Currency,
                        COALESCE((SELECT COUNT(1) FROM clinical.Visits v WHERE v.ClinicId = c.Id AND v.IsVoided = 0 AND v.StartedAtUtc >= @StartUtc AND v.StartedAtUtc <= @EndUtc), 0) AS VisitsCount,
                        COALESCE((SELECT SUM(-SignedAmount) FROM billing.LedgerEntries l WHERE l.ClinicId = c.Id AND l.EntryType = 'Payment' AND l.OccurredAtUtc >= @StartUtc AND l.OccurredAtUtc <= @EndUtc), 0)
                            - COALESCE((SELECT SUM(SignedAmount) FROM billing.LedgerEntries l WHERE l.ClinicId = c.Id AND l.EntryType = 'Refund' AND l.OccurredAtUtc >= @StartUtc AND l.OccurredAtUtc <= @EndUtc), 0) AS NetIncome,
                        COALESCE((SELECT SUM(e.Amount) FROM finance.Expenses e WHERE e.ClinicId = c.Id AND e.IsVoided = 0 AND e.CountsAsCashOut = 1 AND e.OccurredAtUtc >= @StartUtc AND e.OccurredAtUtc <= @EndUtc), 0) AS OperatingExpenses,
                        COALESCE((SELECT SUM(sp.Amount) FROM finance.SupplierPayments sp WHERE sp.ClinicId = c.Id AND sp.PaidAtUtc >= @StartUtc AND sp.PaidAtUtc <= @EndUtc), 0) AS SupplierPayments,
                        COALESCE((SELECT SUM(pr.TotalNetPay) FROM finance.PayrollRuns pr WHERE pr.ClinicId = c.Id AND pr.IsVoided = 0 AND pr.Status = 'Finalized' AND ((pr.PeriodYear * 12) + pr.PeriodMonth) BETWEEN @StartYm AND @EndYm), 0) AS PayrollTotal
                    FROM core.Clinics c
                    ORDER BY c.Name;
                    """;

                return (await conn.QueryAsync<PlatformClinicDapperRow>(
                    new CommandDefinition(sql, new { StartUtc = startUtc, EndUtc = endUtc, StartYm = startYm, EndYm = endYm }, transaction: tx, cancellationToken: cancellationToken))).ToList();
            });

        List<FixedCost> allFixedCosts = await dbContext.FixedCosts
            .AsNoTracking()
            .Where(fc => !fc.IsVoided && fc.IsActive)
            .ToListAsync(cancellationToken);

        var fixedByClinic = allFixedCosts
            .GroupBy(fc => fc.ClinicId)
            .ToDictionary(
                g => g.Key,
                g => FinanceCalculator.ExpandFixedCosts(g, fromDate, toDate).Sum(o => o.Amount));

        var clinicRows = dapperClinics.Select(c =>
        {
            decimal fixedTotal = RoundMoney(fixedByClinic.TryGetValue(c.ClinicId, out decimal fc) ? fc : 0m);
            decimal opEx = RoundMoney(c.OperatingExpenses);
            decimal supPay = RoundMoney(c.SupplierPayments);
            decimal payroll = RoundMoney(c.PayrollTotal);
            decimal totalExpenses = RoundMoney(opEx + supPay + fixedTotal + payroll);
            decimal netIncome = RoundMoney(c.NetIncome);
            decimal netProfit = RoundMoney(netIncome - totalExpenses);
            return new PlatformClinicReportRowResult(
                c.ClinicId,
                c.ClinicName,
                c.Slug,
                c.Currency,
                c.VisitsCount,
                netIncome,
                opEx,
                supPay,
                fixedTotal,
                payroll,
                totalExpenses,
                netProfit);
        }).ToList();

        return new PlatformCrossClinicReportResult(
            fromDate,
            toDate,
            clinicRows.Count,
            clinicRows.Sum(r => r.VisitsCount),
            RoundMoney(clinicRows.Sum(r => r.NetIncome)),
            RoundMoney(clinicRows.Sum(r => r.TotalExpenses)),
            RoundMoney(clinicRows.Sum(r => r.NetProfit)),
            clinicRows);
    }

    private static async Task<bool> IsModuleEnabledAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        string moduleKey,
        CancellationToken cancellationToken) =>
        await dbContext.ClinicModules
            .AsNoTracking()
            .AnyAsync(m => m.ClinicId == clinicId && m.ModuleKey == moduleKey && m.Enabled, cancellationToken);

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ToUtcRange(string? timeZoneId, DateOnly fromDate, DateOnly toDate)
    {
        TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(timeZoneId);
        DateTimeOffset startUtc = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), tz.BaseUtcOffset).ToUniversalTime();
        DateTimeOffset endUtc = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), tz.BaseUtcOffset).ToUniversalTime();
        return (startUtc, endUtc);
    }

    private static string ExtractFirstSpecialty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "general";
        }

        try
        {
            List<string>? items = JsonSerializer.Deserialize<List<string>>(json);
            return items is { Count: > 0 } ? items[0] : "general";
        }
        catch (JsonException)
        {
            return "general";
        }
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class IncomeExpenseDapperRow
    {
        public decimal GrossPayments { get; init; }
        public decimal Refunds { get; init; }
        public decimal OperatingExpenses { get; init; }
        public decimal SupplierAccruals { get; init; }
        public decimal SupplierPayments { get; init; }
    }

    private sealed class DoctorSummaryDapperRow
    {
        public Guid DoctorId { get; init; }
        public string DoctorName { get; init; } = string.Empty;
        public string SpecialtyCodesJson { get; init; } = "[]";
        public int SessionsCount { get; init; }
        public int CompletedSessionsCount { get; init; }
        public decimal BilledTotal { get; init; }
        public decimal CollectedTotal { get; init; }
        public decimal RefundedTotal { get; init; }
        public decimal ToolsCost { get; init; }
        public decimal LabCost { get; init; }
        public decimal PayrollCost { get; init; }
    }

    private sealed class SessionCategoryDapperRow
    {
        public DateTime VisitDateDt { get; init; }
        public Guid? CategoryId { get; init; }
        public string CategoryNameAr { get; init; } = string.Empty;
        public string CategoryNameEn { get; init; } = string.Empty;
        public int SessionsCount { get; init; }
        public int CompletedSessionsCount { get; init; }
        public decimal BilledTotal { get; init; }
    }

    private sealed class MethodBucketDapperRow
    {
        public string MethodName { get; init; } = string.Empty;
        public string Bucket { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
    }

    private sealed class PlatformClinicDapperRow
    {
        public Guid ClinicId { get; init; }
        public string ClinicName { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Currency { get; init; } = Money.DefaultCurrency;
        public int VisitsCount { get; init; }
        public decimal NetIncome { get; init; }
        public decimal OperatingExpenses { get; init; }
        public decimal SupplierPayments { get; init; }
        public decimal PayrollTotal { get; init; }
    }
}
