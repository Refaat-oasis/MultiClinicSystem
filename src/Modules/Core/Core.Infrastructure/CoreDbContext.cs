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
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();
    public DbSet<OnboardingProgress> OnboardingProgresses => Set<OnboardingProgress>();
    public DbSet<ClinicSlugHistory> ClinicSlugs => Set<ClinicSlugHistory>();
    public DbSet<ClinicSpecialty> ClinicSpecialties => Set<ClinicSpecialty>();
    public DbSet<ClinicAsset> ClinicAssets => Set<ClinicAsset>();

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
            b.Property(u => u.AvatarAssetId);
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
            b.Property(c => c.OnboardingStatus).IsRequired().HasMaxLength(32).HasDefaultValue("NotStarted");
            b.Property(c => c.TimeZone).IsRequired().HasMaxLength(64).HasDefaultValue("Africa/Cairo");
            b.Property(c => c.Currency).IsRequired().HasMaxLength(16).HasDefaultValue("EGP");
            b.Property(c => c.RoundingRule).IsRequired().HasMaxLength(32).HasDefaultValue("NearestPound");
            b.Property(c => c.Street).HasMaxLength(250);
            b.Property(c => c.City).HasMaxLength(120);
            b.Property(c => c.Governorate).HasMaxLength(120);
            b.Property(c => c.MainPhone).HasMaxLength(40);
            b.Property(c => c.ExtraPhonesJson).IsRequired().HasMaxLength(1000).HasDefaultValue("[]");
            b.Property(c => c.ContactEmail).HasMaxLength(256);
            b.Property(c => c.LogoAssetId);
            b.Property(c => c.HasFinancialEntries).HasDefaultValue(false);
            b.Property(c => c.RowVersion).IsRowVersion();
            b.HasIndex(c => c.Slug).IsUnique();
        });

        // Owner entity configuration
        builder.Entity<Owner>(b =>
        {
            b.ToTable("Owners", "core");
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).IsRequired().HasMaxLength(50);
            b.Property(o => o.WelcomeSeen).HasDefaultValue(false);
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

        // PlatformAdmin entity configuration (isolated from clinic Identity tables)
        builder.Entity<PlatformAdmin>(b =>
        {
            b.ToTable("PlatformAdmins", "core");
            b.HasKey(p => p.Id);
            b.Property(p => p.Email).IsRequired().HasMaxLength(256);
            b.Property(p => p.FullName).IsRequired().HasMaxLength(200);
            b.Property(p => p.PasswordHash).IsRequired().HasMaxLength(500);
            b.Property(p => p.EncryptedTotpSecret).HasMaxLength(1000);
            b.Property(p => p.RecoveryCodeHashesJson).IsRequired();
            b.Property(p => p.SecurityStamp).IsRequired().HasMaxLength(128);
            b.HasIndex(p => p.Email).IsUnique();
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
            b.HasQueryFilter(r => _currentClinic.IsPlatformAdmin || r.ClinicId == _currentClinic.ClinicId);
        });

        // PatientAccessLog entity configuration
        builder.Entity<PatientAccessLog>(b =>
        {
            b.ToTable("PatientAccessLogs", "core");
            b.HasKey(l => l.Id);
            b.Property(l => l.ActorId).IsRequired().HasMaxLength(150);
            b.Property(l => l.Reason).IsRequired().HasMaxLength(500);
        });

        // ClinicSlugs history table (unique slugs across platform for redirect preservation)
        builder.Entity<ClinicSlugHistory>(b =>
        {
            b.ToTable("ClinicSlugs", "core");
            b.HasKey(s => s.Id);
            b.Property(s => s.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(s => s.Slug).IsUnique();
            b.HasIndex(s => s.ClinicId);
        });

        // OnboardingProgress tenant table
        builder.Entity<OnboardingProgress>(b =>
        {
            b.ToTable("OnboardingProgress", "core");
            b.HasKey(p => p.Id);
            b.Property(p => p.StepKey).IsRequired().HasMaxLength(64);
            b.Property(p => p.RowVersion).IsRowVersion();
            b.HasIndex(p => new { p.ClinicId, p.StepKey }).IsUnique();
            b.HasQueryFilter(p => _currentClinic.IsPlatformAdmin || p.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicSpecialty tenant table
        builder.Entity<ClinicSpecialty>(b =>
        {
            b.ToTable("ClinicSpecialties", "core");
            b.HasKey(s => s.Id);
            b.Property(s => s.CatalogCode).IsRequired().HasMaxLength(64);
            b.Property(s => s.NameAr).IsRequired().HasMaxLength(120);
            b.Property(s => s.NameEn).IsRequired().HasMaxLength(120);
            b.Property(s => s.DefaultFormSchemaJson).IsRequired();
            b.Property(s => s.DefaultAppointmentCategoriesJson).IsRequired();
            b.Property(s => s.RowVersion).IsRowVersion();
            b.HasIndex(s => new { s.ClinicId, s.CatalogCode });
            b.HasQueryFilter(s => _currentClinic.IsPlatformAdmin || s.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicAsset tenant table
        builder.Entity<ClinicAsset>(b =>
        {
            b.ToTable("ClinicAssets", "core");
            b.HasKey(a => a.Id);
            b.Property(a => a.OwnerType).IsRequired().HasMaxLength(50);
            b.Property(a => a.FileName).IsRequired().HasMaxLength(255);
            b.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
            b.Property(a => a.FullStorageKey).IsRequired().HasMaxLength(500);
            b.Property(a => a.ThumbStorageKey).IsRequired().HasMaxLength(500);
            b.Property(a => a.RowVersion).IsRowVersion();
            b.HasIndex(a => a.ClinicId);
            b.HasQueryFilter(a => _currentClinic.IsPlatformAdmin || a.ClinicId == _currentClinic.ClinicId);
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
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.TenantSampleRecords,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.OnboardingProgress,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.OnboardingProgress,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicSpecialties,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicSpecialties,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicAssets,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicAssets
                WITH (STATE = ON);
            ');
        ";

        await Database.ExecuteSqlRawAsync(rlsScript, cancellationToken);
    }
}
