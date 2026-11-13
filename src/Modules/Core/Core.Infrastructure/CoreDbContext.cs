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
    public DbSet<ClinicModule> ClinicModules => Set<ClinicModule>();
    public DbSet<ClinicRole> ClinicRoles => Set<ClinicRole>();
    public DbSet<ClinicRoleClaim> ClinicRolePermissions => Set<ClinicRoleClaim>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<UserDashboardLayout> UserDashboardLayouts => Set<UserDashboardLayout>();
    public DbSet<ClinicRoom> ClinicRooms => Set<ClinicRoom>();
    public DbSet<ClinicDoctor> ClinicDoctors => Set<ClinicDoctor>();
    public DbSet<ClinicWorkingHour> ClinicWorkingHours => Set<ClinicWorkingHour>();
    public DbSet<ClinicDayOff> ClinicDaysOff => Set<ClinicDayOff>();
    public DbSet<NumberBlock> NumberBlocks => Set<NumberBlock>();
    public DbSet<ProcessedOperation> ProcessedOperations => Set<ProcessedOperation>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<PatientNote> PatientNotes => Set<PatientNote>();
    public DbSet<PatientAttachment> PatientAttachments => Set<PatientAttachment>();
    public DbSet<AppointmentCategory> AppointmentCategories => Set<AppointmentCategory>();
    public DbSet<DoctorBookingSetting> DoctorBookingSettings => Set<DoctorBookingSetting>();
    public DbSet<ClinicSchedulingSetting> ClinicSchedulingSettings => Set<ClinicSchedulingSetting>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<WaitingListEntry> WaitingListEntries => Set<WaitingListEntry>();
    public DbSet<SlotOffer> SlotOffers => Set<SlotOffer>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitVital> VisitVitals => Set<VisitVital>();
    public DbSet<VisitDiagnosis> VisitDiagnoses => Set<VisitDiagnosis>();
    public DbSet<VisitProcedure> VisitProcedures => Set<VisitProcedure>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<ClinicalForm> ClinicalForms => Set<ClinicalForm>();
    public DbSet<VisitFormData> VisitFormData => Set<VisitFormData>();
    public DbSet<ToothRecord> ToothRecords => Set<ToothRecord>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<ChargeLine> ChargeLines => Set<ChargeLine>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<SkippedReceiptNumber> SkippedReceiptNumbers => Set<SkippedReceiptNumber>();
    public DbSet<LabWorkOrder> LabWorkOrders => Set<LabWorkOrder>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<DailyClosing> DailyClosings => Set<DailyClosing>();
    public DbSet<ClosingMethodTotal> ClosingMethodTotals => Set<ClosingMethodTotal>();
    public DbSet<TreatmentPlan> TreatmentPlans => Set<TreatmentPlan>();
    public DbSet<PlanSession> PlanSessions => Set<PlanSession>();
    public DbSet<PlanPaymentScheduleItem> PlanPaymentScheduleItems => Set<PlanPaymentScheduleItem>();
    public DbSet<PatientAccount> PatientAccounts => Set<PatientAccount>();
    public DbSet<PortalConsent> PortalConsents => Set<PortalConsent>();
    public DbSet<ClinicPortalSetting> ClinicPortalSettings => Set<ClinicPortalSetting>();
    public DbSet<ClinicPortalBookingSetting> ClinicPortalBookingSettings => Set<ClinicPortalBookingSetting>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryItemBatch> InventoryItemBatches => Set<InventoryItemBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<ClinicInventorySetting> ClinicInventorySettings => Set<ClinicInventorySetting>();
    public DbSet<InventoryAlert> InventoryAlerts => Set<InventoryAlert>();
    public DbSet<SessionDuration> SessionDurations => Set<SessionDuration>();
    public DbSet<CategoryTimeStat> CategoryTimeStats => Set<CategoryTimeStat>();
    public DbSet<DoctorTimeEngineSetting> DoctorTimeEngineSettings => Set<DoctorTimeEngineSetting>();
    public DbSet<ClinicTimeEngineSetting> ClinicTimeEngineSettings => Set<ClinicTimeEngineSetting>();
    public DbSet<FixedCost> FixedCosts => Set<FixedCost>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<ReportExportLog> ReportExportLogs => Set<ReportExportLog>();
    public DbSet<ScheduledReportConfig> ScheduledReportConfigs => Set<ScheduledReportConfig>();
    public DbSet<ImportBatchLog> ImportBatchLogs => Set<ImportBatchLog>();

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
            b.Property(c => c.ScopePatientsByDoctor).HasDefaultValue(false);
            b.Property(c => c.ScopePatientsBySpecialty).HasDefaultValue(false);
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
            b.Property(d => d.IsApproved).HasDefaultValue(true);
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
            b.Property(l => l.ActorType).IsRequired().HasMaxLength(32).HasDefaultValue("SuperAdmin");
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

        // ClinicModules tenant table
        builder.Entity<ClinicModule>(b =>
        {
            b.ToTable("ClinicModules", "core");
            b.HasKey(m => m.Id);
            b.Property(m => m.ModuleKey).IsRequired().HasMaxLength(64);
            b.Property(m => m.Note).HasMaxLength(500);
            b.Property(m => m.RowVersion).IsRowVersion();
            b.HasIndex(m => new { m.ClinicId, m.ModuleKey }).IsUnique();
            b.HasQueryFilter(m => _currentClinic.IsPlatformAdmin || m.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicRoles tenant table
        builder.Entity<ClinicRole>(b =>
        {
            b.ToTable("ClinicRoles", "core");
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).IsRequired().HasMaxLength(100);
            b.Property(r => r.NameAr).IsRequired().HasMaxLength(120);
            b.Property(r => r.NameEn).IsRequired().HasMaxLength(120);
            b.Property(r => r.TemplateKey).HasMaxLength(64);
            b.Property(r => r.RowVersion).IsRowVersion();
            b.HasIndex(r => new { r.ClinicId, r.Name });
            b.HasQueryFilter(r => _currentClinic.IsPlatformAdmin || r.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicRolePermissions tenant table
        builder.Entity<ClinicRoleClaim>(b =>
        {
            b.ToTable("ClinicRolePermissions", "core");
            b.HasKey(rp => rp.Id);
            b.Property(rp => rp.PermissionKey).IsRequired().HasMaxLength(100);
            b.Property(rp => rp.RowVersion).IsRowVersion();
            b.HasIndex(rp => new { rp.ClinicId, rp.ClinicRoleId, rp.PermissionKey }).IsUnique();
            b.HasQueryFilter(rp => _currentClinic.IsPlatformAdmin || rp.ClinicId == _currentClinic.ClinicId);
        });

        // UserPermissionOverrides tenant table
        builder.Entity<UserPermissionOverride>(b =>
        {
            b.ToTable("UserPermissionOverrides", "core");
            b.HasKey(o => o.Id);
            b.Property(o => o.PermissionKey).IsRequired().HasMaxLength(100);
            b.Property(o => o.Effect).IsRequired().HasMaxLength(16);
            b.Property(o => o.RowVersion).IsRowVersion();
            b.HasIndex(o => new { o.ClinicId, o.UserId, o.PermissionKey }).IsUnique();
            b.HasQueryFilter(o => _currentClinic.IsPlatformAdmin || o.ClinicId == _currentClinic.ClinicId);
        });

        // UserDashboardLayouts tenant table
        builder.Entity<UserDashboardLayout>(b =>
        {
            b.ToTable("UserDashboardLayouts", "core");
            b.HasKey(l => l.Id);
            b.Property(l => l.PinnedWidgetsJson).IsRequired();
            b.Property(l => l.RowVersion).IsRowVersion();
            b.HasIndex(l => new { l.ClinicId, l.UserId }).IsUnique();
            b.HasQueryFilter(l => _currentClinic.IsPlatformAdmin || l.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicRooms tenant table
        builder.Entity<ClinicRoom>(b =>
        {
            b.ToTable("ClinicRooms", "core");
            b.HasKey(r => r.Id);
            b.Property(r => r.Code).IsRequired().HasMaxLength(40);
            b.Property(r => r.NameAr).IsRequired().HasMaxLength(120);
            b.Property(r => r.NameEn).IsRequired().HasMaxLength(120);
            b.Property(r => r.SpecialtyCode).HasMaxLength(64);
            b.Property(r => r.FloorOrLocation).HasMaxLength(120);
            b.Property(r => r.RowVersion).IsRowVersion();
            b.HasIndex(r => new { r.ClinicId, r.Code });
            b.HasQueryFilter(r => _currentClinic.IsPlatformAdmin || r.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicDoctors tenant table
        builder.Entity<ClinicDoctor>(b =>
        {
            b.ToTable("ClinicDoctors", "core");
            b.HasKey(d => d.Id);
            b.Property(d => d.FullName).IsRequired().HasMaxLength(200);
            b.Property(d => d.TitleAr).IsRequired().HasMaxLength(120);
            b.Property(d => d.TitleEn).IsRequired().HasMaxLength(120);
            b.Property(d => d.SpecialtyCodesJson).IsRequired();
            b.Property(d => d.AssignedRoomIdsJson).IsRequired();
            b.Property(d => d.RowVersion).IsRowVersion();
            b.HasIndex(d => d.ClinicId);
            b.HasQueryFilter(d => _currentClinic.IsPlatformAdmin || d.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicWorkingHours tenant table
        builder.Entity<ClinicWorkingHour>(b =>
        {
            b.ToTable("ClinicWorkingHours", "core");
            b.HasKey(w => w.Id);
            b.Property(w => w.StartTime).IsRequired().HasMaxLength(16);
            b.Property(w => w.EndTime).IsRequired().HasMaxLength(16);
            b.Property(w => w.RowVersion).IsRowVersion();
            b.HasIndex(w => new { w.ClinicId, w.DoctorId, w.DayOfWeek });
            b.HasQueryFilter(w => _currentClinic.IsPlatformAdmin || w.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicDaysOff tenant table
        builder.Entity<ClinicDayOff>(b =>
        {
            b.ToTable("ClinicDaysOff", "core");
            b.HasKey(d => d.Id);
            b.Property(d => d.StartDate).IsRequired().HasMaxLength(32);
            b.Property(d => d.EndDate).IsRequired().HasMaxLength(32);
            b.Property(d => d.ReasonAr).IsRequired().HasMaxLength(200);
            b.Property(d => d.ReasonEn).IsRequired().HasMaxLength(200);
            b.Property(d => d.RowVersion).IsRowVersion();
            b.HasIndex(d => d.ClinicId);
            b.HasQueryFilter(d => _currentClinic.IsPlatformAdmin || d.ClinicId == _currentClinic.ClinicId);
        });

        // NumberBlocks tenant table
        builder.Entity<NumberBlock>(b =>
        {
            b.ToTable("NumberBlocks", "core");
            b.HasKey(n => n.Id);
            b.Property(n => n.DeviceId).IsRequired().HasMaxLength(128);
            b.Property(n => n.Kind).IsRequired().HasMaxLength(32);
            b.Property(n => n.Status).IsRequired().HasMaxLength(32);
            b.Property(n => n.RowVersion).IsRowVersion();
            b.HasIndex(n => new { n.ClinicId, n.Kind, n.FromNumber }).IsUnique();
            b.HasQueryFilter(n => _currentClinic.IsPlatformAdmin || n.ClinicId == _currentClinic.ClinicId);
        });

        // ProcessedOperations tenant table
        builder.Entity<ProcessedOperation>(b =>
        {
            b.ToTable("ProcessedOperations", "core");
            b.HasKey(o => o.Id);
            b.Property(o => o.EntityName).IsRequired().HasMaxLength(80);
            b.Property(o => o.RowVersion).IsRowVersion();
            b.HasIndex(o => new { o.ClinicId, o.OperationId }).IsUnique();
            b.HasQueryFilter(o => _currentClinic.IsPlatformAdmin || o.ClinicId == _currentClinic.ClinicId);
        });

        // Patients tenant table (patients schema)
        builder.Entity<Patient>(b =>
        {
            b.ToTable("Patients", "patients");
            b.HasKey(p => p.Id);
            b.Property(p => p.FileNumber).IsRequired().HasMaxLength(40);
            b.Property(p => p.FullName).IsRequired().HasMaxLength(200);
            b.Property(p => p.NormalizedName).IsRequired().HasMaxLength(200);
            b.Property(p => p.Phone).IsRequired().HasMaxLength(40);
            b.Property(p => p.NormalizedPhone).IsRequired().HasMaxLength(40);
            b.Property(p => p.SecondaryPhone).HasMaxLength(40);
            b.Property(p => p.Gender).IsRequired().HasMaxLength(20);
            b.Property(p => p.NationalId).HasMaxLength(40);
            b.Property(p => p.Address).HasMaxLength(400);
            b.Property(p => p.PrimarySpecialtyCode).HasMaxLength(64);
            b.Property(p => p.Email).HasMaxLength(256);
            b.Property(p => p.BloodType).HasMaxLength(16);
            b.Property(p => p.GuardianName).HasMaxLength(200);
            b.Property(p => p.GuardianEmail).HasMaxLength(256);
            b.Property(p => p.MedicalHistorySummary).HasMaxLength(2000);
            b.Property(p => p.ChronicConditionsJson).IsRequired();
            b.Property(p => p.CurrentMedicationsJson).IsRequired();
            b.Property(p => p.PastSurgeriesJson).IsRequired();
            b.Property(p => p.RowVersion).IsRowVersion();
            b.HasIndex(p => new { p.ClinicId, p.FileNumber }).IsUnique();
            b.HasIndex(p => new { p.ClinicId, p.NormalizedName });
            b.HasIndex(p => new { p.ClinicId, p.NormalizedPhone });
            b.HasQueryFilter(p => _currentClinic.IsPlatformAdmin || p.ClinicId == _currentClinic.ClinicId);
        });

        // PatientAllergies tenant table (patients schema)
        builder.Entity<PatientAllergy>(b =>
        {
            b.ToTable("PatientAllergies", "patients");
            b.HasKey(a => a.Id);
            b.Property(a => a.Allergen).IsRequired().HasMaxLength(200);
            b.Property(a => a.Severity).IsRequired().HasMaxLength(20);
            b.Property(a => a.Reaction).HasMaxLength(400);
            b.Property(a => a.RowVersion).IsRowVersion();
            b.HasIndex(a => new { a.ClinicId, a.PatientId });
            b.HasQueryFilter(a => _currentClinic.IsPlatformAdmin || a.ClinicId == _currentClinic.ClinicId);
        });

        // PatientNotes tenant table (patients schema)
        builder.Entity<PatientNote>(b =>
        {
            b.ToTable("PatientNotes", "patients");
            b.HasKey(n => n.Id);
            b.Property(n => n.NoteText).IsRequired().HasMaxLength(2000);
            b.Property(n => n.RowVersion).IsRowVersion();
            b.HasIndex(n => new { n.ClinicId, n.PatientId });
            b.HasQueryFilter(n => _currentClinic.IsPlatformAdmin || n.ClinicId == _currentClinic.ClinicId);
        });

        // PatientAttachments tenant table (patients schema)
        builder.Entity<PatientAttachment>(b =>
        {
            b.ToTable("PatientAttachments", "patients");
            b.HasKey(a => a.Id);
            b.Property(a => a.Category).IsRequired().HasMaxLength(60);
            b.Property(a => a.FileName).IsRequired().HasMaxLength(255);
            b.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
            b.Property(a => a.Sha256Hash).IsRequired().HasMaxLength(64);
            b.Property(a => a.StorageKey).IsRequired().HasMaxLength(500);
            b.Property(a => a.ThumbnailStorageKey).HasMaxLength(500);
            b.Property(a => a.Notes).HasMaxLength(500);
            b.Property(a => a.RowVersion).IsRowVersion();
            b.HasIndex(a => new { a.ClinicId, a.PatientId });
            b.HasQueryFilter(a => _currentClinic.IsPlatformAdmin || a.ClinicId == _currentClinic.ClinicId);
        });

        // AppointmentCategories tenant table (scheduling schema)
        builder.Entity<AppointmentCategory>(b =>
        {
            b.ToTable("AppointmentCategories", "scheduling");
            b.HasKey(c => c.Id);
            b.Property(c => c.SpecialtyCode).IsRequired().HasMaxLength(64);
            b.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
            b.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
            b.Property(c => c.DefaultPriceAmount).HasColumnType("decimal(18,2)");
            b.Property(c => c.DefaultPriceCurrency).IsRequired().HasMaxLength(10);
            b.Property(c => c.RowVersion).IsRowVersion();
            b.HasIndex(c => new { c.ClinicId, c.SpecialtyCode });
            b.HasQueryFilter(c => _currentClinic.IsPlatformAdmin || c.ClinicId == _currentClinic.ClinicId);
        });

        // DoctorBookingSettings tenant table (scheduling schema)
        builder.Entity<DoctorBookingSetting>(b =>
        {
            b.ToTable("DoctorBookingSettings", "scheduling");
            b.HasKey(s => s.Id);
            b.Property(s => s.RowVersion).IsRowVersion();
            b.HasIndex(s => new { s.ClinicId, s.DoctorId }).IsUnique();
            b.HasQueryFilter(s => _currentClinic.IsPlatformAdmin || s.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicSchedulingSettings tenant table (scheduling schema)
        builder.Entity<ClinicSchedulingSetting>(b =>
        {
            b.ToTable("ClinicSchedulingSettings", "scheduling");
            b.HasKey(s => s.Id);
            b.Property(s => s.RescheduleOptionsJson).IsRequired();
            b.Property(s => s.RowVersion).IsRowVersion();
            b.HasIndex(s => s.ClinicId).IsUnique();
            b.HasQueryFilter(s => _currentClinic.IsPlatformAdmin || s.ClinicId == _currentClinic.ClinicId);
        });

        // Appointments tenant table (scheduling schema)
        builder.Entity<Appointment>(b =>
        {
            b.ToTable("Appointments", "scheduling");
            b.HasKey(a => a.Id);
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(a => a.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(a => a.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(a => a.Notes).HasMaxLength(1000);
            b.Property(a => a.CancelReason).HasMaxLength(500);
            b.Property(a => a.ReviewNotes).HasMaxLength(500);
            b.Property(a => a.RowVersion).IsRowVersion();
            b.HasIndex(a => new { a.ClinicId, a.DoctorId, a.StartUtc, a.EndUtc });
            b.HasIndex(a => new { a.ClinicId, a.RoomId, a.StartUtc, a.EndUtc });
            b.HasIndex(a => new { a.ClinicId, a.PatientId, a.StartUtc });
            b.HasQueryFilter(a => _currentClinic.IsPlatformAdmin || a.ClinicId == _currentClinic.ClinicId);
        });

        // QueueEntries tenant table (queue schema)
        builder.Entity<QueueEntry>(b =>
        {
            b.ToTable("QueueEntries", "queue");
            b.HasKey(q => q.Id);
            b.Property(q => q.QueueDate).IsRequired().HasMaxLength(16);
            b.Property(q => q.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(q => q.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(q => q.RowVersion).IsRowVersion();
            b.HasIndex(q => new { q.ClinicId, q.QueueDate, q.DoctorId, q.OrderIndex });
            b.HasQueryFilter(q => _currentClinic.IsPlatformAdmin || q.ClinicId == _currentClinic.ClinicId);
        });

        // WaitingListEntries tenant table (queue schema)
        builder.Entity<WaitingListEntry>(b =>
        {
            b.ToTable("WaitingListEntries", "queue");
            b.HasKey(w => w.Id);
            b.Property(w => w.PreferredDate).IsRequired().HasMaxLength(16);
            b.Property(w => w.Notes).HasMaxLength(500);
            b.Property(w => w.RowVersion).IsRowVersion();
            b.HasIndex(w => new { w.ClinicId, w.PreferredDate, w.IsFulfilled });
            b.HasQueryFilter(w => _currentClinic.IsPlatformAdmin || w.ClinicId == _currentClinic.ClinicId);
        });

        // SlotOffers tenant table (queue schema)
        builder.Entity<SlotOffer>(b =>
        {
            b.ToTable("SlotOffers", "queue");
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(o => o.RowVersion).IsRowVersion();
            b.HasIndex(o => new { o.ClinicId, o.Status, o.OfferedAtUtc });
            b.HasQueryFilter(o => _currentClinic.IsPlatformAdmin || o.ClinicId == _currentClinic.ClinicId);
        });

        // Visits tenant table (clinical schema)
        builder.Entity<Visit>(b =>
        {
            b.ToTable("Visits", "clinical");
            b.HasKey(v => v.Id);
            b.Property(v => v.SpecialtyCode).HasMaxLength(64).IsRequired();
            b.Property(v => v.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(v => v.ChiefComplaint).HasMaxLength(1000);
            b.Property(v => v.HistoryOfPresentIllness).HasMaxLength(2000);
            b.Property(v => v.Examination).HasMaxLength(2000);
            b.Property(v => v.DiagnosisSummary).HasMaxLength(1000);
            b.Property(v => v.Advice).HasMaxLength(2000);
            b.Property(v => v.PrivateDoctorNotes).HasMaxLength(2000);
            b.Property(v => v.OvertimeExcuseReason).HasMaxLength(500);
            b.Property(v => v.RowVersion).IsRowVersion();
            b.HasIndex(v => new { v.ClinicId, v.AppointmentId });
            b.HasIndex(v => new { v.ClinicId, v.PatientId, v.StartedAtUtc });
            b.HasQueryFilter(v => _currentClinic.IsPlatformAdmin || v.ClinicId == _currentClinic.ClinicId);
        });

        // VisitVitals tenant table (clinical schema)
        builder.Entity<VisitVital>(b =>
        {
            b.ToTable("VisitVitals", "clinical");
            b.HasKey(v => v.Id);
            b.Property(v => v.TemperatureCelsius).HasPrecision(5, 2);
            b.Property(v => v.WeightKg).HasPrecision(6, 2);
            b.Property(v => v.HeightCm).HasPrecision(6, 2);
            b.Property(v => v.Bmi).HasPrecision(6, 2);
            b.Property(v => v.GlucoseMgDl).HasPrecision(7, 2);
            b.Property(v => v.RowVersion).IsRowVersion();
            b.HasIndex(v => new { v.ClinicId, v.VisitId });
            b.HasQueryFilter(v => _currentClinic.IsPlatformAdmin || v.ClinicId == _currentClinic.ClinicId);
        });

        // VisitDiagnoses tenant table (clinical schema)
        builder.Entity<VisitDiagnosis>(b =>
        {
            b.ToTable("VisitDiagnoses", "clinical");
            b.HasKey(d => d.Id);
            b.Property(d => d.Icd10Code).HasMaxLength(32).IsRequired();
            b.Property(d => d.TitleAr).HasMaxLength(300).IsRequired();
            b.Property(d => d.TitleEn).HasMaxLength(300).IsRequired();
            b.Property(d => d.Notes).HasMaxLength(1000);
            b.Property(d => d.RowVersion).IsRowVersion();
            b.HasIndex(d => new { d.ClinicId, d.VisitId });
            b.HasQueryFilter(d => _currentClinic.IsPlatformAdmin || d.ClinicId == _currentClinic.ClinicId);
        });

        // VisitProcedures tenant table (clinical schema)
        builder.Entity<VisitProcedure>(b =>
        {
            b.ToTable("VisitProcedures", "clinical");
            b.HasKey(p => p.Id);
            b.Property(p => p.ProcedureCode).HasMaxLength(64).IsRequired();
            b.Property(p => p.NameAr).HasMaxLength(300).IsRequired();
            b.Property(p => p.NameEn).HasMaxLength(300).IsRequired();
            b.Property(p => p.ToothOrSite).HasMaxLength(100);
            b.Property(p => p.Notes).HasMaxLength(1000);
            b.Property(p => p.RowVersion).IsRowVersion();
            b.HasIndex(p => new { p.ClinicId, p.VisitId });
            b.HasQueryFilter(p => _currentClinic.IsPlatformAdmin || p.ClinicId == _currentClinic.ClinicId);
        });

        // Prescriptions tenant table (clinical schema)
        builder.Entity<Prescription>(b =>
        {
            b.ToTable("Prescriptions", "clinical");
            b.HasKey(p => p.Id);
            b.Property(p => p.Notes).HasMaxLength(1000);
            b.Property(p => p.AllergyWarningDetails).HasMaxLength(1000);
            b.Property(p => p.RowVersion).IsRowVersion();
            b.HasIndex(p => new { p.ClinicId, p.VisitId });
            b.HasQueryFilter(p => _currentClinic.IsPlatformAdmin || p.ClinicId == _currentClinic.ClinicId);
        });

        // PrescriptionItems tenant table (clinical schema)
        builder.Entity<PrescriptionItem>(b =>
        {
            b.ToTable("PrescriptionItems", "clinical");
            b.HasKey(i => i.Id);
            b.Property(i => i.DrugName).HasMaxLength(200).IsRequired();
            b.Property(i => i.ActiveIngredient).HasMaxLength(200);
            b.Property(i => i.Dosage).HasMaxLength(120).IsRequired();
            b.Property(i => i.Frequency).HasMaxLength(120).IsRequired();
            b.Property(i => i.Duration).HasMaxLength(120).IsRequired();
            b.Property(i => i.Instructions).HasMaxLength(500);
            b.Property(i => i.RowVersion).IsRowVersion();
            b.HasIndex(i => new { i.ClinicId, i.PrescriptionId });
            b.HasQueryFilter(i => _currentClinic.IsPlatformAdmin || i.ClinicId == _currentClinic.ClinicId);
        });

        // LabOrders tenant table (clinical schema)
        builder.Entity<LabOrder>(b =>
        {
            b.ToTable("LabOrders", "clinical");
            b.HasKey(l => l.Id);
            b.Property(l => l.LabName).HasMaxLength(200).IsRequired();
            b.Property(l => l.TestOrWorkName).HasMaxLength(300).IsRequired();
            b.Property(l => l.ToothOrSite).HasMaxLength(100);
            b.Property(l => l.Instructions).HasMaxLength(1000);
            b.Property(l => l.Status).HasMaxLength(32).IsRequired();
            b.Property(l => l.ResultSummary).HasMaxLength(2000);
            b.Property(l => l.RowVersion).IsRowVersion();
            b.HasIndex(l => new { l.ClinicId, l.VisitId });
            b.HasQueryFilter(l => _currentClinic.IsPlatformAdmin || l.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicalForms tenant table (clinical schema)
        builder.Entity<ClinicalForm>(b =>
        {
            b.ToTable("ClinicalForms", "clinical");
            b.HasKey(f => f.Id);
            b.Property(f => f.SpecialtyCode).HasMaxLength(64).IsRequired();
            b.Property(f => f.TitleAr).HasMaxLength(200).IsRequired();
            b.Property(f => f.TitleEn).HasMaxLength(200).IsRequired();
            b.Property(f => f.JsonSchema).IsRequired();
            b.Property(f => f.RowVersion).IsRowVersion();
            b.HasIndex(f => new { f.ClinicId, f.SpecialtyCode, f.Version });
            b.HasQueryFilter(f => _currentClinic.IsPlatformAdmin || f.ClinicId == _currentClinic.ClinicId);
        });

        // VisitFormData tenant table (clinical schema)
        builder.Entity<VisitFormData>(b =>
        {
            b.ToTable("VisitFormData", "clinical");
            b.HasKey(d => d.Id);
            b.Property(d => d.SpecialtyCode).HasMaxLength(64).IsRequired();
            b.Property(d => d.AnswersJson).IsRequired();
            b.Property(d => d.RowVersion).IsRowVersion();
            b.HasIndex(d => new { d.ClinicId, d.VisitId, d.ClinicalFormId });
            b.HasQueryFilter(d => _currentClinic.IsPlatformAdmin || d.ClinicId == _currentClinic.ClinicId);
        });

        // ToothRecords tenant table (clinical schema)
        builder.Entity<ToothRecord>(b =>
        {
            b.ToTable("ToothRecords", "clinical");
            b.HasKey(t => t.Id);
            b.Property(t => t.FdiToothNumber).HasMaxLength(8).IsRequired();
            b.Property(t => t.UniversalToothCode).HasMaxLength(8).IsRequired();
            b.Property(t => t.Condition).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(t => t.SurfacesJson).HasMaxLength(128).IsRequired();
            b.Property(t => t.Notes).HasMaxLength(500);
            b.Property(t => t.RowVersion).IsRowVersion();
            b.HasIndex(t => new { t.ClinicId, t.PatientId, t.FdiToothNumber, t.RecordedAtUtc });
            b.HasQueryFilter(t => _currentClinic.IsPlatformAdmin || t.ClinicId == _currentClinic.ClinicId);
        });

        // PriceListItems tenant table (billing schema)
        builder.Entity<PriceListItem>(b =>
        {
            b.ToTable("PriceListItems", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.PriceCode).HasMaxLength(64).IsRequired();
            b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            b.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.UnitCostAmount).HasPrecision(18, 2);
            b.Property(x => x.DefaultPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PriceCode });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // Charges tenant table (billing schema)
        builder.Entity<Charge>(b =>
        {
            b.ToTable("Charges", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.StandardSessionPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.FreeSessionReason).HasMaxLength(500);
            b.Property(x => x.CustomSessionPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.CustomPriceReason).HasMaxLength(500);
            b.Property(x => x.BaseSessionPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.ToolsTotalCostAmount).HasPrecision(18, 2);
            b.Property(x => x.ToolsTotalChargedAmount).HasPrecision(18, 2);
            b.Property(x => x.ToolsMarkupAmount).HasPrecision(18, 2);
            b.Property(x => x.ExtraChargesAmount).HasPrecision(18, 2);
            b.Property(x => x.ExtraChargeReason).HasMaxLength(500);
            b.Property(x => x.GrossSubtotalAmount).HasPrecision(18, 2);
            b.Property(x => x.DiscountPercent).HasPrecision(9, 4);
            b.Property(x => x.DiscountFixedAmount).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.DiscountReason).HasMaxLength(500);
            b.Property(x => x.UnroundedNetTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.NetTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.RoundingRuleApplied).HasMaxLength(32).IsRequired();
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.CreatedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.ClinicId, x.VisitId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ChargeLines tenant table (billing schema)
        builder.Entity<ChargeLine>(b =>
        {
            b.ToTable("ChargeLines", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Description).HasMaxLength(300).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 2);
            b.Property(x => x.UnitCostAmount).HasPrecision(18, 2);
            b.Property(x => x.UnitChargedPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.LineCostAmount).HasPrecision(18, 2);
            b.Property(x => x.LineTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.MarkupAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ChargeId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // LedgerEntries tenant table (billing schema, append-only)
        builder.Entity<LedgerEntry>(b =>
        {
            b.ToTable("LedgerEntries", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.EntryType).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.ReferenceKind).HasMaxLength(64).IsRequired();
            b.Property(x => x.SignedAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.Description).HasMaxLength(300).IsRequired();
            b.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId, x.OccurredAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // Payments tenant table (billing schema, append-only)
        builder.Entity<Payment>(b =>
        {
            b.ToTable("Payments", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.ReceiptNumber).HasMaxLength(64).IsRequired();
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            b.Property(x => x.CollectedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ReceiptNumber }).IsUnique();
            b.HasIndex(x => new { x.ClinicId, x.PatientId, x.PaidAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PaymentAllocations tenant table (billing schema)
        builder.Entity<PaymentAllocation>(b =>
        {
            b.ToTable("PaymentAllocations", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.AllocatedAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PaymentId });
            b.HasIndex(x => new { x.ClinicId, x.ChargeId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // Refunds tenant table (billing schema, append-only)
        builder.Entity<Refund>(b =>
        {
            b.ToTable("Refunds", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            b.Property(x => x.ReceiptNumber).HasMaxLength(64);
            b.Property(x => x.IssuedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId, x.RefundedAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // SkippedReceiptNumbers tenant table (billing schema)
        builder.Entity<SkippedReceiptNumber>(b =>
        {
            b.ToTable("SkippedReceiptNumbers", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.ReceiptNumber).HasMaxLength(64).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            b.Property(x => x.RecordedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ReceiptNumber }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // LabWorkOrders tenant table (billing schema)
        builder.Entity<LabWorkOrder>(b =>
        {
            b.ToTable("LabWorkOrders", "billing");
            b.HasKey(x => x.Id);
            b.Property(x => x.LabName).HasMaxLength(200).IsRequired();
            b.Property(x => x.WorkDescription).HasMaxLength(500).IsRequired();
            b.Property(x => x.ToothReference).HasMaxLength(64);
            b.Property(x => x.LabCostAmount).HasPrecision(18, 2);
            b.Property(x => x.PatientPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId, x.CreatedAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // Suppliers tenant table (finance schema)
        builder.Entity<Supplier>(b =>
        {
            b.ToTable("Suppliers", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.Name });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // SupplierPayments tenant table (finance schema, append-only)
        builder.Entity<SupplierPayment>(b =>
        {
            b.ToTable("SupplierPayments", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.ReferenceNote).HasMaxLength(500);
            b.Property(x => x.PaidByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.SupplierId, x.PaidAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // Expenses tenant table (finance schema)
        builder.Entity<Expense>(b =>
        {
            b.ToTable("Expenses", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Category).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.RecordedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.OccurredAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // DailyClosings tenant table (finance schema)
        builder.Entity<DailyClosing>(b =>
        {
            b.ToTable("DailyClosings", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.TotalExpectedAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalCountedAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalVarianceAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.ClosedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.CloseNotes).HasMaxLength(1000);
            b.Property(x => x.ReopenedByUserId).HasMaxLength(128);
            b.Property(x => x.ReopenReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ClosingDate }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ClosingMethodTotals tenant table (finance schema)
        builder.Entity<ClosingMethodTotal>(b =>
        {
            b.ToTable("ClosingMethodTotals", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.PaymentsTotal).HasPrecision(18, 2);
            b.Property(x => x.RefundsTotal).HasPrecision(18, 2);
            b.Property(x => x.ExpensesTotal).HasPrecision(18, 2);
            b.Property(x => x.ExpectedTotal).HasPrecision(18, 2);
            b.Property(x => x.CountedTotal).HasPrecision(18, 2);
            b.Property(x => x.VarianceAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.DailyClosingId, x.Method }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // TreatmentPlans tenant table (plans schema)
        builder.Entity<TreatmentPlan>(b =>
        {
            b.ToTable("TreatmentPlans", "plans");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.FixedTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.IncludedMaterialsCostAmount).HasPrecision(18, 2);
            b.Property(x => x.UpfrontAmount).HasPrecision(18, 2);
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.CreatedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.ClinicId, x.DoctorId, x.Status });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PlanSessions tenant table (plans schema)
        builder.Entity<PlanSession>(b =>
        {
            b.ToTable("PlanSessions", "plans");
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.SessionPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.MaterialsCostAmount).HasPrecision(18, 2);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.TreatmentPlanId, x.SequenceNumber }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PlanPaymentScheduleItems tenant table (plans schema)
        builder.Entity<PlanPaymentScheduleItem>(b =>
        {
            b.ToTable("PlanPaymentScheduleItems", "plans");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.TreatmentPlanId, x.InstallmentNumber }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PatientAccounts tenant table (portal schema)
        builder.Entity<PatientAccount>(b =>
        {
            b.ToTable("PatientAccounts", "portal");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).HasMaxLength(256);
            b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(512);
            b.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.PrintedCodeHmac).HasMaxLength(128);
            b.Property(x => x.GuardianName).HasMaxLength(200);
            b.Property(x => x.OnlineBookingBlockReason).HasMaxLength(300);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId }).IsUnique().HasFilter("[IsVoided] = 0");
            b.HasIndex(x => new { x.ClinicId, x.NormalizedEmail });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PortalConsents tenant table (portal schema)
        builder.Entity<PortalConsent>(b =>
        {
            b.ToTable("PortalConsents", "portal");
            b.HasKey(x => x.Id);
            b.Property(x => x.TermsVersion).HasMaxLength(64).IsRequired();
            b.Property(x => x.Channel).HasMaxLength(64).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PatientId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicPortalSettings tenant table (portal schema)
        builder.Entity<ClinicPortalSetting>(b =>
        {
            b.ToTable("ClinicPortalSettings", "portal");
            b.HasKey(x => x.Id);
            b.Property(x => x.TermsTextAr).HasMaxLength(2000).IsRequired();
            b.Property(x => x.TermsTextEn).HasMaxLength(2000).IsRequired();
            b.Property(x => x.WelcomeMessageAr).HasMaxLength(1000).IsRequired();
            b.Property(x => x.WelcomeMessageEn).HasMaxLength(1000).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.ClinicId).IsUnique().HasFilter("[IsVoided] = 0");
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicPortalBookingSettings tenant table (portal schema)
        builder.Entity<ClinicPortalBookingSetting>(b =>
        {
            b.ToTable("ClinicPortalBookingSettings", "portal");
            b.HasKey(x => x.Id);
            b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.ClinicId).IsUnique().HasFilter("[IsVoided] = 0");
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // InventoryItems tenant table (inventory schema)
        builder.Entity<InventoryItem>(b =>
        {
            b.ToTable("InventoryItems", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(64).IsRequired();
            b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            b.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
            b.Property(x => x.NormalizedSearchName).HasMaxLength(420).IsRequired();
            b.Property(x => x.UnitName).HasMaxLength(64).IsRequired();
            b.Property(x => x.UnitCostAmount).HasPrecision(18, 2);
            b.Property(x => x.DefaultMarkupPercent).HasPrecision(9, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.LowStockThreshold).HasPrecision(18, 2);
            b.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            b.Property(x => x.SpecialtyCode).HasMaxLength(64);
            b.Property(x => x.StorageLocation).HasMaxLength(200);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.Code }).IsUnique().HasFilter("[IsVoided] = 0");
            b.HasIndex(x => new { x.ClinicId, x.NormalizedSearchName });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // InventoryItemBatches tenant table (inventory schema)
        builder.Entity<InventoryItemBatch>(b =>
        {
            b.ToTable("InventoryItemBatches", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.BatchNumber).HasMaxLength(100).IsRequired();
            b.Property(x => x.QuantityReceived).HasPrecision(18, 2);
            b.Property(x => x.QuantityRemaining).HasPrecision(18, 2);
            b.Property(x => x.UnitCostAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ItemId, x.ExpiryDate });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // StockMovements tenant table (inventory schema, append-only)
        builder.Entity<StockMovement>(b =>
        {
            b.ToTable("StockMovements", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.QuantityDelta).HasPrecision(18, 2);
            b.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            b.Property(x => x.UnitCostAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.CreatedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ItemId, x.OccurredAtUtc });
            b.HasIndex(x => new { x.ClinicId, x.ReferenceVisitId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PurchaseInvoices tenant table (inventory schema)
        builder.Entity<PurchaseInvoice>(b =>
        {
            b.ToTable("PurchaseInvoices", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
            b.Property(x => x.InvoiceNumber).HasMaxLength(100).IsRequired();
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.Property(x => x.RemainingAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.RecordedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.SupplierId, x.InvoiceDate });
            b.HasIndex(x => new { x.ClinicId, x.InvoiceNumber }).IsUnique().HasFilter("[IsVoided] = 0");
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PurchaseInvoiceLines tenant table (inventory schema)
        builder.Entity<PurchaseInvoiceLine>(b =>
        {
            b.ToTable("PurchaseInvoiceLines", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.TotalUnits).HasPrecision(18, 2);
            b.Property(x => x.PackPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.LineTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.UnitCostAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.BatchNumber).HasMaxLength(100).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PurchaseInvoiceId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicInventorySettings tenant table (inventory schema)
        builder.Entity<ClinicInventorySetting>(b =>
        {
            b.ToTable("ClinicInventorySettings", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.NegativeStockPolicy).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.ClinicId).IsUnique().HasFilter("[IsVoided] = 0");
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // InventoryAlerts tenant table (inventory schema)
        builder.Entity<InventoryAlert>(b =>
        {
            b.ToTable("InventoryAlerts", "inventory");
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.MessageAr).HasMaxLength(500).IsRequired();
            b.Property(x => x.MessageEn).HasMaxLength(500).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.IsResolved, x.Kind });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // SessionDurations tenant table (time_engine schema)
        builder.Entity<SessionDuration>(b =>
        {
            b.ToTable("SessionDurations", "time_engine");
            b.HasKey(x => x.Id);
            b.Property(x => x.ExcuseReason).HasMaxLength(500);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.VisitId }).IsUnique();
            b.HasIndex(x => new { x.ClinicId, x.DoctorId, x.CategoryId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // CategoryTimeStats tenant table (time_engine schema)
        builder.Entity<CategoryTimeStat>(b =>
        {
            b.ToTable("CategoryTimeStats", "time_engine");
            b.HasKey(x => x.Id);
            b.Property(x => x.TrimmedAverageMinutes).HasPrecision(10, 2);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.DoctorId, x.CategoryId }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // DoctorTimeEngineSettings tenant table (time_engine schema)
        builder.Entity<DoctorTimeEngineSetting>(b =>
        {
            b.ToTable("DoctorTimeEngineSettings", "time_engine");
            b.HasKey(x => x.Id);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.DoctorId }).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ClinicTimeEngineSettings tenant table (time_engine schema)
        builder.Entity<ClinicTimeEngineSetting>(b =>
        {
            b.ToTable("ClinicTimeEngineSettings", "time_engine");
            b.HasKey(x => x.Id);
            b.Property(x => x.TrimFraction).HasPrecision(5, 2);
            b.Property(x => x.MaxStepChangePercent).HasPrecision(5, 2);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.ClinicId).IsUnique();
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // FixedCosts tenant table (finance schema)
        builder.Entity<FixedCost>(b =>
        {
            b.ToTable("FixedCosts", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Recurrence).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.PaidFromMethod).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
            b.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(500);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.IsActive, x.EffectiveFromDate });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // Employees tenant table (finance schema; fixed salary, no doctor commission)
        builder.Entity<Employee>(b =>
        {
            b.ToTable("Employees", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.RoleTitle).HasMaxLength(100).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(40);
            b.Property(x => x.BaseSalaryAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.PaidFromMethod).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.IsActive });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PayrollRuns tenant table (finance schema)
        builder.Entity<PayrollRun>(b =>
        {
            b.ToTable("PayrollRuns", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.PaidFromMethod).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.TotalBaseSalary).HasPrecision(18, 2);
            b.Property(x => x.TotalBonuses).HasPrecision(18, 2);
            b.Property(x => x.TotalDeductions).HasPrecision(18, 2);
            b.Property(x => x.TotalAdvances).HasPrecision(18, 2);
            b.Property(x => x.TotalNetPay).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.CreatedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.FinalizedByUserId).HasMaxLength(128);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PeriodYear, x.PeriodMonth });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PayrollLines tenant table (finance schema)
        builder.Entity<PayrollLine>(b =>
        {
            b.ToTable("PayrollLines", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.EmployeeNameSnapshot).HasMaxLength(200).IsRequired();
            b.Property(x => x.BaseSalaryAmount).HasPrecision(18, 2);
            b.Property(x => x.BonusTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.DeductionTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.AdvanceTotalAmount).HasPrecision(18, 2);
            b.Property(x => x.NetPayAmount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PayrollRunId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // PayrollAdjustments tenant table (finance schema)
        builder.Entity<PayrollAdjustment>(b =>
        {
            b.ToTable("PayrollAdjustments", "finance");
            b.HasKey(x => x.Id);
            b.Property(x => x.AdjustmentType).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(300).IsRequired();
            b.Property(x => x.RecordedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.PeriodYear, x.PeriodMonth, x.EmployeeId });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ReportExportLogs tenant table (reports schema)
        builder.Entity<ReportExportLog>(b =>
        {
            b.ToTable("ReportExportLogs", "reports");
            b.HasKey(x => x.Id);
            b.Property(x => x.ReportKind).HasConversion<string>().HasMaxLength(48).IsRequired();
            b.Property(x => x.Format).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ExportedAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ScheduledReportConfigs tenant table (reports schema)
        builder.Entity<ScheduledReportConfig>(b =>
        {
            b.ToTable("ScheduledReportConfigs", "reports");
            b.HasKey(x => x.Id);
            b.Property(x => x.ReportKind).HasConversion<string>().HasMaxLength(48).IsRequired();
            b.Property(x => x.Format).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.Frequency).HasMaxLength(32).IsRequired();
            b.Property(x => x.CreatedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.VoidedBy).HasMaxLength(128);
            b.Property(x => x.VoidReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.IsEnabled });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
        });

        // ImportBatchLogs tenant table (import schema)
        builder.Entity<ImportBatchLog>(b =>
        {
            b.ToTable("ImportBatchLogs", "import");
            b.HasKey(x => x.Id);
            b.Property(x => x.TargetKind).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.ClinicId, x.ExecutedAtUtc });
            b.HasQueryFilter(x => _currentClinic.IsPlatformAdmin || x.ClinicId == _currentClinic.ClinicId);
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
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicAssets,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicModules,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicModules,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicRoles,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicRoles,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicRolePermissions,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicRolePermissions,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.UserPermissionOverrides,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.UserPermissionOverrides,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.UserDashboardLayouts,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.UserDashboardLayouts,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicRooms,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicRooms,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicDoctors,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicDoctors,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicWorkingHours,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicWorkingHours,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicDaysOff,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ClinicDaysOff,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.NumberBlocks,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.NumberBlocks,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ProcessedOperations,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON core.ProcessedOperations,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.Patients,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.Patients,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.PatientAllergies,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.PatientAllergies,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.PatientNotes,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.PatientNotes,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.PatientAttachments,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON patients.PatientAttachments,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.AppointmentCategories,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.AppointmentCategories,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.DoctorBookingSettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.DoctorBookingSettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.ClinicSchedulingSettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.ClinicSchedulingSettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.Appointments,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON scheduling.Appointments,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON queue.QueueEntries,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON queue.QueueEntries,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON queue.WaitingListEntries,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON queue.WaitingListEntries,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON queue.SlotOffers,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON queue.SlotOffers,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.Visits,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.Visits,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitVitals,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitVitals,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitDiagnoses,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitDiagnoses,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitProcedures,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitProcedures,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.Prescriptions,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.Prescriptions,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.PrescriptionItems,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.PrescriptionItems,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.LabOrders,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.LabOrders,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.ClinicalForms,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.ClinicalForms,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitFormData,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.VisitFormData,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.ToothRecords,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON clinical.ToothRecords,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.PriceListItems,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.PriceListItems,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.Charges,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.Charges,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.ChargeLines,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.ChargeLines,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.LedgerEntries,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.LedgerEntries,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.Payments,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.Payments,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.PaymentAllocations,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.PaymentAllocations,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.Refunds,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.Refunds,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.SkippedReceiptNumbers,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.SkippedReceiptNumbers,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.LabWorkOrders,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON billing.LabWorkOrders,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.Suppliers,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.Suppliers,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.SupplierPayments,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.SupplierPayments,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.Expenses,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.Expenses,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.DailyClosings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.DailyClosings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.ClosingMethodTotals,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.ClosingMethodTotals,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON plans.TreatmentPlans,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON plans.TreatmentPlans,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON plans.PlanSessions,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON plans.PlanSessions,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON plans.PlanPaymentScheduleItems,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON plans.PlanPaymentScheduleItems,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.PatientAccounts,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.PatientAccounts,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.PortalConsents,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.PortalConsents,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.ClinicPortalSettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.ClinicPortalSettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.ClinicPortalBookingSettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON portal.ClinicPortalBookingSettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.InventoryItems,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.InventoryItems,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.InventoryItemBatches,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.InventoryItemBatches,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.StockMovements,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.StockMovements,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.PurchaseInvoices,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.PurchaseInvoices,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.PurchaseInvoiceLines,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.PurchaseInvoiceLines,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.ClinicInventorySettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.ClinicInventorySettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.InventoryAlerts,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON inventory.InventoryAlerts,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.SessionDurations,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.SessionDurations,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.CategoryTimeStats,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.CategoryTimeStats,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.DoctorTimeEngineSettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.DoctorTimeEngineSettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.ClinicTimeEngineSettings,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON time_engine.ClinicTimeEngineSettings,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.FixedCosts,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.FixedCosts,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.Employees,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.Employees,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.PayrollRuns,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.PayrollRuns,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.PayrollLines,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.PayrollLines,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.PayrollAdjustments,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON finance.PayrollAdjustments,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON reports.ReportExportLogs,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON reports.ReportExportLogs,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON reports.ScheduledReportConfigs,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON reports.ScheduledReportConfigs,
                ADD FILTER PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON import.ImportBatchLogs,
                ADD BLOCK PREDICATE sec.fn_tenantSecurityPredicate(ClinicId) ON import.ImportBatchLogs
                WITH (STATE = ON);
            ');
        ";

        await Database.ExecuteSqlRawAsync(rlsScript, cancellationToken);
    }
}
