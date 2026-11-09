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

// Authentication with JWT Bearer + Cookie fallback
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
            if (string.IsNullOrEmpty(context.Token) &&
                context.Request.Cookies.TryGetValue("__Host-Staff-Token", out var cookieToken))
            {
                context.Token = cookieToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Application & Infrastructure Services
builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
builder.Services.AddScoped<IOutbox, OutboxService>();
builder.Services.AddScoped<OutboxService>();
builder.Services.AddScoped<IPasswordPolicy, PasswordPolicy>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAccountInvitations, AccountInvitations>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Configure ProblemDetails and Controllers
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

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
