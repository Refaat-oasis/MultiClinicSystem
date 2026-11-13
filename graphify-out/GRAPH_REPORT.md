# Graph Report - Clinic  (2026-11-14)

## Corpus Check
- Large corpus: 1066 files · ~310,075 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder.

## Summary
- 6919 nodes · 24790 edges · 260 communities (230 shown, 30 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 2880 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `83110ef`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Core Domain & Value Objects
- Core Application DTOs
- API DTOs & Connections
- Platform API Project Layer
- Auth & Email Test Fixtures
- Module Catalog & Identity Roles
- Tenant Scoping & Patient Privacy
- Portal Session & HMAC Codes
- Scheduling & Day Close Core
- Clinic Context & DI Setup
- CoreDbContext Schema Mappings
- Finance & Payroll Infrastructure
- HTTP Controllers & Attributes
- Architecture & BuildingBlocks Tests
- Billing & Clinics Endpoints
- Platform Access & RLS Interceptor
- Super Admin 1h TOTP Security
- Sync Devices & Time Tracking
- ASP.NET Identity Entity Config
- Phase Gates & Verification Codes
- Clinical Sessions & Odontogram
- Fixed Costs & Financial Ops
- Patient Portal Account Flows
- Inventory & FEFO Stock Control
- React Workspaces & PWA Config
- Invite User Module
- offline Db Module
- Phase11Gate Module
- Clinic Role Module
- Phase13Time Engine Screen Module
- Phase15Offline Sync Screen Module
- Lab Work Order Module
- Charge Module
- Refresh Token Module
- Inventory Item Batch Module
- package Module
- Clinic Time Engine Setting Module
- Clinic Module
- Patient Module
- Visit Module
- Patients Module
- Sync Conflict Module
- Price List Item Module
- package Module
- Category Time Stat Module
- Billing View Model Mapper Module
- Appointment Module
- Patient Attachment Module
- Clinical View Model Mapper Module
- Platform Dashboard Screen Module
- Scheduling View Model Mapper Module
- Patient Allergy Module
- Treatment Plan Module
- Clinic Structure Module
- Employee Module
- Inventory Item Module
- Staff Portal Management Module
- package Module
- Portal App Module
- package Module
- Clinic Management Module
- App Device Module
- Scheduling Module
- Slot Offer Module
- Treatment Plan Module
- Clinic Portal Booking Setting Module
- Create Payroll Run Module
- Phase4Screens Module
- Closing Method Total Module
- Reports Module
- Password Policy Module
- Sync View Model Mapper Module
- Roles Permissions Module
- Plan Session Module
- Stock Movement Module
- Patient Access Log Module
- Payroll Run Module
- Book Appointment Module
- SYSTEM_DOCUMENTATION Module
- Billing Domain Module
- Plan Payment Schedule Item Module
- Clinical Module
- Record Visit Vitals Module
- Money Module
- vitest.config Module
- Plan Installment Status Module
- Clinic Inventory Setting Module
- Report Export Kind Module
- Finance Module
- Inventory Module
- Daily Closing Module
- Ledger Entry Module
- Session Duration Module
- Patient View Model Mapper Module
- Appointment Category Module
- Inventory Alert Module
- Sync Operation Log Module
- Portal Isolation Sweep Module
- Slug Generator Module
- Sync Module
- Clinic Sync Setting Module
- Billing Receipt Pdf Generator Module
- Reports Pdf Generator Module
- Patient Portal View Model Module
- Payroll Adjustment Module
- Purchase Invoice Module
- Purchase Invoice Line Module
- Receive Purchase Invoice Module
- Inventory View Model Mapper Module
- Patient Portal View Model Module
- Finance Reports View Model Module
- ISpecialty Plugin Module
- Record Payroll Adjustment Module
- Clinic Doctor Module
- Expense Module
- Logo Image Processor Module
- Payroll Line Module
- Update Clinic Portal Booking Module
- Phase12Inventory Screen Module
- Reports Dapper Queries Module
- Refund Module
- Clinic Scheduling Setting Module
- Waiting List Entry Module
- tsconfig.base Module
- Excel Import Module
- IClinic Management Module
- Charge Line Module
- Clinic Portal Setting Module
- Clinic Room Module
- Lab Order Module
- Payment Module
- Scheduled Report Config Module
- Sync Device State Module
- Visit Vital Module
- Treatment Plans Module
- Create Clinic Day Off Module
- Phase8Billing Screen Module
- Phase6Scheduling Screen Module
- Excel Import Module
- package Module
- Clinic Asset Module
- Clinic Specialty Module
- Clinic Working Hour Module
- Treatment Plan Calculator Module
- Clinic Module State View Module
- Add To Waiting List Module
- launch Settings Module
- Phase7Clinical Screen Module
- IExcel Import Module
- Profile Module
- Arabic Normalizer Module
- Clinic Day Off Module
- Doctor Booking Setting Module
- Number Block Module
- Supplier Module
- Sync Module
- Phase9Treatment Plans Screen Module
- Inventory Fefo Allocator Module
- Patient Portal View Model Module
- Tooth Record Module
- Time Engine Module
- Prescription Item Module
- Upsert Inventory Item Module
- Phase10Portal Screen Module
- Phase11Portal Booking Screen Module
- Integration Module
- Audit Log Module
- Clinical Form Module
- Import Batch Log Module
- Prescription Item Module
- Phase5Patients Screen Module
- Owner Module
- Prescription Module
- Report Export Log Module
- Supplier Payment Module
- Visit Diagnosis Module
- Reports Dapper Queries Module
- Upsert Employee Module
- Gates Module
- Phase14Finance Reports Screen Module
- IFile Storage Module
- Reports Dapper Queries Module
- Building Blocks Module
- Excel Import Module
- Outbox Message Module
- ISpecialty Catalog Module
- Plan Session Status Module
- Skipped Receipt Number Module
- Sync Operation Status Module
- Tooth Condition Module
- Complete Visit Module
- Architecture Module
- Activation Screen Module
- Plan Suggested Slot Module
- Supplier Type Module
- Clinic Module Module
- Sync Calculator Module
- Clinical Domain Module
- User Permission Override Module
- Reports Dapper Queries Module
- Update Clinic Doctor Module
- Update Visit Notes Module
- tsconfig Module
- tsconfig Module
- Platform.Api Module
- Unit Module
- Guid V7 Module
- Verification Codes Module
- Treatment Plan Mode Module
- Platform Clinic Report Row Module
- Payroll Run Status Module
- Patient Portal View Model Module
- User View Model Module
- Core.Infrastructure Module
- Sent Email Module
- Queue Entry Status Module
- Update Clinic Room Module
- Update Clinic Scheduling Settings Module
- Clinic Schedule Hub Module
- Treatment Plan Module
- Add Visit Procedure Module
- Adjust Stock Module
- Close Day Module
- Permission Override Item Module
- tsconfig Module
- tsconfig Module
- tsconfig Module
- IAuditable Module
- Clinical Form Schema Validator Module
- Reports Dapper Queries Module
- Create Clinic Role Module
- Create Lab Order Module
- Create Specialty Form Module
- Enqueue Walk In Module
- Update Clinic Time Engine Module
- Clinical Form View Model Module
- Clinic Portal Inspect View Module
- Doctor Financial Report Row Module
- Portal Access Log Item Module
- Session Category Report Row Module
- Tooth Record Module
- Treatment Plan Status Module
- Invite Staff Member Module
- Set Session Duration Excused Module
- Update Clinic Inventory Setting Module
- Import Row Error View Module
- Clinic Schedule Hub Module
- Core.Domain Module
- Slug Check Module
- Reports Dapper Queries Module
- Deactivate Owner Module
- Save Visit Form Data Module
- Set Doctor Time Engine Module
- Set Patient Online Booking Module
- Update Doctor Booking Setting Module
- Update Owner Quota Module
- Invite User View Model Module
- Allocate Number Block Module

## God Nodes (most connected - your core abstractions)
1. `Result` - 621 edges
2. `Core.Application` - 406 edges
3. `Core.Domain` - 355 edges
4. `CoreDbContext` - 331 edges
5. `Platform.Api.ViewModels` - 223 edges
6. `BuildingBlocks.Domain` - 215 edges
7. `Platform.Api.DTOs` - 175 edges
8. `Core.Infrastructure` - 90 edges
9. `ITenantEntity` - 83 edges
10. `BuildingBlocks.Application` - 79 edges

## Surprising Connections (you probably didn't know these)
- `Three Isolated Auth Realms (Staff / Portal / Super Admin)` --evaluates_permissions_via--> `EffectivePermissionsEvaluator`  [EXTRACTED]
  docs/SYSTEM_DOCUMENTATION.md → src/Modules/Core/Core.Domain/EffectivePermissionsEvaluator.cs
- `Switchable Module Catalog (16 Modules & DAG)` --implemented_by--> `ModuleCatalogService`  [EXTRACTED]
  docs/SYSTEM_DOCUMENTATION.md → src/Modules/Core/Core.Infrastructure/ModuleCatalogService.cs
- `SQL Server Row-Level Security & Tenant Isolation` --implemented_by--> `TenantSessionInterceptor`  [EXTRACTED]
  docs/SYSTEM_DOCUMENTATION.md → src/BuildingBlocks/Infrastructure/TenantSessionInterceptor.cs
- `Owner-Only Full Clinic ZIP Export (clinic.export)` --implemented_by--> `ClinicExportService`  [EXTRACTED]
  docs/SYSTEM_DOCUMENTATION.md → src/Modules/Core/Core.Infrastructure/ClinicExportService.cs
- `Owner-Only Full Clinic ZIP Export (clinic.export)` --exposed_by--> `ClinicExportController`  [EXTRACTED]
  docs/SYSTEM_DOCUMENTATION.md → src/Platform.Api/Controllers/ClinicExportController.cs

## Import Cycles
- None detected.

## Communities (260 total, 30 thin omitted)

### Community 0 - "Core Domain & Value Objects"
Cohesion: 0.01
Nodes (9): Core.Application, Core.Domain, BuildingBlocks.Domain, AddVisitDiagnosisInput, AddVisitProcedureInput, CreateLabOrderInput, PatientPortalResetPasswordInput, RecordVisitVitalsInput (+1 more)

### Community 1 - "Core Application DTOs"
Cohesion: 0.03
Nodes (37): AuditLogItemResult, CategoryTimeStatResult, ClinicBillingInspectResult, ClinicDayOffResult, ClinicRoomResult, ClinicSchedulingSettingResult, ClinicWorkingHourResult, CreateLabWorkBillingInput (+29 more)

### Community 2 - "API DTOs & Connections"
Cohesion: 0.02
Nodes (90): TenantSessionInterceptor, AddPatientAllergyDto, CreateClinicDoctorDto, AssignedRoomIds, FullName, SlotDurationMinutes, SpecialtyCodes, TitleAr (+82 more)

### Community 3 - "Platform API Project Layer"
Cohesion: 0.02
Nodes (61): Platform.Api.ViewModels, ArchitectureTests, Platform.Api.Controllers, Platform.Api.DTOs, Platform.Api.Filters, AddPatientNoteDto, AllocateNumberBlockDto, AssignStaffRoleDto (+53 more)

### Community 4 - "Auth & Email Test Fixtures"
Cohesion: 0.07
Nodes (51): FakeEmailSender, SentMessages, ActivatePatientPortalInput, BookAppointmentInput, ChargeLineInput, CompleteVisitInput, CreateChargeInput, CreateExpenseInput (+43 more)

### Community 5 - "Module Catalog & Identity Roles"
Cohesion: 0.04
Nodes (39): ClinicModuleStateResult, ClinicRoleResult, ClinicSummaryResult, DashboardWidgetCatalogResult, DashboardWidgetDefinitionResult, IModuleCatalogService, IModuleManifest, DashboardWidgets (+31 more)

### Community 6 - "Tenant Scoping & Patient Privacy"
Cohesion: 0.06
Nodes (18): Error, Result, Error, IsFailure, IsSuccess, Result, Value, ClinicPatientScopingResult (+10 more)

### Community 7 - "Portal Session & HMAC Codes"
Cohesion: 0.04
Nodes (32): ClinicPortalInspectResult, ClinicPortalSettingResult, IClinicalPortalQueries, IPatientPortalQueries, ISchedulingPortalQueries, PatientPortalAdminStatusResult, PortalBookingRequestItemResult, PortalContactSnapshotResult (+24 more)

### Community 8 - "Scheduling & Day Close Core"
Cohesion: 0.06
Nodes (28): AppointmentCategoryResult, AppointmentItemResult, ClinicScheduleInspectResult, CloseDayResult, DoctorTimetableResult, FreeSlotItemResult, IPortalBooking, QueueEntryItemResult (+20 more)

### Community 9 - "Clinic Context & DI Setup"
Cohesion: 0.05
Nodes (54): CurrentClinicContext, ClinicId, HasTenant, IsPlatformAdmin, ICurrentClinic, ClinicId, HasTenant, IsPlatformAdmin (+46 more)

### Community 10 - "CoreDbContext Schema Mappings"
Cohesion: 0.02
Nodes (92): CoreDbContext, AppDevices, AppointmentCategories, Appointments, AuditLogs, CategoryTimeStats, ChargeLines, Charges (+84 more)

### Community 11 - "Finance & Payroll Infrastructure"
Cohesion: 0.06
Nodes (20): ClinicDoctorResult, ClinicFinanceReportsInspectResult, CreatePayrollRunInput, EmployeeResult, IFinanceExtendedService, ImportBatchLogResult, IncomeVsExpensesReportResult, PayrollAdjustmentResult (+12 more)

### Community 12 - "HTTP Controllers & Attributes"
Cohesion: 0.07
Nodes (11): AuthController, HealthController, PatientPortalController, PlatformAuthController, PlatformOwnersController, ProfileController, PublicClinicDirectoryController, UpdateProfileDto (+3 more)

### Community 13 - "Architecture & BuildingBlocks Tests"
Cohesion: 0.10
Nodes (5): UnitTests, IntegrationTests, Gates, BuildingBlocks.Infrastructure, VerificationCodeSweepTests

### Community 14 - "Billing & Clinics Endpoints"
Cohesion: 0.10
Nodes (5): BillingController, ClinicsController, ModulesController, PlatformClinicsController, PlatformMonitoringController

### Community 15 - "Platform Access & RLS Interceptor"
Cohesion: 0.08
Nodes (4): Core.Contracts, BuildingBlocks.Application, Core.Infrastructure, ClinicTimeZoneResolver

### Community 16 - "Super Admin 1h TOTP Security"
Cohesion: 0.04
Nodes (24): PlatformAdminEnrollmentResult, PlatformAdminLoginResult, TotpValidationResult, PlatformAdmin, CreatedAtUtc, Email, EncryptedTotpSecret, FailedLoginAttempts (+16 more)

### Community 17 - "Sync Devices & Time Tracking"
Cohesion: 0.04
Nodes (40): ClinicDeviceItemResult, SwitchClinicResult, AppointmentPriority, Normal, Rush, Vip, AvailableSlotCandidate, QueueEntry (+32 more)

### Community 18 - "ASP.NET Identity Entity Config"
Cohesion: 0.03
Nodes (61): ITenantEntity, ClinicId, ClinicRoleClaim, ClinicId, ClinicRoleId, CreatedAtUtc, Id, PermissionKey (+53 more)

### Community 19 - "Phase Gates & Verification Codes"
Cohesion: 0.06
Nodes (25): IEmailSender, ActivateAccountDto, Code, DeviceId, DeviceName, Email, Password, LoginDto (+17 more)

### Community 20 - "Clinical Sessions & Odontogram"
Cohesion: 0.08
Nodes (26): ClinicalFormResult, ClinicClinicalInspectResult, DerivedToothStateResult, Icd10CatalogEntryResult, LabOrderResult, PortalVisitSummaryResult, PrescriptionItemResult, PrescriptionPdfResult (+18 more)

### Community 21 - "Fixed Costs & Financial Ops"
Cohesion: 0.04
Nodes (50): FixedCostResult, UpsertFixedCostInput, FixedCost, Amount, Category, ClinicId, Currency, EffectiveFromDate (+42 more)

### Community 22 - "Patient Portal Account Flows"
Cohesion: 0.04
Nodes (39): PatientPortalLoginInput, PatientPortalSessionResult, PatientAccount, ClinicId, CreatedAtUtc, Email, FailedAccessCount, GuardianName (+31 more)

### Community 23 - "Inventory & FEFO Stock Control"
Cohesion: 0.09
Nodes (12): AdjustStockInput, ClinicInventoryInspectResult, IInventoryService, InventoryAlertResult, InventoryItemResult, PurchaseInvoiceLineInput, PurchaseInvoiceLineResult, PurchaseInvoiceResult (+4 more)

### Community 24 - "React Workspaces & PWA Config"
Cohesion: 0.05
Nodes (46): @clinic/feature-core, vite-plugin-pwa, dependencies, @clinic/api-client@*, @clinic/feature-core@*, @clinic/ui@*, lucide-react@^0.475.0, react@^19.0.0 (+38 more)

### Community 25 - "Invite User Module"
Cohesion: 0.04
Nodes (35): BreakGlassResetDto, BreakGlassSecret, Email, ConfirmEmailChangeDto, Code, ForgotPasswordDto, Email, InviteUserDto (+27 more)

### Community 26 - "offline Db Module"
Cohesion: 0.08
Nodes (35): RFC-7807, ApiClient, RequestOptions, decryptSensitiveJson(), EncryptedPayload, encryptSensitiveJson(), exportKeyBase64(), fromBase64() (+27 more)

### Community 27 - "Phase11Gate Module"
Cohesion: 0.08
Nodes (14): ActivatePatientPortalDto, InvitePatientToPortalDto, OnboardingDetailsDto, OnboardingSlugDto, OnboardingTypesDto, PatientPortalLoginDto, SpecialtySelectionDto, TransitionAppointmentStatusDto (+6 more)

### Community 28 - "Clinic Role Module"
Cohesion: 0.04
Nodes (46): ISoftDeletable, IsVoided, VoidedAtUtc, VoidedBy, VoidReason, ClinicRole, ClinicId, CreatedAtUtc (+38 more)

### Community 29 - "Phase13Time Engine Screen Module"
Cohesion: 0.08
Nodes (29): ClinicSettingsData, ClinicSettingsScreenProps, ClinicSwitcherProps, MyClinicCard, MyClinicsScreenProps, DeviceInfo, DeviceManagementScreenProps, ForgotPasswordScreenProps (+21 more)

### Community 30 - "Phase15Offline Sync Screen Module"
Cohesion: 0.15
Nodes (46): InspectClinicOfflineSyncSnapshot, InspectClinicShell(), InspectClinicShellProps, InspectClinicSnapshot, ActiveTab, App(), DEFAULT_SPECIALTY_CATALOG, HealthStatus (+38 more)

### Community 31 - "Lab Work Order Module"
Cohesion: 0.07
Nodes (27): ChargeLineResult, ChargeResult, ExpenseResult, LabWorkBillingResult, LabWorkOrder, ChargeId, ClinicId, CreatedAtUtc (+19 more)

### Community 32 - "Charge Module"
Cohesion: 0.05
Nodes (40): Charge, AppointmentId, BaseSessionPriceAmount, ClinicId, CreatedAtUtc, CreatedByUserId, Currency, CustomPriceReason (+32 more)

### Community 33 - "Refresh Token Module"
Cohesion: 0.06
Nodes (27): AuthResult, Tokens, User, TokenResult, AccessToken, ExpiresAtUtc, RefreshToken, UserInfoResult (+19 more)

### Community 34 - "Inventory Item Batch Module"
Cohesion: 0.06
Nodes (25): InventoryItemBatchResult, PortalPatientSummaryResult, RegisterPrintedPinInput, InventoryItemBatch, BatchNumber, ClinicId, CreatedAtUtc, Currency (+17 more)

### Community 35 - "package Module"
Cohesion: 0.06
Nodes (42): dependencies, @clinic/api-client@*, @clinic/ui@*, lucide-react@^0.475.0, react@^19.0.0, react-dom@^19.0.0, @tanstack/react-query@^5.66.0, zustand@^5.0.3 (+34 more)

### Community 36 - "Clinic Time Engine Setting Module"
Cohesion: 0.06
Nodes (23): ClinicTimeEngineInspectResult, ClinicTimeEngineSettingResult, DoctorTimeEngineSettingResult, SessionDurationResult, ClinicTimeEngineSetting, ClinicId, Id, IsVoided (+15 more)

### Community 37 - "Clinic Module"
Cohesion: 0.05
Nodes (27): Clinic, City, ContactEmail, CreatedAtUtc, Currency, ExtraPhonesJson, Governorate, GraceDays (+19 more)

### Community 38 - "Patient Module"
Cohesion: 0.05
Nodes (35): Patient, Address, ApproximateAgeYears, AssignedDoctorId, BloodType, ChronicConditionsJson, ClinicId, CreatedAtUtc (+27 more)

### Community 39 - "Visit Module"
Cohesion: 0.05
Nodes (33): Visit, Advice, AppointmentId, CategoryId, ChiefComplaint, ClinicId, CreatedAtUtc, DiagnosisSummary (+25 more)

### Community 40 - "Patients Module"
Cohesion: 0.14
Nodes (5): PatientsController, CheckPatientDuplicatesDto, CreatePatientDto, VoidPatientItemDto, ClinicPatientScopingViewModel

### Community 41 - "Sync Conflict Module"
Cohesion: 0.06
Nodes (31): ResolveSyncConflictInput, SyncConflictResult, ConflictClass, AppendOnly, LastWriterWins, ServerValidated, SyncConflict, ClientPayloadJson (+23 more)

### Community 42 - "Price List Item Module"
Cohesion: 0.05
Nodes (29): ChargeLineKind, Extra, LabWork, Service, Session, Tool, PriceListItem, ClinicId (+21 more)

### Community 43 - "package Module"
Cohesion: 0.07
Nodes (37): dependencies, @clinic/api-client@*, @clinic/ui@*, lucide-react@^0.475.0, react@^19.0.0, react-dom@^19.0.0, @tanstack/react-query@^5.66.0, zustand@^5.0.3 (+29 more)

### Community 44 - "Category Time Stat Module"
Cohesion: 0.07
Nodes (25): CategoryTimeStat, AppliedDurationMinutes, CategoryId, ClinicId, DefaultDurationMinutes, DoctorId, ExcusedCount, Id (+17 more)

### Community 45 - "Billing View Model Mapper Module"
Cohesion: 0.06
Nodes (15): ChargeStatus, Draft, Posted, Voided, BillingViewModelMapper, ChargeLineViewModel, ChargeViewModel, ClinicBillingInspectViewModel (+7 more)

### Community 46 - "Appointment Module"
Cohesion: 0.06
Nodes (31): Appointment, ApprovalExpiresAtUtc, CancelReason, CategoryId, ClinicId, CreatedAtUtc, CreatedByUserId, DoctorId (+23 more)

### Community 47 - "Patient Attachment Module"
Cohesion: 0.07
Nodes (22): PatientAttachment, Category, ClinicId, ContentType, CreatedAtUtc, FileName, Id, IsVoided (+14 more)

### Community 48 - "Clinical View Model Mapper Module"
Cohesion: 0.08
Nodes (12): ClinicalViewModelMapper, DerivedToothStateViewModel, LabOrderViewModel, PatientAllergyItemViewModel, PortalVisitSummaryViewModel, PrescriptionItemViewModel, PrescriptionViewModel, VisitDiagnosisViewModel (+4 more)

### Community 49 - "Platform Dashboard Screen Module"
Cohesion: 0.15
Nodes (23): AdminEnrollmentScreen(), AdminEnrollmentScreenProps, TotpEnrollmentCompletedResponse, AdminLoginScreen(), AdminLoginScreenProps, PlatformLoginResponse, adminQueryClient, AdminTab (+15 more)

### Community 50 - "Scheduling View Model Mapper Module"
Cohesion: 0.07
Nodes (12): DoctorBookingSettingResult, SlotOfferItemResult, AppointmentItemViewModel, ClinicScheduleInspectViewModel, CloseDayViewModel, DoctorBookingSettingViewModel, DoctorTimetableViewModel, FreeSlotItemViewModel (+4 more)

### Community 51 - "Patient Allergy Module"
Cohesion: 0.06
Nodes (29): PatientAllergy, Allergen, ClinicId, CreatedAtUtc, Id, IsVoided, PatientId, Reaction (+21 more)

### Community 52 - "Treatment Plan Module"
Cohesion: 0.17
Nodes (7): ITreatmentPlanService, PayPlanInstallmentInput, PlanSessionResult, RecordPlanSessionSlipInput, RefundPlanDepositInput, TreatmentPlanResult, TreatmentPlanService

### Community 53 - "Clinic Structure Module"
Cohesion: 0.19
Nodes (3): ClinicStructureController, ClinicDayOffViewModel, ClinicWorkingHourViewModel

### Community 54 - "Employee Module"
Cohesion: 0.07
Nodes (22): Employee, BaseSalaryAmount, ClinicId, Currency, DoctorId, FullName, HiredOnDate, Id (+14 more)

### Community 55 - "Inventory Item Module"
Cohesion: 0.07
Nodes (25): InventoryItem, ClinicId, Code, CreatedAtUtc, Currency, DefaultMarkupPercent, Id, IsActive (+17 more)

### Community 56 - "Staff Portal Management Module"
Cohesion: 0.25
Nodes (3): PublicClinicLandingResult, StaffPortalManagementController, SetPortalVisibilityDto

### Community 57 - "package Module"
Cohesion: 0.09
Nodes (29): dexie, fake-indexeddb, @types/uuid, uuid, dependencies, dexie@^4.4.7, fake-indexeddb@^6.2.8, react@^19.0.0 (+21 more)

### Community 58 - "Portal App Module"
Cohesion: 0.07
Nodes (25): queryClient, PatientPortalApp, queryClient, StaffApp, PortalAllergyItem, PortalAppProps, PortalAuthMode, PortalAvailabilityData (+17 more)

### Community 59 - "package Module"
Cohesion: 0.09
Nodes (29): @testing-library/user-event, dependencies, lucide-react@^0.475.0, react@^19.0.0, react-dom@^19.0.0, devDependencies, jsdom@^26.0.0, @testing-library/react@^16.2.0 (+21 more)

### Community 60 - "Clinic Management Module"
Cohesion: 0.17
Nodes (6): ClinicDashboardResult, ClinicSettingsResult, ClinicSpecialtyItemResult, LogoUploadResult, OnboardingStateResult, ClinicManagementService

### Community 61 - "App Device Module"
Cohesion: 0.06
Nodes (23): AppDevice, ApprovedAtUtc, CreatedAtUtc, DeviceId, DeviceName, Id, IsApproved, IsRevoked (+15 more)

### Community 63 - "Slot Offer Module"
Cohesion: 0.07
Nodes (24): SlotOffer, CategoryId, ClinicId, CreatedAtUtc, DoctorId, Id, IsVoided, OfferedAtUtc (+16 more)

### Community 64 - "Treatment Plan Module"
Cohesion: 0.07
Nodes (27): TreatmentPlan, ClinicId, CompletedSessionsCount, CreatedAtUtc, CreatedByUserId, Currency, DoctorId, FixedTotalAmount (+19 more)

### Community 65 - "Clinic Portal Booking Setting Module"
Cohesion: 0.08
Nodes (23): ClinicPortalBookingSetting, ApprovalHoldHours, AutoBlockAfterNoShows, CancellationCutoffHours, ClinicId, CreatedAtUtc, Id, IsVoided (+15 more)

### Community 66 - "Create Payroll Run Module"
Cohesion: 0.08
Nodes (18): CloseDayInput, DashboardWidgetDataResult, SupplierPaymentResult, UpsertEmployeeInput, ClosingMethodSummary, PaymentMethod, Cash, InstaPay (+10 more)

### Community 67 - "Phase4Screens Module"
Cohesion: 0.08
Nodes (26): ClinicAuditEntry, ClinicDeviceItem, ClinicDoctorItem, ClinicModuleItem, ClinicRoleItem, ClinicRoomItem, ClinicStructureScreenProps, DashboardWidgetTile (+18 more)

### Community 68 - "Closing Method Total Module"
Cohesion: 0.09
Nodes (20): ClosingMethodTotalResult, DailyClosingResult, ClosingMethodTotal, ClinicId, CountedTotal, Currency, DailyClosingId, ExpectedTotal (+12 more)

### Community 69 - "Reports Module"
Cohesion: 0.16
Nodes (4): IReportsService, UpsertScheduledReportInput, PlatformReportsController, ReportsController

### Community 70 - "Password Policy Module"
Cohesion: 0.14
Nodes (6): PasswordPolicy, PasswordPolicyTests, PatientsDomainTests, Phase15SyncDomainTests, PhoneNormalizerTests, SchedulingDomainTests

### Community 71 - "Sync View Model Mapper Module"
Cohesion: 0.10
Nodes (10): AllocatedNumberBlockResult, ClinicOfflineSyncInspectResult, SyncDeviceStateResult, AllocatedNumberBlockViewModel, ClinicOfflineSyncInspectViewModel, ClinicSyncSettingViewModel, SyncDeviceStateViewModel, SyncPullItemViewModel (+2 more)

### Community 73 - "Plan Session Module"
Cohesion: 0.08
Nodes (26): PlanSession, ActualOrSlippedDate, ClinicId, EarliestAllowedDate, Id, IntervalDaysFromPrevious, IsVoided, LatestAllowedDate (+18 more)

### Community 74 - "Stock Movement Module"
Cohesion: 0.08
Nodes (25): StockMovement, BalanceAfter, BatchId, ClinicId, CreatedByUserId, Currency, Id, IsVoided (+17 more)

### Community 75 - "Patient Access Log Module"
Cohesion: 0.11
Nodes (12): ClinicPortalBookingInspectResult, ClinicPortalBookingSettingResult, UpdateClinicPortalBookingSettingInput, PatientAccessLog, ActorId, ActorType, ClinicId, Details (+4 more)

### Community 76 - "Payroll Run Module"
Cohesion: 0.08
Nodes (24): PayrollRun, ClinicId, CreatedAtUtc, CreatedByUserId, Currency, FinalizedAtUtc, FinalizedByUserId, Id (+16 more)

### Community 77 - "Book Appointment Module"
Cohesion: 0.08
Nodes (23): BookAppointmentDto, CategoryId, DoctorId, EndUtc, Id, Notes, OperationId, PatientId (+15 more)

### Community 78 - "SYSTEM_DOCUMENTATION Module"
Cohesion: 0.11
Nodes (10): Owner-Only Full Clinic ZIP Export (clinic.export), Switchable Module Catalog (16 Modules & DAG), Offline PWA Outbox & NumberBlock Sync, SQL Server Row-Level Security & Tenant Isolation, OWASP Security Headers & no-store Middleware, Three Isolated Auth Realms (Staff / Portal / Super Admin), Platform.Api.Middleware, HostRoutingMiddleware (+2 more)

### Community 79 - "Billing Domain Module"
Cohesion: 0.13
Nodes (4): BillingCalculator, ChargeCalculationResult, ChargeToolLineInput, BillingDomainTests

### Community 80 - "Plan Payment Schedule Item Module"
Cohesion: 0.10
Nodes (19): PlanPaymentScheduleItem, Amount, ClinicId, Currency, DueDate, Id, InstallmentNumber, IsUpfront (+11 more)

### Community 82 - "Record Visit Vitals Module"
Cohesion: 0.08
Nodes (21): AddVisitDiagnosisDto, Icd10Code, IsPrimary, Notes, TitleAr, TitleEn, RecordToothStateDto, Condition (+13 more)

### Community 83 - "Money Module"
Cohesion: 0.13
Nodes (5): Money, Amount, Currency, InspectClinicResult, MoneyTests

### Community 85 - "Plan Installment Status Module"
Cohesion: 0.09
Nodes (13): ClinicTreatmentPlansInspectResult, PlanInstallmentResult, PlanInstallmentStatus, Overdue, Paid, PartiallyPaid, Pending, Refunded (+5 more)

### Community 86 - "Clinic Inventory Setting Module"
Cohesion: 0.11
Nodes (17): ClinicInventorySettingResult, UpdateClinicInventorySettingInput, ClinicInventorySetting, ClinicId, CreatedAtUtc, Id, IsVoided, NearExpiryWarningDays (+9 more)

### Community 87 - "Report Export Kind Module"
Cohesion: 0.10
Nodes (17): ExportedReportFileResult, ReportExportFormat, Excel, Pdf, ReportExportKind, DoctorSummary, IncomeVsExpenses, MethodTotals (+9 more)

### Community 90 - "Daily Closing Module"
Cohesion: 0.10
Nodes (19): DailyClosing, ClinicId, ClosedAtUtc, ClosedByUserId, CloseNotes, ClosingDate, Currency, FinanceExpensesExcluded (+11 more)

### Community 91 - "Ledger Entry Module"
Cohesion: 0.09
Nodes (21): LedgerEntry, ActorUserId, ClinicId, Currency, Description, EntryType, Id, Method (+13 more)

### Community 92 - "Session Duration Module"
Cohesion: 0.10
Nodes (19): SessionDuration, CategoryId, ClinicId, CreatedAtUtc, DoctorId, ExcuseReason, Id, IsExcused (+11 more)

### Community 93 - "Patient View Model Mapper Module"
Cohesion: 0.10
Nodes (8): PatientSearchResult, PatientSummaryItemResult, PatientAttachmentItemViewModel, PatientDuplicateWarningViewModel, PatientNoteItemViewModel, PatientSearchViewModel, PatientSummaryItemViewModel, PatientViewModelMapper

### Community 94 - "Appointment Category Module"
Cohesion: 0.10
Nodes (18): AppointmentCategory, BookableOnline, ClinicId, CreatedAtUtc, DefaultDurationMinutes, DefaultPriceAmount, DefaultPriceCurrency, Id (+10 more)

### Community 95 - "Inventory Alert Module"
Cohesion: 0.10
Nodes (20): InventoryAlert, BatchId, ClinicId, CreatedAtUtc, Id, IsResolved, IsVoided, ItemId (+12 more)

### Community 96 - "Sync Operation Log Module"
Cohesion: 0.10
Nodes (21): SyncOperationLog, ActionType, ClientTimestampUtc, ClinicId, ClockSkewSeconds, ConflictClass, CursorSequence, DeviceId (+13 more)

### Community 97 - "Portal Isolation Sweep Module"
Cohesion: 0.12
Nodes (5): PortalSecurityHeadersAttribute, AdminHostSweepTests, AnonymousEndpointAllowlistTests, PolicySweepTests, PortalIsolationSweepTests

### Community 98 - "Slug Generator Module"
Cohesion: 0.22
Nodes (3): PhoneNormalizer, SlugGenerator, SlugGeneratorTests

### Community 99 - "Sync Module"
Cohesion: 0.22
Nodes (4): SyncPushBatchInput, SyncPushOperationInput, OperationExecOutcome, SyncService

### Community 100 - "Clinic Sync Setting Module"
Cohesion: 0.14
Nodes (12): ClinicSyncSettingResult, UpdateClinicSyncSettingInput, ClinicSyncSetting, ClinicId, ClockSkewWarningSeconds, Id, IsVoided, OfflineSessionMaxHours (+4 more)

### Community 101 - "Billing Receipt Pdf Generator Module"
Cohesion: 0.14
Nodes (7): LedgerEntryResult, PatientLedgerResult, PaymentResult, PortalBillingChargeItemResult, PortalBillingSummaryResult, ReceiptPdfResult, BillingReceiptPdfGenerator

### Community 102 - "Reports Pdf Generator Module"
Cohesion: 0.22
Nodes (4): MethodTotalReportRowResult, ReportsExcelGenerator, ReportsPdfGenerator, MethodTotalReportRowViewModel

### Community 103 - "Patient Portal View Model Module"
Cohesion: 0.12
Nodes (8): PortalAvailabilityResult, PortalAvailableSlotItemResult, PortalBookableCategoryItemResult, PortalBookableDoctorItemResult, PortalAvailabilityViewModel, PortalAvailableSlotItemViewModel, PortalBookableCategoryItemViewModel, PortalBookableDoctorItemViewModel

### Community 104 - "Payroll Adjustment Module"
Cohesion: 0.10
Nodes (19): PayrollAdjustment, AdjustmentType, Amount, ClinicId, Currency, EmployeeId, Id, IsVoided (+11 more)

### Community 105 - "Purchase Invoice Module"
Cohesion: 0.10
Nodes (20): PurchaseInvoice, ClinicId, CreatedAtUtc, Currency, Id, InvoiceDate, InvoiceNumber, IsVoided (+12 more)

### Community 106 - "Purchase Invoice Line Module"
Cohesion: 0.10
Nodes (20): PurchaseInvoiceLine, BatchId, BatchNumber, ClinicId, Currency, ExpiryDate, Id, IsVoided (+12 more)

### Community 107 - "Receive Purchase Invoice Module"
Cohesion: 0.10
Nodes (18): PurchaseInvoiceLineDto, BatchNumber, ExpiryDate, ItemId, PackCount, PackPriceAmount, PackSize, ReceivePurchaseInvoiceDto (+10 more)

### Community 108 - "Inventory View Model Mapper Module"
Cohesion: 0.11
Nodes (7): ClinicInventorySettingViewModel, InventoryAlertViewModel, InventoryViewModelMapper, PurchaseInvoiceLineViewModel, PurchaseInvoiceViewModel, StockMovementViewModel, SupplierInventoryStatementViewModel

### Community 109 - "Patient Portal View Model Module"
Cohesion: 0.11
Nodes (7): ClinicPortalBookingInspectViewModel, ClinicPortalBookingSettingViewModel, PatientPortalAdminStatusViewModel, PatientPortalSessionViewModel, PatientPortalViewModelMapper, PortalBookingRequestItemViewModel, PortalSignedUrlViewModel

### Community 110 - "Finance Reports View Model Module"
Cohesion: 0.13
Nodes (7): FixedCostOccurrenceResult, ClinicFinanceReportsInspectViewModel, EmployeeViewModel, FinanceReportsViewModelMapper, ImportBatchLogViewModel, IncomeVsExpensesReportViewModel, ReportExportLogViewModel

### Community 111 - "ISpecialty Plugin Module"
Cohesion: 0.11
Nodes (18): ISpecialtyPlugin, DefaultFormJsonSchema, DefaultFormTitleAr, DefaultFormTitleEn, SpecialtyCode, SupportsDentalChart, DentalSpecialtyPlugin, DefaultFormJsonSchema (+10 more)

### Community 112 - "Record Payroll Adjustment Module"
Cohesion: 0.11
Nodes (15): RecordPayrollAdjustmentInput, PayrollAdjustmentType, Advance, Bonus, Deduction, RecordPayrollAdjustmentDto, AdjustmentType, Amount (+7 more)

### Community 113 - "Clinic Doctor Module"
Cohesion: 0.11
Nodes (17): ClinicDoctor, AssignedRoomIdsJson, ClinicId, CreatedAtUtc, FullName, Id, IsActive, IsVoided (+9 more)

### Community 114 - "Expense Module"
Cohesion: 0.11
Nodes (19): Expense, Amount, Category, ClinicId, CountsAsCashOut, Currency, Description, Id (+11 more)

### Community 115 - "Logo Image Processor Module"
Cohesion: 0.18
Nodes (3): LogoImageProcessor, LogoSanitizeResult, LogoImageProcessorTests

### Community 116 - "Payroll Line Module"
Cohesion: 0.11
Nodes (19): PayrollLine, AdvanceTotalAmount, BaseSalaryAmount, BonusTotalAmount, ClinicId, Currency, DeductionTotalAmount, DoctorId (+11 more)

### Community 117 - "Update Clinic Portal Booking Module"
Cohesion: 0.10
Nodes (17): PortalBookAppointmentDto, CategoryId, DoctorId, Notes, OperationId, StartAtUtc, UpdateClinicPortalBookingSettingDto, ApprovalHoldHours (+9 more)

### Community 118 - "Phase12Inventory Screen Module"
Cohesion: 0.11
Nodes (18): AdjustStockPayload, ClinicInventoryInspectData, ClinicInventorySettingData, DEFAULT_ALERTS, DEFAULT_BATCHES, DEFAULT_ITEMS, DEFAULT_MOVEMENTS, DEFAULT_STATEMENT (+10 more)

### Community 120 - "Refund Module"
Cohesion: 0.11
Nodes (16): RefundResult, Refund, Amount, ChargeId, ClinicId, Currency, Id, IssuedByUserId (+8 more)

### Community 121 - "Clinic Scheduling Setting Module"
Cohesion: 0.11
Nodes (16): ClinicSchedulingSetting, ClinicId, CreatedAtUtc, DefaultSlotStepMinutes, Id, IsVoided, LateGraceMinutes, MaxQueuePushes (+8 more)

### Community 122 - "Waiting List Entry Module"
Cohesion: 0.11
Nodes (16): WaitingListEntry, CategoryId, ClinicId, CreatedAtUtc, DoctorId, Id, IsFulfilled, IsVoided (+8 more)

### Community 123 - "tsconfig.base Module"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleDetection, moduleResolution (+9 more)

### Community 124 - "Excel Import Module"
Cohesion: 0.29
Nodes (5): ImportRowErrorResult, DentalNotationConverter, ExcelImportService, ParsedInventoryRow, ParsedPatientRow

### Community 126 - "Charge Line Module"
Cohesion: 0.12
Nodes (17): ChargeLine, ChargeId, ClinicId, CreatedAtUtc, Currency, Description, Id, InventoryItemId (+9 more)

### Community 127 - "Clinic Portal Setting Module"
Cohesion: 0.12
Nodes (16): ClinicPortalSetting, ClinicId, DefaultVisitVisibility, Id, IsVoided, RowVersion, ShowDoctorNames, ShowPricesByDefault (+8 more)

### Community 128 - "Clinic Room Module"
Cohesion: 0.12
Nodes (15): ClinicRoom, ClinicId, Code, CreatedAtUtc, FloorOrLocation, Id, IsActive, IsVoided (+7 more)

### Community 129 - "Lab Order Module"
Cohesion: 0.12
Nodes (16): LabOrder, ClinicId, CreatedAtUtc, DoctorId, Id, Instructions, IsVoided, LabName (+8 more)

### Community 130 - "Payment Module"
Cohesion: 0.12
Nodes (16): Payment, Amount, ClinicId, CollectedByUserId, Currency, DeviceId, Id, IsDeposit (+8 more)

### Community 131 - "Scheduled Report Config Module"
Cohesion: 0.12
Nodes (16): ScheduledReportConfig, ClinicId, CreatedByUserId, Format, Frequency, Id, IsEnabled, IsVoided (+8 more)

### Community 132 - "Sync Device State Module"
Cohesion: 0.12
Nodes (14): SyncDeviceState, ClinicId, CreatedAtUtc, DeviceId, Id, IsVoided, LastOnlineAuthAtUtc, LastPullAtUtc (+6 more)

### Community 133 - "Visit Vital Module"
Cohesion: 0.12
Nodes (16): VisitVital, BloodPressureDiastolic, BloodPressureSystolic, Bmi, ClinicId, GlucoseMgDl, HeightCm, Id (+8 more)

### Community 135 - "Create Clinic Day Off Module"
Cohesion: 0.10
Nodes (14): CloneClinicRoleDto, NewName, NewNameAr, NewNameEn, CreateClinicDayOffDto, DoctorId, EndDate, ReasonAr (+6 more)

### Community 136 - "Phase8Billing Screen Module"
Cohesion: 0.12
Nodes (16): InspectClinicBillingSnapshot, applyRounding(), BillingChargeData, BillingChargeLineData, BillingClosingMethodData, BillingDailyClosingData, BillingExpenseData, BillingInitialSnapshot (+8 more)

### Community 137 - "Phase6Scheduling Screen Module"
Cohesion: 0.15
Nodes (15): InspectClinicScheduleSnapshot, AppointmentItem, DoctorOptionItem, FreeSlotItem, PatientOptionItem, Phase6SchedulingScreenProps, PRIORITY_BADGES, QueueEntryItem (+7 more)

### Community 138 - "Excel Import Module"
Cohesion: 0.27
Nodes (4): ExcelImportController, UploadExcelImportDto, File, OperationId

### Community 139 - "package Module"
Cohesion: 0.14
Nodes (12): @playwright/test, devDependencies, @playwright/test@^1.65.1, typescript@^5.8.2, typescript, name, private, scripts (+4 more)

### Community 140 - "Clinic Asset Module"
Cohesion: 0.12
Nodes (16): ClinicAsset, ClinicId, ContentType, CreatedAtUtc, FileName, FullStorageKey, Id, IsVoided (+8 more)

### Community 141 - "Clinic Specialty Module"
Cohesion: 0.12
Nodes (15): ClinicSpecialty, CatalogCode, ClinicId, CreatedAtUtc, DefaultAppointmentCategoriesJson, DefaultFormSchemaJson, HasDentalPlugin, Id (+7 more)

### Community 142 - "Clinic Working Hour Module"
Cohesion: 0.12
Nodes (14): ClinicWorkingHour, ClinicId, CreatedAtUtc, DayOfWeek, DoctorId, EndTime, Id, IsClosed (+6 more)

### Community 143 - "Treatment Plan Calculator Module"
Cohesion: 0.17
Nodes (4): PlanBalanceSummary, PlanSessionWindowCalculation, TreatmentPlanCalculator, TreatmentPlanDomainTests

### Community 144 - "Clinic Module State View Module"
Cohesion: 0.14
Nodes (7): ClinicModuleStateViewModel, ClinicSettingsViewModel, ClinicSpecialtyItemViewModel, ModuleMenuItemViewModel, MyModulesManifestViewModel, OnboardingStateViewModel, SettingsSectionViewModel

### Community 145 - "Add To Waiting List Module"
Cohesion: 0.12
Nodes (14): AddToWaitingListDto, CategoryId, DoctorId, Notes, OperationId, PatientId, PreferredDate, RescheduleAppointmentDto (+6 more)

### Community 146 - "launch Settings Module"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 147 - "Phase7Clinical Screen Module"
Cohesion: 0.15
Nodes (14): InspectClinicClinicalSnapshot, ClinicalAllergyBannerItem, ClinicalDiagnosisEntry, ClinicalPrescriptionEntry, ClinicalPrescriptionItem, ClinicalProcedureEntry, ClinicalSpecialtyFormDefinition, ClinicalToothStateEntry (+6 more)

### Community 148 - "IExcel Import Module"
Cohesion: 0.25
Nodes (7): IExcelImportService, ImportTemplateResult, ImportValidationResult, ImportTargetKind, InventoryItems, Patients, Suppliers

### Community 149 - "Profile Module"
Cohesion: 0.18
Nodes (6): UserClinicMembershipResult, UserDeviceItemResult, UserProfileResult, UserClinicMembershipViewModel, UserDeviceItemViewModel, UserProfileViewModel

### Community 150 - "Arabic Normalizer Module"
Cohesion: 0.21
Nodes (4): ArabicNormalizer, PatientDuplicateCandidate, PatientDuplicateDetector, PatientDuplicateMatch

### Community 151 - "Clinic Day Off Module"
Cohesion: 0.13
Nodes (14): ClinicDayOff, ClinicId, CreatedAtUtc, DoctorId, EndDate, Id, IsVoided, ReasonAr (+6 more)

### Community 152 - "Doctor Booking Setting Module"
Cohesion: 0.13
Nodes (13): DoctorBookingSetting, AcceptsOnlineBooking, ClinicId, CreatedAtUtc, DoctorId, Id, IsVoided, MaxDailyOnlineBookings (+5 more)

### Community 153 - "Number Block Module"
Cohesion: 0.14
Nodes (12): NumberBlock, ClinicId, CreatedAtUtc, DeviceId, FromNumber, Id, Kind, NextNumber (+4 more)

### Community 154 - "Supplier Module"
Cohesion: 0.13
Nodes (15): Supplier, ClinicId, CreatedAtUtc, Id, IsActive, IsVoided, Name, Notes (+7 more)

### Community 156 - "Phase9Treatment Plans Screen Module"
Cohesion: 0.13
Nodes (13): InspectClinicTreatmentPlansSnapshot, addDaysIso(), CompletePlanSessionPayload, CreateTreatmentPlanPayload, DEFAULT_PLANS, PayPlanInstallmentPayload, Phase9TreatmentPlansScreenProps, RefundPlanInstallmentPayload (+5 more)

### Community 157 - "Inventory Fefo Allocator Module"
Cohesion: 0.20
Nodes (5): AllocatedBatchDeduction, BatchSlice, FefoAllocationResult, InventoryFefoAllocator, InventoryDomainTests

### Community 158 - "Patient Portal View Model Module"
Cohesion: 0.14
Nodes (5): PortalVisibleNoteItemResult, PortalMedicalBackgroundViewModel, PortalSharedAttachmentItemViewModel, PortalVisibleNoteItemViewModel, PortalVisitDetailViewModel

### Community 159 - "Tooth Record Module"
Cohesion: 0.14
Nodes (14): ToothRecord, ClinicId, Condition, FdiToothNumber, Id, IsPrimaryTooth, IsVoided, Notes (+6 more)

### Community 161 - "Prescription Item Module"
Cohesion: 0.14
Nodes (12): CreatePrescriptionDto, AcknowledgeAllergyWarning, Items, Notes, OperationId, PrescriptionItemDto, ActiveIngredient, Dosage (+4 more)

### Community 162 - "Upsert Inventory Item Module"
Cohesion: 0.14
Nodes (13): UpsertInventoryItemDto, Code, DefaultMarkupPercent, IsActive, LowStockThreshold, NameAr, NameEn, OperationId (+5 more)

### Community 163 - "Phase10Portal Screen Module"
Cohesion: 0.14
Nodes (13): ClinicPortalInspectData, ClinicPortalSettingData, DEFAULT_PATIENT_STATUS, DEFAULT_PORTAL_SETTINGS(), InvitePatientPortalPayload, PatientPortalAdminStatusData, Phase10PortalScreenProps, PortalAccessLogItemData (+5 more)

### Community 164 - "Phase11Portal Booking Screen Module"
Cohesion: 0.14
Nodes (13): ClinicPortalBookingInspectData, ClinicPortalBookingSettingData, DEFAULT_BLOCK_TARGET, DEFAULT_BOOKING_SETTINGS(), DEFAULT_REQUESTS(), PatientBookingBlockTargetData, Phase11PortalBookingScreenProps, PortalBookingMode (+5 more)

### Community 165 - "Integration Module"
Cohesion: 0.15
Nodes (12): Testcontainers.MsSql (4.16.0), net10.0, ClosedXML (0.105.1), coverlet.collector (6.0.4), FluentAssertions (7.2.0), Microsoft.AspNetCore.Mvc.Testing (10.0.14), Microsoft.AspNetCore.SignalR.Client (10.0.14), Microsoft.NET.Test.Sdk (17.14.1) (+4 more)

### Community 166 - "Audit Log Module"
Cohesion: 0.15
Nodes (12): AuditLog, Action, ClinicId, EntityId, EntityName, Id, IpAddress, NewValues (+4 more)

### Community 167 - "Clinical Form Module"
Cohesion: 0.15
Nodes (12): ClinicalForm, ClinicId, CreatedAtUtc, Id, IsActive, IsVoided, JsonSchema, RowVersion (+4 more)

### Community 168 - "Import Batch Log Module"
Cohesion: 0.15
Nodes (13): ImportBatchLog, AcceptedRows, ActorUserId, ClinicId, ExecutedAtUtc, FileName, Id, IsCommitted (+5 more)

### Community 169 - "Prescription Item Module"
Cohesion: 0.15
Nodes (13): PrescriptionItem, ActiveIngredient, ClinicId, Dosage, DrugName, Duration, Frequency, Id (+5 more)

### Community 170 - "Phase5Patients Screen Module"
Cohesion: 0.18
Nodes (11): CreatePatientFormPayload, PatientAllergyItem, PatientAttachmentItem, PatientDetailData, PatientDuplicateWarningItem, PatientNoteItem, PatientScopingSettings, PatientSummaryItem (+3 more)

### Community 171 - "Owner Module"
Cohesion: 0.17
Nodes (7): Owner, CreatedAtUtc, Id, MaxClinics, Status, UserId, WelcomeSeen

### Community 172 - "Prescription Module"
Cohesion: 0.17
Nodes (12): Prescription, AllergyWarningDetails, ClinicId, CreatedAtUtc, DoctorId, HasAllergyWarning, Id, IsVoided (+4 more)

### Community 173 - "Report Export Log Module"
Cohesion: 0.17
Nodes (12): ReportExportLog, ActorUserId, ClinicId, ExportedAtUtc, FileName, Format, FromDate, Id (+4 more)

### Community 174 - "Supplier Payment Module"
Cohesion: 0.17
Nodes (12): SupplierPayment, Amount, ClinicId, Currency, Id, Method, OperationId, PaidAtUtc (+4 more)

### Community 175 - "Visit Diagnosis Module"
Cohesion: 0.17
Nodes (12): VisitDiagnosis, ClinicId, CreatedAtUtc, Icd10Code, Id, IsPrimary, IsVoided, Notes (+4 more)

### Community 176 - "Reports Dapper Queries Module"
Cohesion: 0.17
Nodes (12): DoctorSummaryDapperRow, BilledTotal, CollectedTotal, CompletedSessionsCount, DoctorId, DoctorName, LabCost, PayrollCost (+4 more)

### Community 177 - "Upsert Employee Module"
Cohesion: 0.17
Nodes (11): UpsertEmployeeDto, BaseSalaryAmount, DoctorId, EmployeeId, FullName, HiredOnDate, IsActive, OperationId (+3 more)

### Community 178 - "Gates Module"
Cohesion: 0.17
Nodes (11): net10.0, ClosedXML (0.105.1), coverlet.collector (6.0.4), FluentAssertions (7.2.0), Microsoft.AspNetCore.Mvc.Testing (10.0.14), Microsoft.AspNetCore.SignalR.Client (10.0.14), Microsoft.NET.Test.Sdk (17.14.1), Respawn (7.0.0) (+3 more)

### Community 179 - "Phase14Finance Reports Screen Module"
Cohesion: 0.17
Nodes (11): ClinicFinanceReportsInspectData, DoctorFinancialRowData, EmployeeItemData, FixedCostItemData, ImportBatchLogData, ImportRowErrorData, ImportValidationData, IncomeVsExpensesReportData (+3 more)

### Community 181 - "Reports Dapper Queries Module"
Cohesion: 0.18
Nodes (9): ClinicExportPackageResult, SessionCategoryDapperRow, BilledTotal, CategoryId, CategoryNameAr, CategoryNameEn, CompletedSessionsCount, SessionsCount (+1 more)

### Community 182 - "Building Blocks Module"
Cohesion: 0.20
Nodes (8): Microsoft.EntityFrameworkCore (10.0.14), Microsoft.EntityFrameworkCore.Relational (10.0.14), net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk

### Community 183 - "Excel Import Module"
Cohesion: 0.18
Nodes (8): ParsedImportData, Errors, InventoryItems, Patients, PreviewSummaries, Suppliers, TotalRows, ValidRowsCount

### Community 184 - "Outbox Message Module"
Cohesion: 0.18
Nodes (8): OutboxMessage, Content, CreatedAtUtc, Error, Id, ProcessedAtUtc, RetryCount, Type

### Community 185 - "ISpecialty Catalog Module"
Cohesion: 0.33
Nodes (3): ISpecialtyCatalog, SpecialtyCatalogItemResult, SpecialtyCatalog

### Community 186 - "Plan Session Status Module"
Cohesion: 0.18
Nodes (8): PortalPlanSessionItemResult, PlanSessionStatus, Cancelled, Completed, Delayed, Planned, Scheduled, PortalPlanSessionItemViewModel

### Community 187 - "Skipped Receipt Number Module"
Cohesion: 0.18
Nodes (11): SkippedReceiptNumber, ClinicId, DeviceId, Id, NumberBlockId, OperationId, Reason, ReceiptNumber (+3 more)

### Community 188 - "Sync Operation Status Module"
Cohesion: 0.20
Nodes (7): SyncOperationStatus, Applied, Conflict, DuplicateIgnored, Rejected, SyncPushBatchViewModel, SyncPushOperationOutcomeViewModel

### Community 189 - "Tooth Condition Module"
Cohesion: 0.18
Nodes (10): ToothCondition, Bridge, Caries, Crown, Extracted, Filled, Healthy, Implant (+2 more)

### Community 190 - "Complete Visit Module"
Cohesion: 0.18
Nodes (9): CompleteVisitDto, IsOvertimeExcused, OperationId, OvertimeExcuseReason, ToolsUsed, VisitToolUsageDto, InventoryItemId, PatientUnitPriceOverride (+1 more)

### Community 191 - "Architecture Module"
Cohesion: 0.20
Nodes (9): Microsoft.CodeAnalysis.CSharp (4.12.0), NetArchTest.eNhancedEdition (1.4.5), net10.0, coverlet.collector (6.0.4), FluentAssertions (7.2.0), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4) (+1 more)

### Community 192 - "Activation Screen Module"
Cohesion: 0.27
Nodes (6): ActivationResponse, ActivationScreen(), ActivationScreenProps, AuthStore, AuthUserState, useAuthStore

### Community 193 - "Plan Suggested Slot Module"
Cohesion: 0.24
Nodes (3): PlanSuggestedSlotResult, SchedulePlanSessionInput, PlanSuggestedSlotViewModel

### Community 194 - "Supplier Type Module"
Cohesion: 0.22
Nodes (7): SupplierResult, SupplierType, General, Lab, MedicalSupplies, ParsedSupplierRow, SupplierViewModel

### Community 195 - "Clinic Module Module"
Cohesion: 0.20
Nodes (9): ClinicModule, ChangedAtUtc, ChangedByUserId, ClinicId, Enabled, Id, ModuleKey, Note (+1 more)

### Community 197 - "Clinical Domain Module"
Cohesion: 0.20
Nodes (4): DentalNumberingSystem, Fdi, Universal, ClinicalDomainTests

### Community 198 - "User Permission Override Module"
Cohesion: 0.20
Nodes (9): UserPermissionOverride, ClinicId, Effect, Id, PermissionKey, RowVersion, UpdatedAtUtc, UpdatedByUserId (+1 more)

### Community 199 - "Reports Dapper Queries Module"
Cohesion: 0.20
Nodes (10): PlatformClinicDapperRow, ClinicId, ClinicName, Currency, NetIncome, OperatingExpenses, PayrollTotal, Slug (+2 more)

### Community 200 - "Update Clinic Doctor Module"
Cohesion: 0.20
Nodes (9): UpdateClinicDoctorDto, AssignedRoomIds, FullName, IsActive, SlotDurationMinutes, SpecialtyCodes, TitleAr, TitleEn (+1 more)

### Community 201 - "Update Visit Notes Module"
Cohesion: 0.20
Nodes (9): UpdateVisitNotesDto, Advice, ChiefComplaint, DiagnosisSummary, Examination, FollowUpDate, HistoryOfPresentIllness, IsVisibleToPatient (+1 more)

### Community 202 - "tsconfig Module"
Cohesion: 0.20
Nodes (9): compilerOptions, composite, declaration, declarationMap, noEmit, outDir, extends, include (+1 more)

### Community 203 - "tsconfig Module"
Cohesion: 0.20
Nodes (9): compilerOptions, composite, declaration, declarationMap, noEmit, outDir, extends, include (+1 more)

### Community 204 - "Platform.Api Module"
Cohesion: 0.22
Nodes (8): Hangfire.AspNetCore (1.8.18), Hangfire.SqlServer (1.8.18), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.14), Microsoft.AspNetCore.OpenApi (10.0.14), Microsoft.EntityFrameworkCore.Design (10.0.14), Serilog.AspNetCore (9.0.0), Microsoft.NET.Sdk.Web, net10.0

### Community 205 - "Unit Module"
Cohesion: 0.22
Nodes (8): NSubstitute (5.3.0), net10.0, coverlet.collector (6.0.4), FluentAssertions (7.2.0), Microsoft.NET.Test.Sdk (17.14.1), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 208 - "Treatment Plan Mode Module"
Cohesion: 0.22
Nodes (6): CreatePlanInstallmentInput, CreatePlanSessionInput, CreateTreatmentPlanInput, TreatmentPlanMode, FixedTotal, PerSession

### Community 209 - "Platform Clinic Report Row Module"
Cohesion: 0.25
Nodes (4): PlatformClinicReportRowResult, PlatformCrossClinicReportResult, PlatformClinicReportRowViewModel, PlatformCrossClinicReportViewModel

### Community 210 - "Payroll Run Status Module"
Cohesion: 0.25
Nodes (5): PayrollRunStatus, Draft, Finalized, PayrollLineViewModel, PayrollRunViewModel

### Community 211 - "Patient Portal View Model Module"
Cohesion: 0.22
Nodes (3): PortalHomeOverviewViewModel, PortalTreatmentPlanItemViewModel, PortalUpcomingAppointmentItemViewModel

### Community 212 - "User View Model Module"
Cohesion: 0.22
Nodes (8): UserViewModel, ClinicId, Email, FullName, Id, Locale, Roles, Theme

### Community 213 - "Core.Infrastructure Module"
Cohesion: 0.25
Nodes (7): Dapper (2.1.89), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.14), Microsoft.EntityFrameworkCore.SqlServer (10.0.14), System.IdentityModel.Tokens.Jwt (8.6.0), net10.0, ClosedXML (0.105.1), Microsoft.NET.Sdk

### Community 214 - "Sent Email Module"
Cohesion: 0.25
Nodes (5): SentEmail, Body, SentAtUtc, Subject, To

### Community 215 - "Queue Entry Status Module"
Cohesion: 0.25
Nodes (7): QueueEntryStatus, InSession, Late, NoShow, Removed, Served, Waiting

### Community 216 - "Update Clinic Room Module"
Cohesion: 0.25
Nodes (7): UpdateClinicRoomDto, Code, FloorOrLocation, IsActive, NameAr, NameEn, SpecialtyCode

### Community 217 - "Update Clinic Scheduling Settings Module"
Cohesion: 0.25
Nodes (7): UpdateClinicSchedulingSettingsDto, DefaultSlotStepMinutes, LateGraceMinutes, MaxQueuePushes, MinBookingLeadMinutes, RescheduleOptionsJson, VipJumpLimit

### Community 221 - "Add Visit Procedure Module"
Cohesion: 0.29
Nodes (6): AddVisitProcedureDto, NameAr, NameEn, Notes, ProcedureCode, ToothOrSite

### Community 222 - "Adjust Stock Module"
Cohesion: 0.29
Nodes (6): AdjustStockDto, BatchId, ItemId, OperationId, QuantityDelta, Reason

### Community 223 - "Close Day Module"
Cohesion: 0.29
Nodes (6): CloseDayDto, DoctorId, EndDate, ReasonAr, ReasonEn, StartDate

### Community 224 - "Permission Override Item Module"
Cohesion: 0.29
Nodes (5): PermissionOverrideItemDto, Effect, PermissionKey, SetUserPermissionOverridesDto, Overrides

### Community 225 - "tsconfig Module"
Cohesion: 0.29
Nodes (6): compilerOptions, composite, noEmit, extends, include, ../../tsconfig.base.json

### Community 226 - "tsconfig Module"
Cohesion: 0.29
Nodes (6): compilerOptions, composite, noEmit, extends, include, ../../tsconfig.base.json

### Community 227 - "tsconfig Module"
Cohesion: 0.29
Nodes (6): compilerOptions, composite, noEmit, extends, include, ../../../tsconfig.base.json

### Community 228 - "IAuditable Module"
Cohesion: 0.33
Nodes (5): IAuditable, CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy

### Community 230 - "Reports Dapper Queries Module"
Cohesion: 0.33
Nodes (6): IncomeExpenseDapperRow, GrossPayments, OperatingExpenses, Refunds, SupplierAccruals, SupplierPayments

### Community 231 - "Create Clinic Role Module"
Cohesion: 0.33
Nodes (5): CreateClinicRoleDto, Name, NameAr, NameEn, Permissions

### Community 232 - "Create Lab Order Module"
Cohesion: 0.33
Nodes (5): CreateLabOrderDto, Instructions, LabName, TestOrWorkName, ToothOrSite

### Community 233 - "Create Specialty Form Module"
Cohesion: 0.33
Nodes (5): CreateSpecialtyFormDto, JsonSchema, SpecialtyCode, TitleAr, TitleEn

### Community 234 - "Enqueue Walk In Module"
Cohesion: 0.33
Nodes (5): EnqueueWalkInDto, DoctorId, OperationId, PatientId, Priority

### Community 235 - "Update Clinic Time Engine Module"
Cohesion: 0.33
Nodes (5): UpdateClinicTimeEngineSettingDto, MaxStepChangePercent, MinSampleSize, OperationId, TrimFraction

### Community 242 - "Treatment Plan Status Module"
Cohesion: 0.40
Nodes (4): TreatmentPlanStatus, Active, Cancelled, Completed

### Community 243 - "Invite Staff Member Module"
Cohesion: 0.40
Nodes (4): InviteStaffMemberDto, Email, FullName, RoleName

### Community 244 - "Set Session Duration Excused Module"
Cohesion: 0.40
Nodes (4): SetSessionDurationExcusedDto, ExcuseReason, IsExcused, OperationId

### Community 245 - "Update Clinic Inventory Setting Module"
Cohesion: 0.40
Nodes (4): UpdateClinicInventorySettingDto, NearExpiryWarningDays, NegativeStockPolicy, OperationId

### Community 249 - "Core.Domain Module"
Cohesion: 0.50
Nodes (3): Microsoft.Extensions.Identity.Stores (10.0.14), net10.0, Microsoft.NET.Sdk

### Community 251 - "Reports Dapper Queries Module"
Cohesion: 0.50
Nodes (4): MethodBucketDapperRow, Bucket, MethodName, TotalAmount

### Community 252 - "Deactivate Owner Module"
Cohesion: 0.50
Nodes (3): DeactivateOwnerDto, Reason, StepUpPassword

### Community 253 - "Save Visit Form Data Module"
Cohesion: 0.50
Nodes (3): SaveVisitFormDataDto, AnswersJson, ClinicalFormId

### Community 254 - "Set Doctor Time Engine Module"
Cohesion: 0.50
Nodes (3): SetDoctorTimeEngineDto, IsEngineEnabled, OperationId

### Community 255 - "Set Patient Online Booking Module"
Cohesion: 0.50
Nodes (3): SetPatientOnlineBookingBlockDto, Blocked, Reason

### Community 256 - "Update Doctor Booking Setting Module"
Cohesion: 0.50
Nodes (3): UpdateDoctorBookingSettingDto, AcceptsOnlineBooking, MaxDailyOnlineBookings

### Community 257 - "Update Owner Quota Module"
Cohesion: 0.50
Nodes (3): UpdateOwnerQuotaDto, MaxClinics, StepUpPassword

### Community 258 - "Invite User View Model Module"
Cohesion: 0.50
Nodes (3): InviteUserViewModel, Message, UserId

## Knowledge Gaps
- **2117 isolated node(s):** `ClinicId`, `HasTenant`, `IsPlatformAdmin`, `ClinicId`, `HasTenant` (+2112 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 2503 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **30 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CoreDbContext` connect `CoreDbContext Schema Mappings` to `Core Application DTOs`, `API DTOs & Connections`, `Auth & Email Test Fixtures`, `Module Catalog & Identity Roles`, `Tenant Scoping & Patient Privacy`, `Portal Session & HMAC Codes`, `Scheduling & Day Close Core`, `Clinic Context & DI Setup`, `Finance & Payroll Infrastructure`, `Platform Access & RLS Interceptor`, `Super Admin 1h TOTP Security`, `Sync Devices & Time Tracking`, `ASP.NET Identity Entity Config`, `Phase Gates & Verification Codes`, `Clinical Sessions & Odontogram`, `Fixed Costs & Financial Ops`, `Patient Portal Account Flows`, `Inventory & FEFO Stock Control`, `Phase11Gate Module`, `Clinic Role Module`, `Lab Work Order Module`, `Charge Module`, `Refresh Token Module`, `Inventory Item Batch Module`, `Clinic Time Engine Setting Module`, `Clinic Module`, `Patient Module`, `Visit Module`, `Sync Conflict Module`, `Price List Item Module`, `Category Time Stat Module`, `Appointment Module`, `Patient Attachment Module`, `Patient Allergy Module`, `Treatment Plan Module`, `Employee Module`, `Inventory Item Module`, `Clinic Management Module`, `App Device Module`, `Slot Offer Module`, `Treatment Plan Module`, `Clinic Portal Booking Setting Module`, `Closing Method Total Module`, `Plan Session Module`, `Stock Movement Module`, `Patient Access Log Module`, `Payroll Run Module`, `Plan Payment Schedule Item Module`, `Clinic Inventory Setting Module`, `Daily Closing Module`, `Ledger Entry Module`, `Session Duration Module`, `Appointment Category Module`, `Inventory Alert Module`, `Sync Operation Log Module`, `Sync Module`, `Clinic Sync Setting Module`, `Payroll Adjustment Module`, `Purchase Invoice Module`, `Purchase Invoice Line Module`, `Clinic Doctor Module`, `Expense Module`, `Payroll Line Module`, `Reports Dapper Queries Module`, `Refund Module`, `Clinic Scheduling Setting Module`, `Waiting List Entry Module`, `Excel Import Module`, `Charge Line Module`, `Clinic Portal Setting Module`, `Clinic Room Module`, `Lab Order Module`, `Payment Module`, `Scheduled Report Config Module`, `Sync Device State Module`, `Visit Vital Module`, `Clinic Asset Module`, `Clinic Specialty Module`, `Clinic Working Hour Module`, `Clinic Day Off Module`, `Doctor Booking Setting Module`, `Number Block Module`, `Supplier Module`, `Tooth Record Module`, `Audit Log Module`, `Clinical Form Module`, `Import Batch Log Module`, `Prescription Item Module`, `Owner Module`, `Prescription Module`, `Report Export Log Module`, `Supplier Payment Module`, `Visit Diagnosis Module`, `Outbox Message Module`, `Skipped Receipt Number Module`, `Clinic Module Module`, `User Permission Override Module`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **What connects `ClinicId`, `HasTenant`, `IsPlatformAdmin` to the rest of the system?**
  _2117 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Core Domain & Value Objects` be split into smaller, more focused modules?**
  _Cohesion score 0.0120468851747341 - nodes in this community are weakly interconnected._
- **Why does `Core.Domain` connect `Core Domain & Value Objects` to `API DTOs & Connections`, `Platform API Project Layer`, `Portal Session & HMAC Codes`, `Scheduling & Day Close Core`, `Architecture & BuildingBlocks Tests`, `Platform Access & RLS Interceptor`, `Treatment Plan Calculator Module`, `Sync Devices & Time Tracking`, `Super Admin 1h TOTP Security`, `IExcel Import Module`, `Fixed Costs & Financial Ops`, `Patient Portal Account Flows`, `Arabic Normalizer Module`, `Inventory Fefo Allocator Module`, `Refresh Token Module`, `Audit Log Module`, `Visit Module`, `Sync Conflict Module`, `Price List Item Module`, `Owner Module`, `Category Time Stat Module`, `Billing View Model Mapper Module`, `Appointment Module`, `Upsert Employee Module`, `Employee Module`, `Plan Session Status Module`, `Sync Operation Status Module`, `App Device Module`, `Tooth Condition Module`, `Slot Offer Module`, `Clinic Portal Booking Setting Module`, `Create Payroll Run Module`, `Supplier Type Module`, `Sync Calculator Module`, `Closing Method Total Module`, `Clinical Domain Module`, `Stock Movement Module`, `Patient Access Log Module`, `Billing Domain Module`, `Treatment Plan Mode Module`, `Payroll Run Status Module`, `Plan Installment Status Module`, `Clinic Inventory Setting Module`, `Queue Entry Status Module`, `Report Export Kind Module`, `Ledger Entry Module`, `Inventory Alert Module`, `Clinical Form Schema Validator Module`, `Reports Pdf Generator Module`, `Receive Purchase Invoice Module`, `Finance Reports View Model Module`, `Record Payroll Adjustment Module`, `Treatment Plan Status Module`, `Logo Image Processor Module`, `Update Clinic Inventory Setting Module`, `Import Row Error View Module`?**
  _High betweenness centrality (0.049) - this node is a cross-community bridge._
- **Should `Core Application DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.02752383439989552 - nodes in this community are weakly interconnected._
- **Why does `Core.Application` connect `Core Domain & Value Objects` to `Core Application DTOs`, `Allocate Number Block Module`, `Auth & Email Test Fixtures`, `Module Catalog & Identity Roles`, `Platform API Project Layer`, `Portal Session & HMAC Codes`, `Clinic Context & DI Setup`, `Finance & Payroll Infrastructure`, `Architecture & BuildingBlocks Tests`, `Platform Access & RLS Interceptor`, `Super Admin 1h TOTP Security`, `Sync Devices & Time Tracking`, `Clinical Sessions & Odontogram`, `Profile Module`, `Patient Portal Account Flows`, `Inventory & FEFO Stock Control`, `Patient Portal View Model Module`, `Lab Work Order Module`, `Refresh Token Module`, `Inventory Item Batch Module`, `Clinic Time Engine Setting Module`, `Billing View Model Mapper Module`, `Clinical View Model Mapper Module`, `Scheduling View Model Mapper Module`, `Treatment Plan Module`, `Reports Dapper Queries Module`, `ISpecialty Catalog Module`, `Clinic Management Module`, `Plan Suggested Slot Module`, `Create Payroll Run Module`, `Sync View Model Mapper Module`, `Patient Access Log Module`, `Treatment Plan Mode Module`, `Platform Clinic Report Row Module`, `Plan Installment Status Module`, `Clinic Inventory Setting Module`, `Inventory Module`, `Treatment Plan Module`, `Patient View Model Mapper Module`, `Sync Module`, `Clinic Sync Setting Module`, `Billing Receipt Pdf Generator Module`, `Patient Portal View Model Module`, `Inventory View Model Mapper Module`, `Patient Portal View Model Module`, `Doctor Financial Report Row Module`, `ISpecialty Plugin Module`, `Portal Access Log Item Module`, `Session Category Report Row Module`, `Tooth Record Module`, `Finance Reports View Model Module`, `Clinic Schedule Hub Module`, `Slug Check Module`, `Excel Import Module`, `IClinic Management Module`?**
  _High betweenness centrality (0.037) - this node is a cross-community bridge._
- **Should `API DTOs & Connections` be split into smaller, more focused modules?**
  _Cohesion score 0.022064288368636196 - nodes in this community are weakly interconnected._