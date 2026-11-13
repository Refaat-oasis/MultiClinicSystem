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
