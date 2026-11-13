using System.Security.Claims;
using System.Text;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Core.Application;
using Core.Contracts;
using Core.Domain;
using Core.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Register Core Domain Services
builder.Services.AddSingleton<IClock, DateTimeProvider>();
builder.Services.AddSingleton<IVerificationCodes>(_ =>
{
    var secret = builder.Configuration["Security:VerificationSecret"]
                 ?? "dev-fallback-hmac-secret-key-32-chars-clinic-platform";
    return new VerificationCodes(secret);
});

// Tenancy & Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=localhost;Database=ClinicPlatform;Integrated Security=True;TrustServerCertificate=True;";

builder.Services.AddScoped<CurrentClinicContext>();
builder.Services.AddScoped<ICurrentClinic>(sp => sp.GetRequiredService<CurrentClinicContext>());
builder.Services.AddScoped<IPlatformAccess, PlatformAccessService>();
builder.Services.AddScoped<TenantSessionInterceptor>();

builder.Services.AddDbContext<CoreDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<TenantSessionInterceptor>();
    options.UseSqlServer(connectionString)
           .AddInterceptors(interceptor);
});

// Identity Setup
builder.Services.AddDataProtection();
builder.Services.AddIdentityCore<AppUser>(options =>
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

// Authentication with JWT Bearer + Host-isolated Cookie fallback + Realm enforcement
var jwtSecret = builder.Configuration["Security:JwtSecret"]
                ?? "clinic-platform-ultra-secure-jwt-secret-signing-key-minimum-32-chars";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Token))
            {
                bool isAdminHost = HostRoutingMiddleware.IsAdminHost(context.Request);
                if (isAdminHost)
                {
                    if (context.Request.Cookies.TryGetValue("__Host-Platform-Token", out var adminToken))
                    {
                        context.Token = adminToken;
                    }
                }
                else if (context.Request.Path.StartsWithSegments("/api/v1/c", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Request.Cookies.TryGetValue("__Host-Portal-Token", out var portalToken))
                    {
                        context.Token = portalToken;
                    }
                    else if (context.Request.Cookies.TryGetValue("__Host-Staff-Token", out var staffFallbackToken))
                    {
                        context.Token = staffFallbackToken;
                    }
                }
                else if (context.Request.Cookies.TryGetValue("__Host-Staff-Token", out var staffToken))
                {
                    context.Token = staffToken;
                }
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            bool isAdminHost = HostRoutingMiddleware.IsAdminHost(context.Request);
            string? realm = context.Principal?.FindFirst("realm")?.Value;

            if (isAdminHost && !string.Equals(realm, "PlatformAdmin", StringComparison.Ordinal))
            {
                context.Fail("Clinic credentials are not permitted on the admin host.");
                return;
            }
            else if (!isAdminHost && string.Equals(realm, "PlatformAdmin", StringComparison.Ordinal))
            {
                context.Fail("Platform admin credentials are not permitted on the clinic host.");
                return;
            }

            if (string.Equals(realm, "PatientPortal", StringComparison.Ordinal))
            {
                if (!context.HttpContext.Request.Path.StartsWithSegments("/api/v1/c", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("Patient portal credentials are only permitted on portal routes.");
                    return;
                }

                string? sub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                string? patientClaim = context.Principal?.FindFirst("patient_id")?.Value;
                string? clinicClaim = context.Principal?.FindFirst("clinic_id")?.Value;
                string? tokenStamp = context.Principal?.FindFirst("securityStamp")?.Value;

                if (!Guid.TryParse(sub, out Guid accountId) ||
                    !Guid.TryParse(patientClaim, out Guid patientId) ||
                    !Guid.TryParse(clinicClaim, out Guid clinicGuid) ||
                    string.IsNullOrEmpty(tokenStamp))
                {
                    context.Fail("Invalid patient portal token claims.");
                    return;
                }

                var clinicContext = context.HttpContext.RequestServices.GetRequiredService<CurrentClinicContext>();
                clinicContext.SetTenant(clinicGuid);

                var db = context.HttpContext.RequestServices.GetRequiredService<CoreDbContext>();
                var account = await db.PatientAccounts
                    .AsNoTracking()
                    .Where(a => a.ClinicId == clinicGuid && a.Id == accountId && a.PatientId == patientId && !a.IsVoided)
                    .Select(a => new { a.Status, a.SecurityStamp })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (account == null ||
                    account.Status != Core.Domain.PatientAccountStatus.Active ||
                    !string.Equals(account.SecurityStamp, tokenStamp, StringComparison.Ordinal))
                {
                    clinicContext.Clear();
                    context.Fail("Patient portal session is invalidated.");
                    return;
                }
            }
            else if (!string.Equals(realm, "PlatformAdmin", StringComparison.Ordinal))
            {
                string? sub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                string? tokenStamp = context.Principal?.FindFirst("securityStamp")?.Value;
                if (Guid.TryParse(sub, out Guid uid) && !string.IsNullOrEmpty(tokenStamp))
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<CoreDbContext>();
                    var userStamp = await db.Users
                        .Where(u => u.Id == uid)
                        .Select(u => u.SecurityStamp)
                        .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                    if (userStamp == null || !string.Equals(userStamp, tokenStamp, StringComparison.Ordinal))
                    {
                        context.Fail("Session invalidated due to security stamp rotation.");
                        return;
                    }
                }

                string? clinicClaim = context.Principal?.FindFirst("clinic_id")?.Value;
                if (Guid.TryParse(clinicClaim, out Guid clinicGuid))
                {
                    var clinicContext = context.HttpContext.RequestServices.GetRequiredService<CurrentClinicContext>();
                    clinicContext.SetTenant(clinicGuid);
                }
            }
        }
    };
});

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IClock>((options, clock) =>
    {
        options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            DateTime now = clock.UtcNow.UtcDateTime;
            return (!notBefore.HasValue || now >= notBefore.Value) &&
                   (!expires.HasValue || now < expires.Value);
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("realm", "PlatformAdmin")
              .RequireClaim("totp_enrolled", "true"));

    options.AddPolicy("PlatformAdminEnrollment", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("realm", "PlatformAdmin"));

    options.AddPolicy("profile.self", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("portal.patient", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("realm", "PatientPortal"));
});

// Application & Infrastructure Services
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
builder.Services.AddSingleton<ISpecialtyCatalog, SpecialtyCatalog>();
builder.Services.AddSingleton<IModuleCatalogService, ModuleCatalogService>();
builder.Services.AddSingleton<ISettingsSectionRegistry, SettingsSectionRegistry>();
builder.Services.AddScoped<IOutbox, OutboxService>();
builder.Services.AddScoped<OutboxService>();
builder.Services.AddScoped<IPasswordPolicy, PasswordPolicy>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAccountInvitations, AccountInvitations>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IHourlyTotpService, HourlyTotpService>();
builder.Services.AddScoped<IPlatformAdminAuthService, PlatformAdminAuthService>();
builder.Services.AddScoped<IPlatformAdminService, PlatformAdminService>();
builder.Services.AddScoped<IClinicManagementService, ClinicManagementService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IClinicAuthorizationService, ClinicAuthorizationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IOwnerPortfolio, OwnerPortfolioService>();
builder.Services.AddScoped<IClinicStructureService, ClinicStructureService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IPatientPortalQueries>(sp => (PatientService)sp.GetRequiredService<IPatientService>());
builder.Services.AddScoped<IAvailabilityQueries, AvailabilityQueriesService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<IPortalBooking>(sp => (SchedulingService)sp.GetRequiredService<ISchedulingService>());
builder.Services.AddScoped<ISchedulingPortalQueries>(sp => (SchedulingService)sp.GetRequiredService<ISchedulingService>());
builder.Services.AddSingleton<ISpecialtyPlugin, DentalSpecialtyPlugin>();
builder.Services.AddSingleton<ISpecialtyPlugin, OphthalmologySpecialtyPlugin>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IBillingPortalQueries>(sp => (BillingService)sp.GetRequiredService<IBillingService>());
builder.Services.AddScoped<ITreatmentPlanService, TreatmentPlanService>();
builder.Services.AddScoped<ITreatmentPlanPortalQueries>(sp => (TreatmentPlanService)sp.GetRequiredService<ITreatmentPlanService>());
builder.Services.AddScoped<IClinicalService, ClinicalService>();
builder.Services.AddScoped<IClinicalPortalQueries>(sp => (ClinicalService)sp.GetRequiredService<IClinicalService>());
builder.Services.AddScoped<IPatientPortalService, PatientPortalService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ITimeEngineService, TimeEngineService>();
builder.Services.AddScoped<ITimeEngineDurationProvider>(sp => (TimeEngineService)sp.GetRequiredService<ITimeEngineService>());
builder.Services.AddScoped<IFinanceExtendedService, FinanceExtendedService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IScheduleRealtimeNotifier, Platform.Api.Hubs.SignalRScheduleRealtimeNotifier>();
builder.Services.AddSignalR();

// Configure ProblemDetails and Controllers
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

// Support server-side break-glass CLI command: platform-admin reset-2fa --email <email> --secret <secret>
if (args.Length >= 2 &&
    string.Equals(args[0], "platform-admin", StringComparison.OrdinalIgnoreCase) &&
    string.Equals(args[1], "reset-2fa", StringComparison.OrdinalIgnoreCase))
{
    string? cliEmail = null;
    string? cliSecret = null;
    for (int i = 2; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--email", StringComparison.OrdinalIgnoreCase))
        {
            cliEmail = args[i + 1];
        }
        else if (string.Equals(args[i], "--secret", StringComparison.OrdinalIgnoreCase))
        {
            cliSecret = args[i + 1];
        }
    }

    using var scope = app.Services.CreateScope();
    var auth = scope.ServiceProvider.GetRequiredService<IPlatformAdminAuthService>();
    var res = await auth.ResetTwoFactorBreakGlassAsync(cliEmail ?? string.Empty, cliSecret ?? string.Empty, "localhost-cli", null);
    Console.WriteLine(res.IsSuccess ? "2FA reset complete." : $"2FA reset failed: {res.Error.Code} - {res.Error.Message}");
    Environment.ExitCode = res.IsSuccess ? 0 : 1;
    return;
}

// Pipeline Middlewares
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<HostRoutingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Platform.Api.Hubs.ClinicScheduleHub>("/hubs/schedule");

app.Run();

public partial class Program
{
    protected Program() { }
}
