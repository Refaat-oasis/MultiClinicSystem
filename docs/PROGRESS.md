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
