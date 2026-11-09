using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// EF Core database context for the Core module, enforcing tenant isolation, Identity stores, and module schema isolation.
/// </summary>
public sealed class CoreDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    private readonly ICurrentClinic _currentClinic;

    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<PatientAccessLog> PatientAccessLogs => Set<PatientAccessLog>();
    public DbSet<TenantSampleRecord> TenantSampleRecords => Set<TenantSampleRecord>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<UserClinic> UserClinics => Set<UserClinic>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<AppDevice> AppDevices => Set<AppDevice>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public CoreDbContext(DbContextOptions<CoreDbContext> options, ICurrentClinic currentClinic)
        : base(options)
    {
        _currentClinic = currentClinic ?? throw new ArgumentNullException(nameof(currentClinic));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("core");

        // Identity Tables Mapping
        builder.Entity<AppUser>(b =>
        {
            b.ToTable("Users", "core");
            b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            b.Property(u => u.Locale).HasMaxLength(10).HasDefaultValue("ar");
            b.Property(u => u.Theme).HasMaxLength(10).HasDefaultValue("light");
        });

        builder.Entity<AppRole>(b =>
        {
            b.ToTable("Roles", "core");
        });

        builder.Entity<IdentityUserRole<Guid>>(b =>
        {
            b.ToTable("UserRoles", "core");
        });

        builder.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable("UserClaims", "core");
        });

        builder.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable("UserLogins", "core");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(b =>
        {
            b.ToTable("RoleClaims", "core");
        });

        builder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("UserTokens", "core");
        });

        // Clinic entity configuration
        builder.Entity<Clinic>(b =>
        {
            b.ToTable("Clinics", "core");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.Slug).IsRequired().HasMaxLength(100);
            b.Property(c => c.Status).IsRequired().HasMaxLength(50).HasDefaultValue("PendingOnboarding");
            b.HasIndex(c => c.Slug).IsUnique();
        });

        // PatientAccessLog entity configuration
        builder.Entity<PatientAccessLog>(b =>
        {
            b.ToTable("PatientAccessLogs", "core");
            b.HasKey(l => l.Id);
            b.Property(l => l.ActorId).IsRequired().HasMaxLength(150);
            b.Property(l => l.Reason).IsRequired().HasMaxLength(500);
        });

        // Owner entity configuration
        builder.Entity<Owner>(b =>
        {
            b.ToTable("Owners", "core");
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).IsRequired().HasMaxLength(50);
            b.HasIndex(o => o.UserId).IsUnique();
        });

        // UserClinic entity configuration
        builder.Entity<UserClinic>(b =>
        {
            b.ToTable("UserClinics", "core");
            b.HasKey(uc => uc.Id);
            b.Property(uc => uc.Role).IsRequired().HasMaxLength(50);
            b.HasIndex(uc => new { uc.UserId, uc.ClinicId });
        });

        // VerificationCode entity configuration
        builder.Entity<VerificationCode>(b =>
        {
            b.ToTable("VerificationCodes", "core");
            b.HasKey(v => v.Id);
            b.Property(v => v.Realm).IsRequired().HasMaxLength(50);
            b.Property(v => v.Purpose).IsRequired().HasMaxLength(50);
            b.Property(v => v.CodeHash).IsRequired().HasMaxLength(128);
            b.HasIndex(v => new { v.UserId, v.Purpose });
        });

        // AppDevice entity configuration
        builder.Entity<AppDevice>(b =>
        {
            b.ToTable("AppDevices", "core");
            b.HasKey(d => d.Id);
            b.Property(d => d.DeviceId).IsRequired().HasMaxLength(128);
            b.Property(d => d.DeviceName).IsRequired().HasMaxLength(200);
            b.HasIndex(d => new { d.UserId, d.DeviceId });
        });

        // RefreshToken entity configuration
        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens", "core");
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
            b.Property(t => t.DeviceId).IsRequired().HasMaxLength(128);
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.FamilyId);
        });

        // AuditLog entity configuration
        builder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs", "core");
            b.HasKey(a => a.Id);
            b.Property(a => a.Action).IsRequired().HasMaxLength(100);
            b.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
            b.Property(a => a.EntityId).HasMaxLength(100);
        });

        // OutboxMessage entity configuration
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages", "core");
            b.HasKey(m => m.Id);
            b.Property(m => m.Type).IsRequired().HasMaxLength(100);
            b.Property(m => m.Content).IsRequired();
        });

        // TenantSampleRecord entity configuration with Global Query Filter
        builder.Entity<TenantSampleRecord>(b =>
        {
            b.ToTable("TenantSampleRecords", "core");
            b.HasKey(r => r.Id);
            b.Property(r => r.Title).IsRequired().HasMaxLength(200);

            b.Property(r => r.Amount)
                .HasConversion(
                    money => money.Amount,
                    amount => new Money(amount, Money.DefaultCurrency))
                .HasColumnName("Amount")
                .HasPrecision(18, 2)
                .IsRequired();

            // Layer 1 Tenant Isolation: EF Core Global Query Filter
            b.HasQueryFilter(r => _currentClinic.IsPlatformAdmin || (_currentClinic.ClinicId.HasValue && r.ClinicId == _currentClinic.ClinicId.Value));
        });
    }

    /// <summary>
    /// Executes the SQL script to configure SQL Server Row-Level Security (RLS) security predicates.
    /// </summary>
    public async Task ApplyRowLevelSecurityAsync(CancellationToken cancellationToken = default)
    {
        const string rlsScript = @"
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sec')
            BEGIN
                EXEC('CREATE SCHEMA sec AUTHORIZATION dbo;');
            END;

            IF EXISTS (SELECT * FROM sys.security_policies WHERE name = 'TenantSecurityPolicy')
                DROP SECURITY POLICY sec.TenantSecurityPolicy;

            IF OBJECT_ID('sec.fn_tenantSecurityPredicate', 'IF') IS NOT NULL
                DROP FUNCTION sec.fn_tenantSecurityPredicate;

            EXEC('
                CREATE FUNCTION sec.fn_tenantSecurityPredicate(@ClinicId UNIQUEIDENTIFIER)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS fn_securitypredicate_result
                WHERE @ClinicId = CAST(SESSION_CONTEXT(N''ClinicId'') AS UNIQUEIDENTIFIER)
                   OR CAST(SESSION_CONTEXT(N''IsPlatformAdmin'') AS INT) = 1;
            ');

            EXEC('
                CREATE SECURITY POLICY sec.TenantSecurityPolicy
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.TenantSampleRecords,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.TenantSampleRecords
                WITH (STATE = ON);
            ');
        ";

        await Database.ExecuteSqlRawAsync(rlsScript, cancellationToken);
    }
}
