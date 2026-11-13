using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Builds an owner's multi-clinic portfolio strictly by executing aggregate queries
/// under each owned clinic's own tenant context so SQL Server RLS remains active throughout.
/// </summary>
public sealed class OwnerPortfolioService : IOwnerPortfolio
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;

    public OwnerPortfolioService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
    }

    public async Task<Result<OwnerPortfolioResult>> GetPortfolioAsync(
        Guid userId,
        IReadOnlyList<Guid>? filterClinicIds,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc.HasValue && toUtc.HasValue && (toUtc.Value < fromUtc.Value || (toUtc.Value - fromUtc.Value).TotalDays > 366))
        {
            return Result<OwnerPortfolioResult>.Failure(Error.Validation(
                "PORTFOLIO_DATE_RANGE_EXCEEDED",
                "Portfolio date range cannot exceed 366 days."));
        }

        var owner = await _dbContext.Owners
            .FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);

        if (owner == null)
        {
            return Result<OwnerPortfolioResult>.Failure(Error.Forbidden(
                "FORBIDDEN_OWNER_ONLY",
                "Only clinic owners can access the multi-clinic owner portfolio."));
        }

        var ownedClinics = await _dbContext.Clinics
            .Where(c => c.OwnerId == owner.Id)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (filterClinicIds != null && filterClinicIds.Count > 0)
        {
            var ownedIdSet = ownedClinics.Select(c => c.Id).ToHashSet();
            if (filterClinicIds.Any(id => !ownedIdSet.Contains(id)))
            {
                // Cross-owner/cross-clinic isolation rule: return 404, never 403
                return Result<OwnerPortfolioResult>.Failure(Error.NotFound(
                    "CLINIC_NOT_FOUND",
                    "One or more requested clinics were not found."));
            }

            var filterSet = filterClinicIds.ToHashSet();
            ownedClinics = ownedClinics.Where(c => filterSet.Contains(c.Id)).ToList();
        }

        var cards = new List<OwnerPortfolioClinicItemResult>(ownedClinics.Count);
        int totalStaff = 0;
        int totalDoctors = 0;

        foreach (var clinic in ownedClinics)
        {
            // Execute strictly under the specific clinic's tenant context (RLS active, never PlatformAdmin bypass)
            var metrics = await ExecuteForClinicAsync(clinic.Id, async () =>
            {
                int staff = await _dbContext.UserClinics.CountAsync(uc => uc.ClinicId == clinic.Id, cancellationToken);
                int rooms = await _dbContext.ClinicRooms.CountAsync(r => r.ClinicId == clinic.Id && !r.IsVoided && r.IsActive, cancellationToken);
                int doctors = await _dbContext.ClinicDoctors.CountAsync(d => d.ClinicId == clinic.Id && !d.IsVoided && d.IsActive, cancellationToken);
                int modules = await _dbContext.ClinicModules.CountAsync(m => m.ClinicId == clinic.Id && m.Enabled, cancellationToken);
                return (staff, rooms, doctors, modules);
            });

            totalStaff += metrics.staff;
            totalDoctors += metrics.doctors;

            cards.Add(new OwnerPortfolioClinicItemResult(
                ClinicId: clinic.Id,
                Name: clinic.Name,
                Slug: clinic.Slug,
                Status: clinic.Status,
                OnboardingStatus: clinic.OnboardingStatus,
                StaffCount: metrics.staff,
                RoomsCount: metrics.rooms,
                DoctorsCount: metrics.doctors,
                EnabledModulesCount: metrics.modules));
        }

        return Result<OwnerPortfolioResult>.Success(new OwnerPortfolioResult(
            OwnerId: owner.Id,
            TotalClinics: ownedClinics.Count,
            ActiveClinics: ownedClinics.Count(c => c.IsActive && string.Equals(c.Status, "Active", StringComparison.OrdinalIgnoreCase)),
            TotalStaffAcrossClinics: totalStaff,
            TotalDoctorsAcrossClinics: totalDoctors,
            Clinics: cards));
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
