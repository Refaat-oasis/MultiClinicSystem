# Platform API Documentation

## System Conventions
- All routes are versioned: `/api/v1/...`
- Distributed Tracing: Every request requires or is assigned an `X-Request-Id` (Guid v7).
- Error Envelope: RFC 7807 `ProblemDetails` with stable, machine-readable `code` fields.
- Security Headers: `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`.
- Authentication: HttpOnly, Secure, SameSite=Strict cookies or `Authorization: Bearer <token>`.
- Multi-Tenancy: Dual-layer isolation (EF Core global query filter + SQL Server Row-Level Security).

---

## 1. System Health
### `GET /health`
- **Description:** System health and UTC timestamp check.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Response `200 OK`:**
  ```json
  {
    "status": "Healthy",
    "timestampUtc": "2026-11-09T16:00:00.0000000Z"
  }
  ```

---

## 2. Authentication & Clinic Identity (`/api/v1/auth`)

### `POST /api/v1/auth/invite`
- **Description:** Invites a staff member or owner, creates an unactivated account, and generates a 6-digit verification code sent via transactional email.
- **Access:** Requires `Staff.Invite` permission or System Seed.
- **Request Body (`InviteUserDto`):**
  ```json
  {
    "email": "doctor@clinic.local",
    "fullName": "Dr. Sarah Ahmed",
    "role": "Owner",
    "clinicId": "018d0000-0000-7000-8000-000000000001"
  }
  ```
- **Response `200 OK` (`InviteUserViewModel`):**
  ```json
  {
    "email": "doctor@clinic.local",
    "fullName": "Dr. Sarah Ahmed",
    "role": "Owner",
    "clinicId": "018d0000-0000-7000-8000-000000000001",
    "message": "User invited successfully. Verification code dispatched via email."
  }
  ```

### `POST /api/v1/auth/activate`
- **Description:** Activates an invited account using the 6-digit email code and sets the initial password.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`ActivateAccountDto`):**
  ```json
  {
    "email": "doctor@clinic.local",
    "code": "123456",
    "password": "StrongPassword#2026",
    "deviceId": "web-chrome-client-01",
    "deviceName": "Chrome on Windows"
  }
  ```
- **Response `200 OK` (`AuthResponseViewModel`):**
  ```json
  {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "48-byte-hex-token",
    "expiresIn": 900,
    "user": {
      "id": "018d0000-...",
      "email": "doctor@clinic.local",
      "fullName": "Dr. Sarah Ahmed",
      "role": "Owner",
      "clinicId": "018d0000-..."
    }
  }
  ```

### `POST /api/v1/auth/login`
- **Description:** Authenticates staff using email and password. Enforces device registration and account lockout after repeated failures.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`LoginDto`):**
  ```json
  {
    "email": "doctor@clinic.local",
    "password": "StrongPassword#2026",
    "deviceId": "web-chrome-client-01",
    "deviceName": "Chrome on Windows"
  }
  ```
- **Response `200 OK` (`AuthResponseViewModel`):** Returns access and rotating refresh tokens.

### `POST /api/v1/auth/refresh-token`
- **Description:** Rotates a refresh token family. Automatically revokes the entire token family upon detecting token reuse.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`RefreshTokenDto`):**
  ```json
  {
    "refreshToken": "48-byte-hex-token"
  }
  ```
- **Response `200 OK` (`AuthResponseViewModel`):** Returns fresh access and refresh tokens.

### `POST /api/v1/auth/forgot-password`
- **Description:** Initiates password reset by emailing a 6-digit code. Answers with identical `202 Accepted` whether the email exists or not (anti-enumeration).
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`ForgotPasswordDto`):**
  ```json
  {
    "email": "doctor@clinic.local"
  }
  ```
- **Response `202 Accepted` (`ForgotPasswordViewModel`):**
  ```json
  {
    "message": "If the email is registered, a 6-digit verification code has been dispatched."
  }
  ```

### `POST /api/v1/auth/reset-password`
- **Description:** Resets password using the emailed 6-digit code. Invalidates old sessions, rotates security stamp, and audits the action.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`ResetPasswordDto`):**
  ```json
  {
    "email": "doctor@clinic.local",
    "code": "654321",
    "newPassword": "NewStrongPassword#2026"
  }
  ```
- **Response `200 OK` (`GenericMessageViewModel`):**
  ```json
  {
    "message": "Password reset successfully. Previous sessions and refresh tokens have been revoked."
  }
  ```

### `POST /api/v1/auth/resend-code`
- **Description:** Re-issues a fresh 6-digit code if the cooldown period has elapsed. Invalidates previous codes for the same purpose.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`ResendCodeDto`):**
  ```json
  {
    "email": "doctor@clinic.local",
    "purpose": "Invitation"
  }
  ```
- **Response `200 OK` (`GenericMessageViewModel`).**

### `POST /api/v1/auth/request-email-change`
- **Description:** Requests changing account email. Dispatches a 6-digit verification code to the proposed new email address.
- **Access:** Authorized staff.
- **Request Body (`RequestEmailChangeDto`):**
  ```json
  {
    "newEmail": "doctor.new@clinic.local"
  }
  ```
- **Response `200 OK` (`GenericMessageViewModel`).**

### `POST /api/v1/auth/confirm-email-change`
- **Description:** Confirms email change using the code sent to the new address. Sends a security notification to the old email address.
- **Access:** Authorized staff.
- **Request Body (`ConfirmEmailChangeDto`):**
  ```json
  {
    "newEmail": "doctor.new@clinic.local",
    "code": "334455"
  }
  ```
- **Response `200 OK` (`GenericMessageViewModel`).**

### `POST /api/v1/auth/revoke-device`
- **Description:** Revokes an approved device, immediately terminating its active sessions and rejecting subsequent refresh token operations.
- **Access:** Authorized staff.
- **Request Body (`RevokeDeviceDto`):**
  ```json
  {
    "deviceId": "device-uuid-or-fingerprint"
  }
  ```
- **Response `200 OK` (`GenericMessageViewModel`).**


### `GET /api/v1/auth/access-logs`
- **Description:** Returns `PatientAccessLog` rows (such as Super Admin inspect-mode accesses) for clinics owned by the authenticated clinic owner. Cross-clinic or non-owned clinic queries return `404 NotFound`.
- **Access:** Authorized clinic owner.
- **Query Params:** `clinicId` (optional `Guid`).
- **Response `200 OK`:** Array of `PatientAccessLogItemViewModel` (`id`, `actorId`, `reason`, `timestampUtc`, `clinicId`, `details`).

---

## 3. Platform Super Admin Control Plane (`/api/v1/platform/**`)

> **Host Isolation:** All `/platform/**` and `/api/v1/platform/**` routes are served exclusively on the Admin host (`admin.*` or `X-Host-Type: admin`) and return `404 Not Found` on the clinic host. Auth uses the `__Host-Platform-Token` cookie and `realm=PlatformAdmin` JWT claim.

### `POST /api/v1/platform/auth/login`
- **Description:** Super Admin three-factor sign-in (`email`, `password`, and 6-digit `hourlyCode` with 3600s TOTP period or single-use `recoveryKey`). First login returns `requiresEnrollment: true` with manual setup key and `otpauth://` URI (`period=3600`). Sets `__Host-Platform-Token` HttpOnly Secure SameSite=Strict cookie.
- **Access:** Anonymous (`[AllowAnonymous]`).
- **Request Body (`PlatformAdminLoginDto`):** `{ "email", "password", "hourlyCode?", "recoveryKey?" }`
- **Response `200 OK`:** `PlatformAdminLoginViewModel`

### `POST /api/v1/platform/auth/enroll`
- **Description:** Completes mandatory first-login password change and hourly TOTP enrollment, issuing 10 HMAC-hashed single-use recovery keys.
- **Access:** `PlatformAdminEnrollment` policy (`realm=PlatformAdmin`).
- **Request Body (`PlatformAdminEnrollDto`):** `{ "newPassword", "hourlyCode" }`
- **Response `200 OK`:** `TotpEnrollmentCompletedViewModel`

### `POST /api/v1/platform/auth/step-up`
- **Description:** Verifies Super Admin password to open a 10-minute step-up confirmation window for sensitive actions (owner deactivation, quota edits, clinic ownership transfers).
- **Access:** `PlatformAdmin` policy (`realm=PlatformAdmin`, `totp_enrolled=true`).
- **Request Body (`PlatformStepUpDto`):** `{ "password" }`

### `POST /api/v1/platform/auth/break-glass-reset`
- **Description:** Emergency 2FA reset authenticated by server-side break-glass secret (also available via CLI `platform-admin reset-2fa --email ... --secret ...`). Writes immutable `AuditLog` entry.
- **Access:** Anonymous (`[AllowAnonymous]`), gated by `PlatformAdmin:BreakGlassSecret`.
- **Request Body (`BreakGlassResetDto`):** `{ "email", "breakGlassSecret" }`

### `POST /api/v1/platform/auth/logout`
- **Description:** Clears `__Host-Platform-Token` cookie.
- **Access:** `PlatformAdminEnrollment` policy.

### Owners Management (`/api/v1/platform/owners`) — Requires `PlatformAdmin`
- `POST /api/v1/platform/owners`: Creates & invites an Owner (quota `maxClinics`), dispatching 6-digit activation email via outbox. Returns `201 Created` (`OwnerSummaryViewModel`).
- `GET /api/v1/platform/owners?search=...`: Lists platform owners with clinic counts.
- `GET /api/v1/platform/owners/{ownerId}`: Returns `OwnerDetailViewModel` including assigned clinics.
- `POST /api/v1/platform/owners/{ownerId}/resend-code`: Re-dispatches 6-digit activation email.
- `POST /api/v1/platform/owners/{ownerId}/deactivate`: Step-up protected; suspends owner, blocks login, and revokes active tokens/devices.
- `POST /api/v1/platform/owners/{ownerId}/reactivate`: Restores owner active status.
- `PUT /api/v1/platform/owners/{ownerId}/quota`: Step-up protected; updates owner `MaxClinics` quota.

### Clinics Directory, Lifecycle & Read-Only Inspect (`/api/v1/platform/clinics`) — Requires `PlatformAdmin`
- `GET /api/v1/platform/clinics?search=...`: Cross-clinic directory grouped by owner.
- `PUT /api/v1/platform/clinics/{clinicId}/status`: Updates clinic status (`Active`, `ReadOnly`, `Suspended`, `PendingOnboarding`), subscription expiry, and grace days.
- `POST /api/v1/platform/clinics/{clinicId}/move`: Step-up protected; transfers clinic ownership, revokes old owner `UserClinic` mapping, assigns new owner, and audits.
- `GET /api/v1/platform/clinics/{clinicId}/inspect`: Uses `IPlatformAccess` to inspect clinic summary read-only, writes `PatientAccessLog` (`ActorType:SuperAdmin`), and returns `InspectClinicViewModel`.
- `POST/PUT/DELETE/PATCH /api/v1/platform/clinics/{clinicId}/inspect/{*subpath}`: Explicitly returns `403 Forbidden` (`INSPECT_MODE_READ_ONLY`).

### Monitoring & Audit (`/api/v1/platform`) — Requires `PlatformAdmin`
- `GET /api/v1/platform/dashboard`: Returns `PlatformDashboardViewModel` aggregate metrics, failed login counts, expiring subscriptions, outbox queue count, and recent audit rows.
- `GET /api/v1/platform/audit?clinicId=...&action=...&page=1&pageSize=50`: Paginated platform-wide audit viewer.


---

## 4. Clinic Creation, Switching, Resumable Onboarding, Settings & Public Slugs (`/api/v1/clinics/**`, `/api/v1/c/{slug}`)

- `GET /api/v1/clinics/mine`: Lists clinics owned by or associated with the authenticated user (`MyClinicCardViewModel[]`).
- `POST /api/v1/clinics`: Owner creates a new clinic within optional `MaxClinics` quota (`409 CLINIC_QUOTA_REACHED` if exceeded). Starts as `PendingOnboarding` with 0 enabled modules.
- `POST /api/v1/clinics/{clinicId}/switch`: Issues updated staff JWT & refresh token scoped to the target `clinicId` (`404 CLINIC_NOT_FOUND` on unauthorized clinics, `403 CLINIC_SUSPENDED` if suspended).
- `GET /api/v1/clinics/slug-check?slug=...&clinicId=...`: Validates format, reserved words, and uniqueness (including historical slugs) and returns availability + suggested alternative (`SlugCheckViewModel`).
- `GET /api/v1/clinics/specialties`: Returns available specialty catalog (`SpecialtyCatalogItemViewModel[]`).
- `GET /api/v1/clinics/{clinicId}/onboarding`: Returns resumable onboarding progress (`CompletedSteps`, clinic fields, specialties).
- `POST /api/v1/clinics/{clinicId}/onboarding/welcome`: Marks welcome step seen and completes step 1.
- `PUT /api/v1/clinics/{clinicId}/onboarding/details`: Saves validated address, normalized Egyptian/international phones (`+20...`), email, and logo reference.
- `POST /api/v1/clinics/{clinicId}/onboarding/logo`: Validates SVG rejection (`LOGO_SVG_REJECTED`), size/type limits, strips image metadata chunks (`PNG tEXt/iTXt/eXIf/zTXt`, `JPEG APP1-APP15/COM`), and stores full + thumb versions via `IFileStorage`.
- `PUT /api/v1/clinics/{clinicId}/onboarding/types`: Saves selected clinic specialties (`dental` attaches `HasDentalPlugin: true`).
- `PUT /api/v1/clinics/{clinicId}/onboarding/slug`: Reserves/updates unique public URL slug and maintains redirect history.
- `POST /api/v1/clinics/{clinicId}/onboarding/complete`: Finalizes onboarding, transitions clinic to `Active`, and returns `ClinicDashboardViewModel` (`AwaitingModuleSetup: true` until Super Admin enables modules).
- `GET /api/v1/clinics/{clinicId}/dashboard`: Blocked with `409 ONBOARDING_REQUIRED` until onboarding completes; otherwise returns clinic dashboard summary (`ClinicDashboardViewModel`).
- `GET /api/v1/clinics/{clinicId}/settings`: Blocked with `409 ONBOARDING_REQUIRED` until onboarding completes; returns clinic settings and available `ISettingsSectionRegistry` sections (`ClinicSettingsViewModel`).
- `PUT /api/v1/clinics/{clinicId}/settings`: Owner-only (`403 FORBIDDEN_OWNER_ONLY` for staff); updates general settings, reserves slug redirect history on slug changes, and locks `TimeZone`/`Currency`/`RoundingRule` once `HasFinancialEntries` is true (`409 FINANCIAL_SETTINGS_LOCKED`).
- `GET /api/v1/c/{slug}` (`[AllowAnonymous]`): Resolves current or historical clinic slug to clinic metadata or `301`-semantics redirect payload (`IsRedirect`, `RedirectUrl`, `CurrentSlug`), returning `404` for unknown or suspended slugs.

---

## 5. Self-Service User Profile (`/api/v1/profile`) — Policy `profile.self`

- `GET /api/v1/profile`: Returns `UserProfileViewModel` (user details, preferences, active devices, and clinic memberships).
- `PUT /api/v1/profile`: Updates `FullName`, normalized `PhoneNumber`, `Locale`, `Theme`, and `AvatarAssetId`.
- `POST /api/v1/profile/request-email-change`: Generates 6-digit verification code (`HMAC-SHA256`) and dispatches email to the proposed address.
- `POST /api/v1/profile/confirm-email-change`: Verifies code, updates email, rotates `SecurityStamp`, revokes old refresh tokens, notifies old email address, and returns fresh `TokenResult`.
- `POST /api/v1/profile/change-password`: Validates current password + policy, rotates `SecurityStamp`, optionally signs out other devices (`SignOutOtherDevices`), and returns fresh `TokenResult`.
- `DELETE /api/v1/profile/devices/{deviceId}`: Revokes user device and its active refresh tokens.
