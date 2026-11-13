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
                string cookieName = isAdminHost ? "__Host-Platform-Token" : "__Host-Staff-Token";
                if (context.Request.Cookies.TryGetValue(cookieName, out var cookieToken))
                {
                    context.Token = cookieToken;
                }
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            bool isAdminHost = HostRoutingMiddleware.IsAdminHost(context.Request);
            string? realm = context.Principal?.FindFirst("realm")?.Value;

            if (isAdminHost && !string.Equals(realm, "PlatformAdmin", StringComparison.Ordinal))
            {
                context.Fail("Clinic credentials are not permitted on the admin host.");
            }
            else if (!isAdminHost && string.Equals(realm, "PlatformAdmin", StringComparison.Ordinal))
            {
                context.Fail("Platform admin credentials are not permitted on the clinic host.");
            }

            return Task.CompletedTask;
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
});

// Application & Infrastructure Services
builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
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

app.Run();

public partial class Program
{
    protected Program() { }
}
