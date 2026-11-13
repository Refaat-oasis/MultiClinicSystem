using System.Text;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Core.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Factory creating a configured service provider for integration tests against the real SQL database.
/// </summary>
public static class TestServiceFactory
{
    public static (IServiceProvider Provider, FakeEmailSender EmailSender) CreateServices(
        ICurrentClinic currentClinic,
        IClock? clock = null)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:VerificationSecret"] = "test-hmac-secret-32-chars-long-clinic-platform",
                ["Security:JwtSecret"] = "test-jwt-secret-signing-key-minimum-32-chars-clinic",
                ["Security:JwtIssuer"] = "ClinicPlatform",
                ["Security:JwtAudience"] = "ClinicPlatformClients"
            })
            .Build();

        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(clock ?? new DateTimeProvider());
        services.AddSingleton<IVerificationCodes>(_ => new VerificationCodes("test-hmac-secret-32-chars-long-clinic-platform"));
        var emailSender = new FakeEmailSender();
        services.AddSingleton<IEmailSender>(emailSender);
        services.AddSingleton(emailSender);

        services.AddSingleton(currentClinic);
        services.AddSingleton<TenantSessionInterceptor>();

        services.AddDbContext<CoreDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<TenantSessionInterceptor>();
            options.UseSqlServer(SqlDatabaseFixture.ConnectionString)
                   .AddInterceptors(interceptor);
        });

        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<CoreDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<IOutbox, OutboxService>();
        services.AddScoped<OutboxService>();
        services.AddScoped<IPasswordPolicy, PasswordPolicy>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAccountInvitations, AccountInvitations>();
        services.AddScoped<IAuthService, AuthService>();

        if (currentClinic is CurrentClinicContext concreteClinicContext)
        {
            services.AddSingleton(concreteClinicContext);
        }
        else
        {
            services.AddSingleton(_ =>
            {
                var ctx = new CurrentClinicContext();
                if (currentClinic.ClinicId.HasValue)
                {
                    ctx.SetTenant(currentClinic.ClinicId.Value);
                }
                if (currentClinic.IsPlatformAdmin)
                {
                    ctx.SetPlatformAdmin();
                }
                return ctx;
            });
        }

        services.AddScoped<IPlatformAccess, PlatformAccessService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<ISpecialtyCatalog, SpecialtyCatalog>();
        services.AddSingleton<IModuleCatalogService, ModuleCatalogService>();
        services.AddSingleton<ISettingsSectionRegistry, SettingsSectionRegistry>();
        services.AddScoped<IHourlyTotpService, HourlyTotpService>();
        services.AddScoped<IPlatformAdminAuthService, PlatformAdminAuthService>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        services.AddScoped<IClinicManagementService, ClinicManagementService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IClinicAuthorizationService, ClinicAuthorizationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IOwnerPortfolio, OwnerPortfolioService>();
        services.AddScoped<IClinicStructureService, ClinicStructureService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IScheduleRealtimeNotifier, NullScheduleRealtimeNotifier>();
        services.AddScoped<IAvailabilityQueries, AvailabilityQueriesService>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IPortalBooking>(sp => (SchedulingService)sp.GetRequiredService<ISchedulingService>());
        services.AddSingleton<ISpecialtyPlugin, DentalSpecialtyPlugin>();
        services.AddSingleton<ISpecialtyPlugin, OphthalmologySpecialtyPlugin>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IBillingPortalQueries>(sp => (BillingService)sp.GetRequiredService<IBillingService>());
        services.AddScoped<ITreatmentPlanService, TreatmentPlanService>();
        services.AddScoped<ITreatmentPlanPortalQueries>(sp => (TreatmentPlanService)sp.GetRequiredService<ITreatmentPlanService>());
        services.AddScoped<IClinicalService, ClinicalService>();
        services.AddScoped<IClinicalPortalQueries>(sp => (ClinicalService)sp.GetRequiredService<IClinicalService>());

        var provider = services.BuildServiceProvider();
        return (provider, emailSender);
    }
}
