# Progress Log

## Phase 0: Foundation and Walking Skeleton

### Status: Phase 0 Foundation and Walking Skeleton Complete & Integration Gate GREEN

### Summary of Completed Work

#### 1. Backend Foundation (ASP.NET Core 10 Modular Monolith)
- **Solution & Architecture Setup:**
  - Configured `Platform.sln` and `Directory.Build.props` (strict compilation, nullable reference types, warnings-as-errors, zero NuGet audit blocking).
  - Scaffolded `src/BuildingBlocks`, `src/Modules/Core` (`Domain`, `Application`, `Contracts`, `Infrastructure`), and `src/Platform.Api`.
- **Domain Kernel Primitives (Single Type Per File):**
  - [`GuidV7.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/GuidV7.cs): RFC 9562 time-ordered UUIDv7 generation with big-endian comparison.
  - [`Money.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/Money.cs): `decimal(18,2)` immutable value object with currency validation and rounding.
  - [`IClock.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/IClock.cs) & [`DateTimeProvider.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Infrastructure/DateTimeProvider.cs): UTC time abstraction.
  - [`Result.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/Result.cs), [`ResultT.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/ResultT.cs), and [`Error.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/Error.cs): Functional Result pattern.
  - [`ITenantEntity.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/ITenantEntity.cs): Multi-tenancy interface for global EF Core query filters and SQL Server RLS.
  - [`ICurrentClinic.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Application/ICurrentClinic.cs): Ambient tenant context abstraction.
  - [`IAuditable.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/IAuditable.cs) & [`ISoftDeletable.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Domain/ISoftDeletable.cs): Audit timestamps and soft-voiding tracking.
  - [`IVerificationCodes.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Application/IVerificationCodes.cs) & [`VerificationCodes.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Infrastructure/VerificationCodes.cs): HMAC-SHA256 6-digit verification code generation and fixed-time verification.
- **Multi-Tenancy & SQL Server Row-Level Security (RLS):**
  - [`TenantSessionInterceptor.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Infrastructure/TenantSessionInterceptor.cs): Propagates ambient `ClinicId` and `IsPlatformAdmin` into SQL Server `SESSION_CONTEXT`.
  - [`CoreDbContext.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/CoreDbContext.cs): Enforces schema `"core"`, EF Core Global Query Filter on all `ITenantEntity` records, and provisions `sec.fn_tenantSecurityPredicate` security policies.
  - [`PlatformAccessService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Infrastructure/PlatformAccessService.cs): Governs Platform Admin bypass and audits execution.
- **API & Thin Controllers:**
  - [`RequestIdMiddleware.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Middleware/RequestIdMiddleware.cs): Injects and propagates `X-Request-Id` (Guid v7).
  - [`HostRoutingMiddleware.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Middleware/HostRoutingMiddleware.cs): Enforces Admin host vs Clinic host isolation (returning 404 for mismatched access).
  - [`HealthController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/HealthController.cs): Thin controller returning [`HealthCheckViewModel`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/ViewModels/HealthCheckViewModel.cs) without database queries.

#### 2. Frontend Foundation (React + TypeScript npm Workspaces)
- **Monorepo Layout:**
  - Configured `web/package.json` with npm workspaces (`apps/*`, `packages/*`).
  - [`web/packages/ui`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/ui): Shared UI library with Liquid Glass design system, Arabic RTL primitives, `<GlassCard />`, `<GlassButton />`, `<CodeInput />`, `<ThemeToggle />`, and `<LanguageSwitcher />`.
  - [`web/packages/api-client`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/api-client): Typed fetch client with ProblemDetails handling, automatic `X-Request-Id` generation, and `useOfflineMutation` hook with `onlineOnly` option.
  - [`web/apps/clinic`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/apps/clinic): Staff & portal application with Arabic RTL-first layout, Liquid Glass cards, real-time health query, and 6-digit LTR code input.
  - [`web/apps/admin`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/apps/admin): Super admin platform management portal.

---

### Real Test Execution Results

#### Backend Test Suite
```text
dotnet test tests/UnitTests/UnitTests.csproj
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 163 ms - UnitTests.dll (net10.0)

dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
Passed!  - Failed: 0, Passed:  2, Skipped: 0, Total:  2, Duration: 455 ms - ArchitectureTests.dll (net10.0)

dotnet test tests/IntegrationTests/IntegrationTests.csproj (Real SQL Server)
Passed!  - Failed: 0, Passed:  3, Skipped: 0, Total:  3, Duration: 3 s - IntegrationTests.dll (net10.0)

dotnet test tests/Gates/Gates.csproj (Phase 0 Integration Gate)
Passed!  - Failed: 0, Passed:  3, Skipped: 0, Total:  3, Duration: 1 s - Gates.dll (net10.0)
```

#### Frontend Test Suite
```text
cd web && npm run test
> @clinic/admin-app@0.1.0 test:   2 passed (2)
> @clinic/clinic-app@0.1.0 test:  2 passed (2)
> @clinic/api-client@0.1.0 test:  3 passed (3)
> @clinic/ui@0.1.0 test:          3 passed (3)
Total Frontend Tests: 10 passed, 0 failed
```

#### Frontend Production Build
```text
cd web && npm run build
@clinic/admin-app: built in 7.18s (dist/assets/index-iXXQ-XX0.js: 262.58 kB │ gzip: 81.32 kB)
@clinic/clinic-app: built in 6.24s (dist/assets/index-By9mgF0z.js: 276.41 kB │ gzip: 86.19 kB)
```

---

### Phase 0 Status
- [x] Solution & Scaffolding
- [x] Kernel Primitives (`GuidV7`, `Money`, `IClock`, `Result<T>`)
- [x] Multi-Tenancy EF Core Global Query Filter
- [x] SQL Server Row-Level Security (`SESSION_CONTEXT` + `sec.fn_tenantSecurityPredicate`)
- [x] Platform Admin Bypass & Audit
- [x] Host-based Routing (Admin host `/platform/**` vs Clinic host 404)
- [x] Frontend Workspaces, Arabic RTL, Liquid Glass Design Tokens & Components
- [x] Integration Gate Scenarios All Passed GREEN



---

## Phase 1: Accounts and Clinic-Site Identity

### Status: Phase 1 Accounts & Clinic-Site Identity Complete & Integration Gate GREEN

### Summary of Completed Work

#### 1. Backend Identity & Security (ASP.NET Core 10)
- **Domain Entities & Store Mappings (`Core.Domain` & `Core.Infrastructure`):**
  - Configured ASP.NET Core Identity with custom [`AppUser.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/AppUser.cs) and [`AppRole.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/AppRole.cs) in schema `"core"`.
  - Added [`Owner.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/Owner.cs) (with quota `MaxClinics` and status) and [`UserClinic.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/UserClinic.cs).
  - Added [`VerificationCode.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/VerificationCode.cs): Stores 6-digit verification codes using **HMAC-SHA256 with server-side key** bound to User ID and purpose. Strictly never stores raw codes in plaintext.
  - Added [`AppDevice.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/AppDevice.cs) and [`RefreshToken.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/RefreshToken.cs) supporting family reuse detection.
  - Added [`AuditLog.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/AuditLog.cs) and [`AuditService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/AuditService.cs) to audit login, password reset, invitation, device approval, and permission changes.
- **Application Services & Contracts (Single Type Per File):**
  - [`IAccountInvitations.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Contracts/IAccountInvitations.cs) & [`AccountInvitations.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/AccountInvitations.cs): Passwordless user creation, HMAC-SHA256 code generation, outbox transactional email dispatching.
  - [`IAuthService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Application/IAuthService.cs) & [`AuthService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/AuthService.cs): Activation, authentication, rate-limiting, lockout, password reset, and session revoking.
  - [`ITokenService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Application/ITokenService.cs) & [`TokenService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/TokenService.cs): JWT generation with device claims, cryptographic refresh tokens, and family revocation upon reuse.
  - [`IPasswordPolicy.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Application/IPasswordPolicy.cs) & [`PasswordPolicy.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/PasswordPolicy.cs): Length >= 8, uppercase, digit, special symbol, not equal to email, common passwords check.
- **Thin Controllers & DTO/ViewModel Separation:**
  - [`AuthController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/AuthController.cs): Zero EF Core queries or business logic; acts solely as HTTP transport layer delegating to services.
  - Single-type DTOs: `InviteUserDto`, `ActivateAccountDto`, `LoginDto`, `RefreshTokenDto`, `ForgotPasswordDto`, `ResetPasswordDto`, `ResendCodeDto`, `RequestEmailChangeDto`, `ConfirmEmailChangeDto`, `RevokeDeviceDto`.
  - Single-type ViewModels: `AuthResponseViewModel`, `UserViewModel`, `InviteUserViewModel`, `ForgotPasswordViewModel`, `GenericMessageViewModel`.

#### 2. Frontend Identity & Screens (`web/packages/features/core` & `web/apps/clinic`)
- **Shared Feature Package (`@clinic/feature-core`):**
  - [`useAuthStore.ts`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/useAuthStore.ts): Zustand authentication and user session store.
  - [`LoginScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/LoginScreen.tsx): Arabic RTL login screen with Liquid Glass aesthetics, validation, and navigation links.
  - [`ActivationScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/ActivationScreen.tsx): Account activation using 6-digit LTR `<CodeInput />`, password setup, and resend countdown.
  - [`ForgotPasswordScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/ForgotPasswordScreen.tsx): Consistent anti-enumeration request flow.
  - [`ResetPasswordScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/ResetPasswordScreen.tsx): Code confirmation and password reset.
  - [`DeviceManagementScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/DeviceManagementScreen.tsx): Active devices list and remote session revocation.
- **UI Components (`@clinic/ui`):**
  - [`GlassInput.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/ui/src/GlassInput.tsx): Accessible form input with specular glass styles, label association, and error states.
  - [`AlertBanner.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/ui/src/AlertBanner.tsx): RTL-ready alert banners for error, success, and warning states.
- **Clinic App Integration (`web/apps/clinic`):**
  - Dynamic navigation tabs for all auth flows and real-time dashboard health monitoring in light and dark themes.

---

### Real Test Execution Results (Phase 1)

#### 1. Backend Unit Tests (`dotnet test tests/UnitTests/UnitTests.csproj`)
```text
Passed!  - Failed: 0, Passed: 41, Skipped: 0, Total: 41, Duration: 150 ms - UnitTests.dll (net10.0)
```
*Covers `GuidV7`, `Money`, `IClock`, `VerificationCodes` (HMAC generation, wrong code attempts, dead after 5 tries, expiration), `PasswordPolicy`, `RefreshToken`, and entity validation.*

#### 2. Architecture & Security Sweeps (`dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`)
```text
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 237 ms - ArchitectureTests.dll (net10.0)
```
*Covers Policy Sweep (all endpoints have explicit policy or allow-anonymous), Anonymous-Endpoint Allowlist, Verification-Code HMAC Sweep, Controller DbContext Isolation (Thin Controller rule), and Modular Monolith Layer Dependencies.*

#### 3. Database Integration Tests with Real SQL Server (`dotnet test tests/IntegrationTests/IntegrationTests.csproj`)
```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 13 s - IntegrationTests.dll (net10.0)
```
*Covers `AccountInvitationTests`, `AuthLoginTests`, `PasswordResetTests`, `AuditLogTests`, and `TenantIsolationTests` with SQL Server Row-Level Security.*

#### 4. Cumulative Gates Suite (`dotnet test tests/Gates/Gates.csproj`)
```text
Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 11 s - Gates.dll (net10.0)
```
*Covers both Phase 0 Integration Gate (3/3 tests) and Phase 1 Integration Gate (5/5 tests).*

#### 5. Frontend Tests (`npm --prefix web test`)
```text
> @clinic/admin-app@0.1.0 test:     2 passed (2)
> @clinic/clinic-app@0.1.0 test:    2 passed (2)
> @clinic/api-client@0.1.0 test:    3 passed (3)
> @clinic/ui@0.1.0 test:            3 passed (3)
> @clinic/feature-core@0.1.0 test:  5 passed (5)
Total Frontend Tests: 15 passed, 0 failed
```

#### 6. Frontend Production Bundles (`npm --prefix web run build`)
```text
@clinic/admin-app:  built in 10.21s (dist/assets/index-CR_GWd_5.js: 264.04 kB │ gzip: 81.79 kB)
@clinic/clinic-app: built in 9.82s  (dist/assets/index-BIOp2rK5.js: 306.06 kB │ gzip: 92.15 kB)
Status: 0 Errors, Clean Build
```

---

### Phase 1 Exit Checklist
- [x] Invited accounts receive 6-digit code via transactional email (fake sender verified).
- [x] Account cannot log in before entering the code and setting password.
- [x] Activation with valid code succeeds; code is invalidated upon use.
- [x] Five wrong codes permanently kill the code; resend issues fresh code with cooldown.
- [x] Raw codes are NEVER stored in database (HMAC-SHA256 only), never returned in API, never in URLs.
- [x] Forgot password returns identical 202 Accepted for known and unknown accounts.
- [x] Password reset revokes all refresh token families and active sessions.
- [x] Refresh token reuse is detected and terminates the entire token family.
- [x] Revoked devices are denied access.
- [x] Every audited action creates an immutable `AuditLog` entry.
- [x] Arabic RTL-first UI screens built with Liquid Glass design tokens (Light/Dark themes).
- [x] Phase 0 and Phase 1 integration gates pass 100% green.


---

## Phase 2 — Super Admin Site, Hourly TOTP, Host Isolation & Inspect Mode

**Status:** COMPLETE  
**Date:** 2026-11-13  

### Tasks Completed
- **Super Admin Identity & Hourly TOTP (`PlatformAdmins`, `PlatformAdminRecoveryKeys`, `HourlyTotpService`, `PlatformAdminAuthService`):**
  - Seeded Super Admin (`ahmed@platform.local` / `AdminInitial#2026`) with mandatory password change + 3600-second period HMAC-SHA1 TOTP enrollment (`MustChangePassword`, `TotpEnrolled`), DataProtection secret encryption, replay protection (`LastAcceptedTotpStep`), 5-minute previous-hour grace window (`< 300s`), and 10 single-use HMAC-SHA256 recovery keys.
  - Added step-up password confirmation (`POST /api/v1/platform/auth/step-up`) with short-lived step-up session validation (`STEP_UP_REQUIRED` / `STEP_UP_INVALID`), plus break-glass 2FA reset endpoint (`POST /api/v1/platform/auth/break-glass-reset`) and CLI support.
  - Added host isolation (`HostRoutingMiddleware`) returning 404 for `/platform/**` & `/api/v1/platform/**` on the clinic host, 404 for `/c/**`, `/api/v1/auth/**`, `/api/v1/clinics/**` on the admin host, `__Host-Platform-Token` cookie binding, and cross-realm JWT rejection (`AdminHostSweepTests`).
- **Owner Management, Clinic Directory, Moves & Read-Only Inspect Mode (`PlatformAdminService`, `PlatformOwnersController`, `PlatformClinicsController`, `PlatformMonitoringController`):**
  - Owner creation with clinic quota (`POST /api/v1/platform/owners`), outbox invitation email trigger, resend verification code (`POST /api/v1/platform/owners/{ownerId}/resend-code`), deactivate/reactivate (`POST .../deactivate`, `POST .../reactivate` blocking deactivated logins), and quota updates (`PUT .../quota`).
  - Clinic directory listing (`GET /api/v1/platform/clinics`), status updates (`PUT .../status`), ownership transfer with step-up (`POST .../move` removing prior owner's `UserClinic` mapping so prior owner receives 404 on `/api/v1/auth/access-logs`), and `IPlatformAccess` read-only inspect mode (`GET .../inspect`, writing `PatientAccessLog` with `ActorType:SuperAdmin`, and returning `403 INSPECT_MODE_READ_ONLY` on `POST`/`PUT`/`DELETE`/`PATCH .../inspect`).
  - Platform dashboard & audit endpoints (`GET /api/v1/platform/dashboard`, `GET /api/v1/platform/audit`) and clinic-owner access log viewer (`GET /api/v1/auth/access-logs`).
- **EF Core / Clock / API Infrastructure Fixes:**
  - Fixed `CoreDbContext` tenant global query filter (`r.ClinicId == _currentClinic.ClinicId`) to avoid EF Core 10 eager `Nullable<Guid>.Value` evaluation when `IsPlatformAdmin` is active.
  - Aligned `JwtSecurityTokenHandler` `NotBefore`/`IssuedAt` and `LifetimeValidator` in `TokenService`, `PlatformAdminAuthService`, and `Program.cs` with `IClock.UtcNow`.
  - Mapped domain error codes to `ProblemDetails` in `ProblemDetailsMapper` and added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `Core.Infrastructure` & `UnitTests`.
- **Frontend Super Admin App (`web/apps/admin`):**
  - Added `AdminLoginScreen.tsx`, `AdminEnrollmentScreen.tsx`, `PlatformDashboardScreen.tsx`, `OwnersManagementScreen.tsx`, `ClinicDirectoryScreen.tsx`, `InspectClinicShell.tsx`, `PlatformAuditScreen.tsx`, updated `App.tsx` with navigation + `QueryClientProvider` (preserving Arabic RTL + English and Light + Dark themes), and added 6 Vitest specs in `App.test.tsx`.
  - Fixed unused `React` import TS6133 lint errors in `web/packages/features/core/src/ActivationScreen.test.tsx` and `LoginScreen.test.tsx`.

### Verification Results (Phase 2 Integration Gate + Cumulative Regression)
- `dotnet test tests/UnitTests/UnitTests.csproj`: `Passed! - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 189 ms`
- `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`: `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 215 ms`
- `dotnet test tests/IntegrationTests/IntegrationTests.csproj`: `Passed! - Failed: 0, Passed: 17, Skipped: 0, Total: 17, Duration: 9 s`
- `dotnet test tests/Gates/Gates.csproj`: `Passed! - Failed: 0, Passed: 15, Skipped: 0, Total: 15, Duration: 12 s` (Phase 0 + Phase 1 + Phase 2 gates all green)
- `npm --prefix web test`: All 5 workspaces passed (`@clinic/admin-app`: 6/6, `@clinic/clinic-app`: 2/2, `@clinic/api-client`: 3/3, `@clinic/ui`: 3/3, `@clinic/feature-core`: 5/5) — Total: 19 passed
- `npm --prefix web run lint`: `tsc --noEmit` passed across all 5 workspaces (0 errors)
- `npm --prefix web run build`: `@clinic/admin-app` built in `3.41s`; `@clinic/clinic-app` built in `3.39s`

### Phase 2 Exit Checklist
- [x] Seeded Super Admin mandatory password change, 3600s TOTP enrollment, replay protection, 5-minute previous-hour grace, and 10 single-use recovery keys implemented and verified.
- [x] Step-up password verification, break-glass reset, owner management + quotas + deactivation, clinic directory + ownership transfer, and read-only inspect mode with `PatientAccessLog` audit trail verified.
- [x] Host isolation (`AdminHostSweepTests`) and cross-clinic 404 enforcement verified.
- [x] Admin React app built and tested with Arabic RTL + English and Light + Dark themes.
- [x] Phase 0, Phase 1, and Phase 2 integration gates pass 100% green.


---

## Phase 3 — Owner Adds Clinics, Onboarding Wizard, Clinic Settings, and User Profile

**Status:** COMPLETE  
**Date:** 2026-11-13  

### Tasks Completed
- **Domain & Storage Primitives (`Core.Domain` & `BuildingBlocks`):**
  - Updated [`Clinic.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/Clinic.cs), [`Owner.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/Owner.cs), and [`AppUser.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/AppUser.cs) with onboarding state machine (`NotStarted`/`InProgress`/`Completed`), general settings, financial lock (`HasFinancialEntries`), `WelcomeSeen`, and `AvatarAssetId`.
  - Added single-type domain entities & helpers: [`OnboardingProgress.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/OnboardingProgress.cs), [`ClinicSlugHistory.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicSlugHistory.cs), [`ClinicSpecialty.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicSpecialty.cs), [`ClinicAsset.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicAsset.cs), [`SlugGenerator.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/SlugGenerator.cs), [`PhoneNormalizer.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/PhoneNormalizer.cs), [`LogoSanitizeResult.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/LogoSanitizeResult.cs), and [`LogoImageProcessor.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/LogoImageProcessor.cs).
  - Added [`IFileStorage.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Application/IFileStorage.cs) & [`LocalFileStorage.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/BuildingBlocks/Infrastructure/LocalFileStorage.cs) keeping logo assets outside SQL.
- **EF Core & SQL Server RLS (`Core.Infrastructure`):**
  - Updated [`CoreDbContext.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/CoreDbContext.cs) with global query filters and SQL Server Row-Level Security policies for `OnboardingProgress`, `ClinicSpecialties`, and `ClinicAssets`.
  - Implemented [`ClinicManagementService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/ClinicManagementService.cs), [`ProfileService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/ProfileService.cs), [`SpecialtyCatalog.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/SpecialtyCatalog.cs), and [`SettingsSectionRegistry.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/SettingsSectionRegistry.cs), executing multi-tenant writes under `ExecuteForClinicAsync`.
- **Thin API Controllers & Strict DTO / ViewModel Separation (`Platform.Api`):**
  - Added [`ClinicsController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/ClinicsController.cs), [`PublicClinicDirectoryController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/PublicClinicDirectoryController.cs), and [`ProfileController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/ProfileController.cs), updated [`HostRoutingMiddleware.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Middleware/HostRoutingMiddleware.cs), [`ProblemDetailsMapper.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/ProblemDetailsMapper.cs), [`Program.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Program.cs), and single-type DTOs/ViewModels.
- **Frontend Clinic Screens (`web/packages/features/core` & `web/apps/clinic`):**
  - Added [`ClinicSwitcher.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/ClinicSwitcher.tsx) (`ClinicSwitcher` + `MyClinicsScreen`), [`OnboardingWizardScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/OnboardingWizardScreen.tsx), [`ClinicSettingsScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/ClinicSettingsScreen.tsx), and [`UserProfileScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/UserProfileScreen.tsx), wired them into [`App.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/apps/clinic/src/App.tsx), and added Vitest coverage in [`Phase3Screens.test.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/Phase3Screens.test.tsx).

### Verification Results (Phase 3 Integration Gate + Cumulative Regression)
- `dotnet test tests/UnitTests/UnitTests.csproj`: `Passed! - Failed: 0, Passed: 78, Skipped: 0, Total: 78, Duration: 314 ms`
- `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`: `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 541 ms`
- `dotnet test tests/IntegrationTests/IntegrationTests.csproj`: `Passed! - Failed: 0, Passed: 21, Skipped: 0, Total: 21, Duration: 11 s`
- `dotnet test tests/Gates/Gates.csproj`: `Passed! - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 13 s` (Phase 0 + Phase 1 + Phase 2 + Phase 3 gates all green)
- `npm --prefix web test`: All 5 workspaces passed (`@clinic/admin-app`: 6/6, `@clinic/clinic-app`: 2/2, `@clinic/api-client`: 3/3, `@clinic/ui`: 3/3, `@clinic/feature-core`: 9/9) — Total: 23 passed
- `npm --prefix web run lint`: `tsc --noEmit` passed across all 5 workspaces (0 errors)
- `npm --prefix web run build`: `@clinic/admin-app` built in `4.08s`; `@clinic/clinic-app` built in `3.77s`

### Phase 3 Exit Checklist
- [x] Owner adds clinics within `MaxClinics` quota; `PendingOnboarding` blocks clinic dashboard/settings (`ONBOARDING_REQUIRED`).
- [x] 5-step resumable onboarding wizard, phone normalization, SVG rejection, image metadata stripping via `IFileStorage`, and specialty plugin flags (`dental` vs non-dental) implemented and verified.
- [x] Unique public slug reservation and permanent redirect history on slug change verified.
- [x] Owner-only clinic settings editing, financial lock (`TimeZone`, `Currency`, `RoundingRule`) after first financial entry, and Super Admin owner/clinic dashboard aggregates verified.
- [x] Self-service user profile updates, 6-digit email change confirmation + old-email security alert, password change, and `SecurityStamp` token invalidation verified.
- [x] Phase 0, Phase 1, Phase 2, and Phase 3 integration gates pass 100% green.


---

## Phase 4 — Modules, Roles & Claims, Dashboards, Owner Portfolio & Clinic Structure

**Status:** COMPLETE  
**Date:** 2026-11-13  

### Tasks Completed
- **Domain Entities & Pure Policy Evaluation (`Core.Domain`, Single Type Per File):**
  - Added [`ClinicModule.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicModule.cs), [`ClinicRole.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicRole.cs), [`ClinicRoleClaim.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicRoleClaim.cs), [`UserPermissionOverride.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/UserPermissionOverride.cs), [`EffectivePermissionsEvaluation.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/EffectivePermissionsEvaluation.cs), [`EffectivePermissionsEvaluator.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/EffectivePermissionsEvaluator.cs), [`UserDashboardLayout.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/UserDashboardLayout.cs), [`ClinicRoom.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicRoom.cs), [`ClinicDoctor.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicDoctor.cs), [`ClinicWorkingHour.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicWorkingHour.cs), [`ClinicDayOff.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/ClinicDayOff.cs), and [`NumberBlock.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/NumberBlock.cs).
  - Updated [`AppDevice.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Domain/AppDevice.cs) and [`AuthService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/AuthService.cs) so non-owner staff logins/activations require owner device approval (`DEVICE_APPROVAL_REQUIRED`).
- **EF Core Mappings, SQL Server RLS & Core Services (`Core.Infrastructure`):**
  - Updated [`CoreDbContext.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/CoreDbContext.cs) with 10 new tenant entities (`ClinicModules`, `ClinicRoles`, `ClinicRolePermissions`, `UserPermissionOverrides`, `UserDashboardLayouts`, `ClinicRooms`, `ClinicDoctors`, `ClinicWorkingHours`, `ClinicDaysOff`, `NumberBlocks`), global query filters, and SQL Server Row-Level Security filter + block predicates on all 10 tables.
  - Added [`ModuleCatalogService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/ModuleCatalogService.cs) with all 16 switchable modules + `core`, dependency graph validation (`MODULE_PREREQUISITE_OFF`, `MODULE_HAS_DEPENDENTS`), permission catalog, menu items, settings sections, and widget declarations.
  - Added [`ClinicAuthorizationService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/ClinicAuthorizationService.cs) (built-in role seeding, role cloning, reserved owner permission guard, delegation ceiling check, per-user Grant/Deny overrides where Deny wins, dormant permission preservation across module toggles, last-owner protection `LAST_OWNER_PROTECTED`, staff invitations, and device approvals).
  - Added [`DashboardService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/DashboardService.cs) & [`OwnerPortfolioService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/OwnerPortfolioService.cs) (bounded aggregate-only widgets, 366-day range cap, per-clinic RLS portfolio queries returning 404 on unowned clinics).
  - Added [`ClinicStructureService.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Modules/Core/Core.Infrastructure/ClinicStructureService.cs) (rooms, doctors, weekly working hours, days off, owner clinic audit viewer, and concurrency-safe `ReserveNumberBlockAsync` using `sp_getapplock` + `Serializable` transaction isolation).
- **Thin API Controllers & Strict DTO / ViewModel Separation (`Platform.Api`):**
  - Added [`ModulesController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/ModulesController.cs), [`RolesPermissionsController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/RolesPermissionsController.cs), [`DashboardsController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/DashboardsController.cs), and [`ClinicStructureController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/ClinicStructureController.cs); updated [`PlatformClinicsController.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Controllers/PlatformClinicsController.cs), [`HostRoutingMiddleware.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/Middleware/HostRoutingMiddleware.cs), [`ProblemDetailsMapper.cs`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/src/Platform.Api/ViewModels/ProblemDetailsMapper.cs), and single-type DTOs/ViewModels.
- **Frontend UI & Vitest Coverage (`web/`):**
  - Updated [`ClinicDirectoryScreen.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/apps/admin/src/ClinicDirectoryScreen.tsx) & [`App.test.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/apps/admin/src/App.test.tsx) with per-clinic **Module Toggles & Dependencies** panel and step-up password verification.
  - Added [`Phase4Screens.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/Phase4Screens.tsx) (`MyFeaturesScreen`, `RolesPermissionsScreen`, `StaffDevicesScreen`, `ClinicStructureScreen`, `OwnerPortfolioAndWidgetsScreen`), wired tabs into [`App.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/apps/clinic/src/App.tsx), and added tests in [`Phase4Screens.test.tsx`](file:///c:/Users/ahta6/OneDrive/Desktop/Clinic/web/packages/features/core/src/Phase4Screens.test.tsx).

### Verification Results (Phase 4 Integration Gate + Cumulative Regression)
- `dotnet test tests/UnitTests/UnitTests.csproj --no-restore`: `Passed! - Failed: 0, Passed: 90, Skipped: 0, Total: 90, Duration: 248 ms`
- `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-restore`: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 541 ms` (includes `PermissionCeilingSweepTests` & `DashboardSweepTests`)
- `dotnet test tests/IntegrationTests/IntegrationTests.csproj --no-restore`: `Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 41 s` (real SQL Server + concurrent `NumberBlock` reservation test)
- `dotnet test tests/Gates/Gates.csproj --no-restore`: `Passed! - Failed: 0, Passed: 17, Skipped: 0, Total: 17, Duration: 12 s` (Phase 0 + Phase 1 + Phase 2 + Phase 3 + Phase 4 gates all green)
- `npm --prefix web test`: All 5 workspaces passed (`@clinic/admin-app`: 7/7, `@clinic/clinic-app`: 2/2, `@clinic/api-client`: 3/3, `@clinic/ui`: 3/3, `@clinic/feature-core`: 14/14) — Total: 29 passed
- `npm --prefix web run lint`: `tsc --noEmit` passed across all 5 workspaces (0 errors)
- `npm --prefix web run build`: `@clinic/admin-app` built in `3.28s`; `@clinic/clinic-app` built in `3.01s`

### Phase 4 Exit Checklist
- [x] Super Admin assigns and toggles all 16 clinic modules with dependency validation, step-up password, outbox event, and audit trail; clinic owners cannot alter modules (read-only My Features + 403 on PUT).
- [x] Effective permission formula `(roleClaims ∪ userGrants \ userDenies) ∩ activeModulePermissions`, dormant permission restoration, owner-reserved claim blocking, delegation ceiling, and last-owner protection verified.
- [x] Non-owner staff device approval enforced before clinic operations and lifted immediately upon owner approval.
- [x] Modular dashboard widgets return aggregate-only payloads with 366-day max range and per-widget error-tile isolation; Owner Portfolio runs per clinic under SQL Server RLS and returns 404 for unowned clinics.
- [x] Clinic structure (rooms, doctors, weekly working hours, days off), clinic audit log viewer, and concurrency-safe atomic number blocks (`1..500`, `501..1000`) verified.
- [x] Phase 0, Phase 1, Phase 2, Phase 3, and Phase 4 integration gates and all architecture sweeps pass 100% green.

---

## Phase 5: Patients, Medical History, and Attachments (DONE)
- **Branch**: `phase-5/patients-history-attachments`
- **Completed Tasks**:
  - **Domain (`src/Modules/Core/Core.Domain/`)**: Added `ArabicNormalizer`, `PatientAgeCalculator`, `PatientDuplicateDetector` (+ candidate/match value objects), `Patient`, `PatientAllergy`, `PatientNote`, `PatientAttachment`, and `ProcessedOperation`; extended `Clinic` (`ScopePatientsByDoctor`, `ScopePatientsBySpecialty`, `SetPatientScoping`) and `PatientAccessLog` (`ActorType`).
  - **Application & Infrastructure**: Added `IPatientService` + Phase 5 input/result records, `PatientService`, `CoreDbContext` (`patients` schema + `core.ProcessedOperations`, EF global query filters, SQL Server RLS filter+block predicates), atomic `PatientFile` number-block allocation (`sp_getapplock`), duplicate detection (`409` unless confirmed), doctor/specialty patient scoping (`404` out-of-scope), optional-module degradation (`patient-history`, `attachments`), magic-byte/SHA-256/thumbnail handling behind `IFileStorage`, and `ModuleCatalogService` + `DashboardService` real aggregate widgets. Also tightened deterministic device lookup ordering in `ClinicAuthorizationService`.
  - **API (`src/Platform.Api/`)**: Added `PatientsController` (thin controller + dedicated DTOs/ViewModels + `PatientViewModelMapper`), Super Admin inspect endpoint on `PlatformClinicsController` (`PatientAccessLog`), and `ProblemDetailsMapper` `409 DUPLICATE_*` handling.
  - **Frontend (`web/`)**: Added `Phase5PatientsScreen` + `Phase5PatientsScreen.test.tsx`, wired into `@clinic/feature-core` and `web/apps/clinic/src/App.tsx`.
- **Verification Output Summary**:
  - `dotnet test tests/UnitTests`: `Passed! - Failed: 0, Passed: 102, Skipped: 0, Total: 102`
  - `dotnet test tests/ArchitectureTests`: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12`
  - `dotnet test tests/IntegrationTests`: `Passed! - Failed: 0, Passed: 25, Skipped: 0, Total: 25`
  - `dotnet test tests/Gates` (Phases 0–5 cumulative): `Passed! - Failed: 0, Passed: 18, Skipped: 0, Total: 18`
  - Frontend `npm --prefix web test; lint; build`: `32/32` Vitest tests passed across workspaces (`admin-app`: 7, `clinic-app`: 2, `api-client`: 3, `ui`: 3, `feature-core`: 17), `tsc --noEmit` passed, `vite build` succeeded.

### Phase 5 Exit Checklist
- [x] Patient registry with concurrency-safe `PatientFile` number blocks, `OperationId` idempotency (`ProcessedOperations`), Arabic normalization search, and duplicate-warning enforcement.
- [x] Chronic medical history, allergy tracking (with UI allergy banner), doctor-private notes isolation, and SHA-256 deduplicated attachments behind `IFileStorage` with thumbnails.
- [x] Per-clinic doctor & specialty scoping returning `404`, clean degradation when `patient-history` or `attachments` is off, and Super Admin read-only inspect logging `PatientAccessLog`.
- [x] Phases 0 to 5 integration gates, architecture sweeps, and frontend test/lint/build pass 100% green.


---

## Phase 6: Scheduling, Real-Time Queue, and Waiting List (DONE)
- **Branch**: `phase-6/scheduling-and-queue`
- **Completed Tasks**:
  - **Domain (`src/Modules/Core/Core.Domain/`)**: Added scheduling & queue state machines/enums (`AppointmentStatus`, `AppointmentPriority`, `AppointmentSource`, `QueueEntryStatus`, `SlotOfferStatus`, `AppointmentStateMachine`), entities (`AppointmentCategory`, `DoctorBookingSetting`, `ClinicSchedulingSetting`, `Appointment`, `QueueEntry`, `WaitingListEntry`, `SlotOffer`), pure calculators (`AvailabilityCalculator` + working-hour/day-off/busy-interval/candidate value objects, `QueueReorderCalculator` + entry input/output value objects), and unit tests `SchedulingDomainTests`.
  - **Application & Infrastructure**: Added `ISchedulingService`, `IAvailabilityQueries`, `IPortalBooking`, `IScheduleRealtimeNotifier` + Phase 6 input/result records, `AvailabilityQueriesService`, `ClinicTimeZoneResolver`, `SchedulingProjectionHelper`, `QueueWorkflowHelper`, `SchedulingService` (`sp_getapplock` + `Serializable` double-booking prevention, doctor scoping returning `404`, `scheduling.priority` enforcement for VIP/Rush, VIP jump limit enforcement, late push-back / `NoShow`, close-day cascade & `after_week`/`after_month` reschedule, waiting-list `SlotOffer` generation/acceptance, graceful `queue`-off isolation), and live dashboard widgets (`scheduling.appointments-summary`, `scheduling.utilization`, `queue.waiting-count`, `queue.waiting-list`) in `ModuleCatalogService` + `DashboardService`.
  - **API & Real-Time (`src/Platform.Api/`)**: Added `ClinicScheduleHub` (`/hubs/schedule`, clinic-group access verified via `IClinicAuthorizationService`) + `SignalRScheduleRealtimeNotifier`, `SchedulingController` (thin transport + DTOs/ViewModels + `SchedulingViewModelMapper`), Super Admin inspect endpoint `InspectSchedule` on `PlatformClinicsController`, and SignalR client packages for tests.
  - **Frontend (`web/`)**: Added `Phase6SchedulingScreen` + `Phase6SchedulingScreen.test.tsx`, wired into `web/apps/clinic/src/App.tsx` and read-only inspect view in `web/apps/admin/src/InspectClinicShell.tsx`.
- **Verification Output Summary**:
  - `dotnet test tests/UnitTests/UnitTests.csproj`: `Passed! - Failed: 0, Passed: 120, Skipped: 0, Total: 120`
  - `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12`
  - `dotnet test tests/IntegrationTests/IntegrationTests.csproj`: `Passed! - Failed: 0, Passed: 26, Skipped: 0, Total: 26`
  - `dotnet test tests/Gates/Gates.csproj` (Phases 0–6 cumulative): `Passed! - Failed: 0, Passed: 19, Skipped: 0, Total: 19`
  - Frontend `npm --prefix web test; npm --prefix web run lint; npm --prefix web run build`: `35/35` Vitest tests passed (`admin-app`: 7, `clinic-app`: 2, `api-client`: 3, `ui`: 3, `feature-core`: 20), `tsc --noEmit` 0 errors, Vite production builds succeeded for both apps.

### Phase 6 Exit Checklist
- [x] Availability engine computes free slots merging clinic/doctor hours, days off, active appointments, and buffers, and updates immediately upon concurrent bookings.
- [x] Double-booking prevented under concurrency (`sp_getapplock` + `Serializable`), VIP/Rush priority gated on `scheduling.priority`, and doctor scoping returns `404` across doctors.
- [x] Real-time SignalR `/hubs/schedule` broadcasts zero-PII events (`PatientArrived`, `QueueChanged`, `SlotFreed`) strictly isolated to verified clinic groups.
- [x] Queue push-back + `NoShow` threshold, VIP jump limit, waiting-list cancellation `SlotOffer` generation/acceptance, close-day cascade, and `after_week` reschedule verified end-to-end.
- [x] `queue` module toggle cleanly disables queue/waiting-list endpoints and dashboard widgets without breaking `scheduling`, and Super Admin read-only schedule inspect logs `PatientAccessLog`.
- [x] Phases 0 to 6 integration gates, all architecture sweeps, and frontend test/lint/build pass 100% green.


---

## Phase 7: Visits, Clinical Records, and Specialty Plugins (DONE)
- **Branch**: `phase-7/visits-clinical-specialty-plugins`
- **Completed Tasks**:
  - **Domain (`src/Modules/Core/Core.Domain/`)**: Added `VisitStatus`, `ToothCondition`, `DentalNumberingSystem`, `DerivedToothState`, `ClinicalFormValidationResult`, `DentalNotationConverter` (FDI <-> Universal conversion for permanent & primary teeth + historical chart state derivation), `ClinicalFormSchemaValidator` (JSON Schema structural + required + min/max bounds validator), `Visit` (server-side elapsed & overtime tracking + excused reason), `VisitVital` (automatic BMI calculation), `VisitDiagnosis`, `VisitProcedure`, `Prescription`, `PrescriptionItem`, `LabOrder`, `ClinicalForm`, `VisitFormData`, `ToothRecord`, and unit tests `ClinicalDomainTests`.
  - **Application & Infrastructure**: Added `ISpecialtyPlugin`, `IClinicalPortalQueries`, `IClinicalService` + Phase 7 input/result records, `DentalSpecialtyPlugin`, `OphthalmologySpecialtyPlugin`, `ClinicalPdfGenerator` (`%PDF-1.4` printable prescription generator), `ClinicalProjectionHelper`, `ClinicalService`, `CoreDbContext` (`clinical` schema tables + EF global query filters + SQL Server RLS filter/block predicates), live clinical dashboard widgets (`clinical.sessions-summary`, `clinical.top-diagnoses`, `clinical.avg-duration`, `clinical.overtime-sessions`) in `ModuleCatalogService` + `DashboardService`, and idempotent `SessionCompleted` outbox emission.
  - **API (`src/Platform.Api/`)**: Added `ClinicalController` (thin transport + single-type DTOs/ViewModels + `ClinicalViewModelMapper`), Super Admin read-only `InspectClinical` endpoint on `PlatformClinicsController`, `409 ALLERGY_WARNING_CONFIRMATION_REQUIRED` handling in `ProblemDetailsMapper`, and DI registrations in `Program.cs` & `TestServiceFactory.cs`.
  - **Frontend (`web/`)**: Added `Phase7ClinicalScreen` + `Phase7ClinicalScreen.test.tsx`, wired `'clinical'` tab (`الجلسات السريرية`) into `web/apps/clinic/src/App.tsx` and read-only clinical inspect panel into `web/apps/admin/src/InspectClinicShell.tsx`.
- **Verification Output Summary**:
  - `dotnet test tests/UnitTests/UnitTests.csproj`: `Passed! - Failed: 0, Passed: 125, Skipped: 0, Total: 125`
  - `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12`
  - `dotnet test tests/IntegrationTests/IntegrationTests.csproj`: `Passed! - Failed: 0, Passed: 27, Skipped: 0, Total: 27`
  - `dotnet test tests/Gates/Gates.csproj` (Phases 0–7 cumulative): `Passed! - Failed: 0, Passed: 20, Skipped: 0, Total: 20`
  - Frontend `npm --prefix web test; npm --prefix web run lint; npm --prefix web run build`: `38/38` Vitest tests passed (`admin-app`: 7, `clinic-app`: 2, `api-client`: 3, `ui`: 3, `feature-core`: 23), `tsc --noEmit` 0 errors, Vite production builds succeeded for both apps.

### Phase 7 Exit Checklist
- [x] Server-side visit timer survives reload/resume and accurately tracks elapsed & overtime minutes with optional excused reason.
- [x] Patient allergy warning banner visible, conflicting prescriptions require acknowledged override (`409` otherwise + audited), and printable PDF contains patient file number and prescribed medications.
- [x] Dental specialty plugin supports FDI & Universal notation for permanent & primary teeth with historical `ToothRecords` and derived current chart state, while non-dental specialties (Ophthalmology) validate structured JSON-Schema forms with zero dental rows touched.
- [x] `SessionCompleted` enqueued once in transactional outbox, doctor scoping returns `404`, portal queries exclude hidden visits and omit `PrivateDoctorNotes` while logging `PatientAccessLog`, and `clinical`-off leaves `scheduling` working.
- [x] Phases 0 to 7 integration gates, all architecture sweeps, and frontend test/lint/build pass 100% green.

---

## Phase 8: Billing, Ledger, Cash Closing, and Suppliers (DONE)
- **Branch**: `phase-8/billing-ledger-closing-suppliers`
- **Completed Tasks**:
  - **Domain (`src/Modules/Core/Core.Domain/`)**: Added Phase 8 value objects, enums, calculator (`BillingCalculator`, `ChargeLineCalculation`, `ChargeCalculationResult`, `ClosingMethodSummary`, `PaymentMethod`, `ChargeStatus`, `ChargeLineKind`, `LedgerEntryType`, `RoundingRule`, `DailyClosingStatus`) and 14 single-type entities (`PriceListItem`, `Charge`, `ChargeLine`, `LedgerEntry`, `Payment`, `PaymentAllocation`, `Refund`, `SkippedReceiptNumber`, `LabWorkOrder`, `Supplier`, `SupplierPayment`, `Expense`, `DailyClosing`, `ClosingMethodTotal`) plus 10 unit tests in `BillingDomainTests`.
  - **Application & Infrastructure**: Added `IBillingService` & `IBillingPortalQueries` with 13 inputs & 18 results, `CoreDbContext` mappings (`billing` and `finance` schemas, `decimal(18,2)`, EF global query filters & SQL Server RLS filter/block predicates for all 14 tables), `BillingReceiptPdfGenerator` (`%PDF-1.4` printable receipts), `BillingService` (sequential receipt `NumberBlock` allocation, split payments, skipped receipts, append-only refunds, lab work cost vs patient price, supplier payables, expenses, daily closing reconciliation, closed-day lock, and audited owner reopen), `ClinicalService` visit-completion draft charge hook, and live `billing.*` & `finance.*` dashboard aggregates in `ModuleCatalogService` and `DashboardService`.
  - **API (`src/Platform.Api/`)**: Added `BillingController`, thin DTOs & ViewModels + `BillingViewModelMapper`, Super Admin read-only `InspectBilling` endpoint on `PlatformClinicsController`, `ProblemDetailsMapper` codes (`INSUFFICIENT_PERMISSION`, `DAY_CLOSED_LOCKED`, `DAY_ALREADY_CLOSED`, `DAY_ALREADY_REOPENED`, `RECEIPT_ALREADY_USED`), and DI registrations in `Program.cs` & `TestServiceFactory.cs`.
  - **Frontend (`web/`)**: Added `Phase8BillingScreen` + `Phase8BillingScreen.test.tsx`, wired `'billing'` tab (`الفواتير والخزينة`) into `web/apps/clinic/src/App.tsx` and read-only inspect billing panel into `web/apps/admin/src/InspectClinicShell.tsx`.
- **Verification Output Summary**:
  - `dotnet test tests/UnitTests/UnitTests.csproj`: `Passed! - Failed: 0, Passed: 135, Skipped: 0, Total: 135`
  - `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12`
  - `dotnet test tests/IntegrationTests/IntegrationTests.csproj`: `Passed! - Failed: 0, Passed: 28, Skipped: 0, Total: 28`
  - `dotnet test tests/Gates/Gates.csproj` (Phases 0–8 cumulative): `Passed! - Failed: 0, Passed: 21, Skipped: 0, Total: 21`
  - Frontend `npm --prefix web test; npm --prefix web run lint; npm --prefix web run build`: `41/41` Vitest tests passed (`admin-app`: 7, `clinic-app`: 2, `api-client`: 3, `ui`: 3, `feature-core`: 26), `tsc --noEmit` 0 errors, Vite production builds succeeded for both apps.

### Phase 8 Exit Checklist
- [x] Visit completion auto-creates a Draft charge when `billing` is enabled; calculator separates tool cost vs patient charge/markup, enforces mandatory reasons for free session / extra / discount, and respects rounding rules without doctor commissions.
- [x] Receipt numbers allocate strictly from `NumberBlock` across split payments (`Cash`, `Visa`, `InstaPay`, `Wallet`) and support documented skipped receipts and `%PDF-1.4` printable receipt output.
- [x] Refunds write append-only compensating ledger entries without deleting original payments, and lab work keeps clinic `LabCostAmount` separated from patient `PatientPriceAmount`.
- [x] Daily closing reconciles per payment method (`finance` expenses included when enabled, gracefully excluded with `FinanceExpensesExcluded = true` when `finance` is off), locks closed days (`409 DAY_CLOSED_LOCKED`), and requires owner claim + reason + audit to reopen.
- [x] Phases 0 to 8 cumulative gates, tenant/policy/module-off/portal-privacy/architecture sweeps, and frontend test/lint/build pass 100% green.

---

## Phase 9: Treatment Plans (DONE)
- **Branch**: `phase-9/treatment-plans`
- **Completed Tasks**:
  - **Domain (`src/Modules/Core/Core.Domain/`)**: Added single-type enums, value objects, calculator (`TreatmentPlanMode`, `TreatmentPlanStatus`, `PlanSessionStatus`, `PlanInstallmentStatus`, `PlanSessionWindowCalculation`, `PlanBalanceSummary`, `TreatmentPlanCalculator`), entities (`TreatmentPlan`, `PlanSession`, `PlanPaymentScheduleItem` in `plans` schema implementing `ITenantEntity` and `ISoftDeletable`), `Clinic.PortalShowPlanCostsByDefault`, and 4 unit tests in `TreatmentPlanDomainTests`.
  - **Application & Infrastructure**: Added `ITreatmentPlanService` and `ITreatmentPlanPortalQueries` with 8 inputs and 7 results, `CoreDbContext` mappings (`plans` schema, EF global query filters & SQL Server RLS filter/block predicates for all 3 plan tables), `TreatmentPlanService` (`FixedTotal` upfront + installments, `PerSession` per-session billing `Charge` creation on completion, candidate slot suggestion filtering working hours & days off, automatic downstream session window shift on slip with mandatory reason, installment collection via `IBillingService` receipts, online-only append-only deposit/installment refunds, and `ShowCostToPatient` toggle), and 5 live `treatment-plans.*` dashboard widgets in `ModuleCatalogService` and `DashboardService`.
  - **API (`src/Platform.Api/`)**: Added `TreatmentPlansController`, thin DTOs & ViewModels + `TreatmentPlanViewModelMapper`, Super Admin read-only `InspectTreatmentPlans` endpoint on `PlatformClinicsController`, and DI registrations in `Program.cs` & `TestServiceFactory.cs`.
  - **Frontend (`web/`)**: Added `Phase9TreatmentPlansScreen` + `Phase9TreatmentPlansScreen.test.tsx` (plan builder for `FixedTotal`/`PerSession`, live window shift on slip, session completion, installment payment/refund, portal cost toggle, and `readOnlyInspectMode`), wired `'treatmentPlans'` tab (`خطط العلاج`) into `web/apps/clinic/src/App.tsx` and read-only inspect treatment plans panel into `web/apps/admin/src/InspectClinicShell.tsx`.
- **Verification Output Summary**:
  - `dotnet test tests/UnitTests/UnitTests.csproj`: `Passed! - Failed: 0, Passed: 139, Skipped: 0, Total: 139`
  - `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj`: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12`
  - `dotnet test tests/IntegrationTests/IntegrationTests.csproj`: `Passed! - Failed: 0, Passed: 29, Skipped: 0, Total: 29`
  - `dotnet test tests/Gates/Gates.csproj` (Phases 0–9 cumulative): `Passed! - Failed: 0, Passed: 22, Skipped: 0, Total: 22`
  - Frontend `npm --prefix web test; npm --prefix web run lint; npm --prefix web run build`: `44/44` Vitest tests passed (`admin-app`: 7, `clinic-app`: 2, `api-client`: 3, `ui`: 3, `feature-core`: 29), `tsc --noEmit` 0 errors, Vite production builds succeeded for both apps.

### Phase 9 Exit Checklist
- [x] `FixedTotal` (with upfront + installments) and `PerSession` (creating per-session billing charges on completion) treatment plans store contract balances and staff-only internal material costs.
- [x] Session windows (`TargetDate`, `[EarliestAllowedDate, LatestAllowedDate]`) suggest open dates excluding closed weekdays/days off, and recording a session slip with reason shifts downstream sessions' targets & windows automatically.
- [x] Installment payments allocate real receipt numbers via `IBillingService` and appear in daily closing; deposit/installment refunds are append-only, online-only, and never delete payments.
- [x] Portal cost visibility toggle hides/exposes prices & installments via `ITreatmentPlanPortalQueries`; Super Admin read-only inspect works while writes return 403; turning `treatment-plans` off returns 403/404 and removes widgets without breaking billing or scheduling.
- [x] Phases 0 to 9 cumulative gates, tenant/policy/module-off/architecture sweeps, and frontend test/lint/build pass 100% green.

---

## Phase 10: Patient Portal — Public Clinic Page and Read-Only Record (DONE)
- **Branch**: `phase-10/patient-portal-readonly`
- **Completed Tasks**:
  - **Domain (`src/Modules/Core/Core.Domain/`)**: Extended `Patient` (`Email`, `BloodType`, `GuardianName`, `GuardianEmail`, `SetPortalProfile`), `PatientNote` (`VisibleToPatient`, `SetVisibleToPatient`), `PatientAttachment` (`SharedWithPatient`, `SetSharedWithPatient`), `Visit` (`SetVisibleToPatient`); added single-type `PatientAccountStatus`, `PatientAccount`, `PortalConsent`, `ClinicPortalSetting`, `PortalSignedTokenHelper`, and 4 unit tests in `PatientPortalDomainTests`.
  - **Application & Infrastructure**: Added `IPatientPortalService`, `IPatientPortalQueries`, `ISchedulingPortalQueries`, 6 inputs, and 15 results; implemented `PatientPortalService` (historical slug redirects, identical `404 CLINIC_NOT_FOUND` parity when slug unknown/suspended/pending-onboarding/`portal` off, consent-gated invitations via email 6-digit HMAC codes with cooldown/hourly-cap or single-use 10-character printed PIN + DOB, isolated `PatientPortal` JWT realm with `SecurityStamp` rotation on password reset, modular read sections for `patient-history`/`attachments`/`scheduling`/`clinical`/`treatment-plans`, 5-minute HMAC signed download URLs, `PatientAccessLog` with `ActorType = Patient`, clinic portal settings, and Super Admin read-only inspect), updated `CoreDbContext` (`portal` schema + EF filters + SQL Server RLS filter/block predicates), registered `"portal"` manifest & 5 live `portal.*` dashboard widgets in `ModuleCatalogService` and `DashboardService`.
  - **API & Architecture Sweeps**: Added `PortalSecurityHeadersAttribute` (`Cache-Control: no-store`, `Pragma: no-cache`, `X-Robots-Tag: noindex, nofollow`), `PatientPortalController` (`/api/v1/c/{slug}/portal/**`), `StaffPortalManagementController` (`/api/v1/clinics/{clinicId}/portal/**`), `PlatformClinicsController.InspectPortal`, 10 single-type DTOs, 16 single-type ViewModels + `PatientPortalViewModelMapper`, `__Host-Portal-Token` cookie isolation (`portal.patient` policy) in `Program.cs` & `TestServiceFactory.cs`, and `PortalIsolationSweepTests` + `AnonymousEndpointAllowlistTests` updates.
  - **Frontend (`web/`)**: Added lazy-loaded mobile-first Arabic RTL portal app `web/apps/clinic/src/portal/PortalApp.tsx` + `PortalApp.test.tsx` (wired in `web/apps/clinic/src/main.tsx` for `/c/:slug` routes as a separate Vite chunk), staff management tab `Phase10PortalScreen.tsx` + `Phase10PortalScreen.test.tsx` (wired into `web/apps/clinic/src/App.tsx`), and Super Admin read-only portal inspect in `web/apps/admin/src/InspectClinicShell.tsx`.
- **Verification Output Summary**:
  - `dotnet test tests/UnitTests/UnitTests.csproj --no-restore`: `Passed! - Failed: 0, Passed: 143, Skipped: 0, Total: 143`
  - `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build`: `Passed! - Failed: 0, Passed: 15, Skipped: 0, Total: 15`
  - `dotnet test tests/IntegrationTests/IntegrationTests.csproj --no-build`: `Passed! - Failed: 0, Passed: 30, Skipped: 0, Total: 30`
  - `dotnet test tests/Gates/Gates.csproj --no-build` (Phases 0–10 cumulative): `Passed! - Failed: 0, Passed: 23, Skipped: 0, Total: 23`
  - Frontend `npm --prefix web test; npm --prefix web run lint; npm --prefix web run build`: `48/48` Vitest tests passed (`admin-app`: 7, `clinic-app`: 4, `api-client`: 3, `ui`: 3, `feature-core`: 31), `tsc --noEmit` 0 errors, Vite production builds succeeded for both apps (`PortalApp` emitted as a separate lazy chunk).

### Phase 10 Exit Checklist
- [x] Public clinic landing handles active, historical redirect (`X-Canonical-Slug`/`X-Redirect-Target`), and `404 CLINIC_NOT_FOUND` parity for unknown, suspended, pending-onboarding, and `portal`-off clinics.
- [x] Consent-gated enrollment supports 6-digit HMAC email activation (lockout + resend cooldown + single use) and 10-character printed PIN + DOB registration for minor/no-email patients.
- [x] Portal cookie/JWT realm (`__Host-Portal-Token`) is strictly isolated from staff/admin tokens, responses include `no-store` & `noindex`, signed download URLs expire cleanly, and hidden visits/private doctor notes/internal costs never leak.
- [x] Turning `portal` or optional section modules (`treatment-plans`, etc.) off disables only those portal sections with `MODULE_DISABLED`, while `ReadOnly` clinics preserve read access and `Suspended` returns 404.
- [x] Phases 0 to 10 cumulative gates, portal isolation & architecture sweeps, and frontend test/lint/build pass 100% green.
