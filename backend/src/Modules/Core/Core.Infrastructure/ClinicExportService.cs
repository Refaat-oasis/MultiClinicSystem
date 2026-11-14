using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Produces a tenant-isolated ZIP archive of a clinic's operational records for backup/portability without exposing secrets.
/// </summary>
public sealed class ClinicExportService : IClinicExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ClinicExportService(
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

    public async Task<Result<ClinicExportPackageResult>> ExportClinicDataAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(
            userId,
            clinicId,
            "clinic.export",
            requiredModuleKey: null,
            deviceId,
            cancellationToken);

        if (gate.IsFailure)
        {
            return Result<ClinicExportPackageResult>.Failure(gate.Error);
        }

        DateTimeOffset now = _clock.UtcNow;

        ClinicExportPackageResult package = await ClinicTenantScopeHelper.ExecuteForClinicAsync(
            _currentClinicContext,
            clinicId,
            async () =>
            {
                var clinic = await _dbContext.Clinics
                    .Where(c => c.Id == clinicId)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Slug,
                        Status = c.Status.ToString(),
                        c.TimeZone,
                        c.Currency,
                        RoundingRule = c.RoundingRule.ToString(),
                    })
                    .FirstAsync(cancellationToken);

                var patients = await _dbContext.Patients
                    .Where(p => p.ClinicId == clinicId && !p.IsVoided)
                    .OrderBy(p => p.FileNumber)
                    .Select(p => new
                    {
                        p.Id,
                        p.FileNumber,
                        p.FullName,
                        p.Phone,
                        p.Gender,
                        p.DateOfBirth,
                        p.CreatedAtUtc,
                    })
                    .ToListAsync(cancellationToken);

                var appointments = await _dbContext.Appointments
                    .Where(a => a.ClinicId == clinicId && !a.IsVoided)
                    .OrderBy(a => a.StartUtc)
                    .Select(a => new
                    {
                        a.Id,
                        a.PatientId,
                        a.DoctorId,
                        a.CategoryId,
                        a.StartUtc,
                        a.EndUtc,
                        Status = a.Status.ToString(),
                        Source = a.Source.ToString(),
                    })
                    .ToListAsync(cancellationToken);

                var visits = await _dbContext.Visits
                    .Where(v => v.ClinicId == clinicId && !v.IsVoided)
                    .OrderBy(v => v.StartedAtUtc)
                    .Select(v => new
                    {
                        v.Id,
                        v.PatientId,
                        v.DoctorId,
                        v.SpecialtyCode,
                        v.DiagnosisSummary,
                        v.StartedAtUtc,
                        v.EndedAtUtc,
                        Status = v.Status.ToString(),
                    })
                    .ToListAsync(cancellationToken);

                var charges = await _dbContext.Charges
                    .Where(c => c.ClinicId == clinicId && !c.IsVoided)
                    .OrderBy(c => c.CreatedAtUtc)
                    .Select(c => new
                    {
                        c.Id,
                        c.PatientId,
                        c.VisitId,
                        Status = c.Status.ToString(),
                        c.StandardSessionPriceAmount,
                        c.ToolsTotalChargedAmount,
                        c.GrossSubtotalAmount,
                    })
                    .ToListAsync(cancellationToken);

                var payments = await _dbContext.Payments
                    .Where(p => p.ClinicId == clinicId)
                    .OrderBy(p => p.PaidAtUtc)
                    .Select(p => new
                    {
                        p.Id,
                        p.PatientId,
                        p.ReceiptNumber,
                        Method = p.Method.ToString(),
                        p.Amount,
                        p.Currency,
                        p.PaidAtUtc,
                    })
                    .ToListAsync(cancellationToken);

                var treatmentPlans = await _dbContext.TreatmentPlans
                    .Where(tp => tp.ClinicId == clinicId && !tp.IsVoided)
                    .OrderBy(tp => tp.CreatedAtUtc)
                    .Select(tp => new
                    {
                        tp.Id,
                        tp.PatientId,
                        tp.Name,
                        Mode = tp.Mode.ToString(),
                        Status = tp.Status.ToString(),
                        tp.FixedTotalAmount,
                        tp.PaidAmount,
                    })
                    .ToListAsync(cancellationToken);

                var inventoryItems = await _dbContext.InventoryItems
                    .Where(i => i.ClinicId == clinicId && !i.IsVoided)
                    .OrderBy(i => i.Code)
                    .Select(i => new
                    {
                        i.Id,
                        i.Code,
                        i.NameAr,
                        i.NameEn,
                        i.QuantityOnHand,
                        i.UnitCostAmount,
                    })
                    .ToListAsync(cancellationToken);

                var fixedCosts = await _dbContext.FixedCosts
                    .Where(f => f.ClinicId == clinicId && !f.IsVoided)
                    .OrderBy(f => f.EffectiveFromDate)
                    .Select(f => new
                    {
                        f.Id,
                        Category = f.Category.ToString(),
                        f.TitleAr,
                        f.TitleEn,
                        f.Amount,
                        Recurrence = f.Recurrence.ToString(),
                    })
                    .ToListAsync(cancellationToken);

                var employees = await _dbContext.Employees
                    .Where(e => e.ClinicId == clinicId && !e.IsVoided)
                    .OrderBy(e => e.FullName)
                    .Select(e => new
                    {
                        e.Id,
                        e.FullName,
                        e.RoleTitle,
                        e.BaseSalaryAmount,
                        e.Currency,
                    })
                    .ToListAsync(cancellationToken);

                int totalRecords = patients.Count + appointments.Count + visits.Count +
                    charges.Count + payments.Count + treatmentPlans.Count +
                    inventoryItems.Count + fixedCosts.Count + employees.Count;

                var manifest = new
                {
                    ClinicId = clinic.Id,
                    ClinicName = clinic.Name,
                    clinic.Slug,
                    ExportedAtUtc = now,
                    TotalRecords = totalRecords,
                    ContainsSecrets = false,
                    Counts = new
                    {
                        Patients = patients.Count,
                        Appointments = appointments.Count,
                        Visits = visits.Count,
                        Charges = charges.Count,
                        Payments = payments.Count,
                        TreatmentPlans = treatmentPlans.Count,
                        InventoryItems = inventoryItems.Count,
                        FixedCosts = fixedCosts.Count,
                        Employees = employees.Count,
                    },
                };

                using var ms = new MemoryStream();
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    await WriteJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken);
                    await WriteJsonEntryAsync(archive, "clinic.json", clinic, cancellationToken);
                    await WriteJsonEntryAsync(archive, "patients.json", patients, cancellationToken);
                    await WriteJsonEntryAsync(archive, "appointments.json", appointments, cancellationToken);
                    await WriteJsonEntryAsync(archive, "visits.json", visits, cancellationToken);
                    await WriteJsonEntryAsync(archive, "charges.json", charges, cancellationToken);
                    await WriteJsonEntryAsync(archive, "payments.json", payments, cancellationToken);
                    await WriteJsonEntryAsync(archive, "treatment_plans.json", treatmentPlans, cancellationToken);
                    await WriteJsonEntryAsync(archive, "inventory_items.json", inventoryItems, cancellationToken);
                    await WriteJsonEntryAsync(archive, "fixed_costs.json", fixedCosts, cancellationToken);
                    await WriteJsonEntryAsync(archive, "employees.json", employees, cancellationToken);
                }

                string fileName = $"clinic-{clinic.Slug}-export-{now:yyyyMMddHHmmss}.zip";
                return new ClinicExportPackageResult(
                    ms.ToArray(),
                    "application/zip",
                    fileName,
                    totalRecords,
                    now.UtcDateTime);
            });

        await _auditService.LogAsync(
            "ClinicDataExported",
            "Clinic",
            clinicId.ToString(),
            null,
            JsonSerializer.Serialize(new { package.RecordCount, package.ExportedAtUtc }),
            userId,
            clinicId,
            ipAddress,
            requestId,
            cancellationToken);

        return Result<ClinicExportPackageResult>.Success(package);
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T payload,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        await stream.WriteAsync(bytes, cancellationToken);
    }
}

