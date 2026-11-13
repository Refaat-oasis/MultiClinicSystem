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

---

## 6. Modules, Roles & Claims, Dashboards, Owner Portfolio & Clinic Structure

### Platform Module Administration (Admin Host Only — `PlatformAdmin`)
- `GET /api/v1/platform/clinics/{clinicId}/modules`: Lists all 16 switchable modules with `Enabled` state, `Prerequisites`, `MissingPrerequisites`, `EnabledDependents`, timestamps, and notes.
- `PUT /api/v1/platform/clinics/{clinicId}/modules`: Step-up protected toggle endpoint (`UpdateClinicModulesDto`). Validates prerequisite graph (`409 MODULE_PREREQUISITE_OFF`, `409 MODULE_HAS_DEPENDENTS`), emits transactional outbox `ClinicModuleChanged`, and writes `AuditLog`.

### Clinic Read-Only Module Manifest (`/api/v1/clinics/{clinicId}/modules`)
- `GET /api/v1/clinics/{clinicId}/modules`: Returns `MyModulesManifestViewModel` with read-only module flags, active menu items filtered by `(enabled modules ∩ effective permissions)`, settings sections, and effective permissions.
- `PUT /api/v1/clinics/{clinicId}/modules`: Always returns `403 FORBIDDEN_SUPER_ADMIN_ONLY`.

### Roles, Permissions, Staff & Device Approval (`/api/v1/clinics/{clinicId}/...`)
- `GET /api/v1/clinics/{clinicId}/permissions/catalog`: Returns full permission catalog (`PermissionDefinitionViewModel[]`).
- `GET /api/v1/clinics/{clinicId}/permissions/me`: Returns caller's `EffectivePermissions`, `DormantPermissions`, and `EnabledModules`.
- `GET /api/v1/clinics/{clinicId}/roles`: Lists built-in (`Owner`, `Doctor`, `Assistant`, `Receptionist`) and custom clinic roles.
- `POST /api/v1/clinics/{clinicId}/roles`: Creates custom role (enforcing reserved owner permissions & delegation ceiling).
- `POST /api/v1/clinics/{clinicId}/roles/{roleId}/clone`: Clones an existing role while stripping owner-reserved claims.
- `PUT /api/v1/clinics/{clinicId}/roles/{roleId}/permissions`: Updates role permissions (`403 RESERVED_PERMISSION_FORBIDDEN`, `403 DELEGATION_CEILING_EXCEEDED`).
- `DELETE /api/v1/clinics/{clinicId}/roles/{roleId}`: Soft-voids an unused custom role.
- `GET /api/v1/clinics/{clinicId}/staff`: Lists clinic staff with effective/dormant permissions and individual overrides.
- `POST /api/v1/clinics/{clinicId}/staff/invite`: Invites staff under a clinic role with a 6-digit email activation code.
- `PUT /api/v1/clinics/{clinicId}/staff/{staffUserId}/role`: Reassigns staff role (`409 LAST_OWNER_PROTECTED` prevents demoting owner).
- `PUT /api/v1/clinics/{clinicId}/staff/{staffUserId}/overrides`: Sets per-user `Grant` / `Deny` overrides (`Deny` wins; delegation ceiling enforced).
- `DELETE /api/v1/clinics/{clinicId}/staff/{staffUserId}`: Removes staff membership (`409 LAST_OWNER_PROTECTED` prevents removing primary owner).
- `GET /api/v1/clinics/{clinicId}/devices`: Lists devices across clinic staff (`isApproved`, `isRevoked`).
- `POST /api/v1/clinics/{clinicId}/devices/{targetDeviceId}/approve`: Owner/manager approves a non-owner staff device (`DEVICE_APPROVAL_REQUIRED` enforced until approved).

### Dashboards & Multi-Clinic Owner Portfolio
- `GET /api/v1/clinics/{clinicId}/dashboards/widgets`: Returns authorized widgets for enabled modules and user layout.
- `GET /api/v1/clinics/{clinicId}/dashboards/widgets/{widgetKey}?from=...&to=...&scope=...`: Queries aggregate-only widget metrics (`403 MODULE_DISABLED`, `403 FORBIDDEN_PERMISSION`, `400 WIDGET_DATE_RANGE_EXCEEDED` over 366 days).
- `PUT /api/v1/clinics/{clinicId}/dashboards/layout`: Saves user pinned widgets layout.
- `GET /api/v1/portfolio?clinicIds=...&from=...&to=...`: Owner-only aggregate portfolio executed sequentially under each owned clinic's SQL Server RLS tenant context (`404 CLINIC_NOT_FOUND` for any non-owned clinic ID).

### Clinic Structure, Atomic Number Blocks & Owner Audit Viewer (`/api/v1/clinics/{clinicId}/...`)
- `GET/POST/PUT/DELETE /api/v1/clinics/{clinicId}/rooms`: Clinic room management.
- `GET/POST/PUT/DELETE /api/v1/clinics/{clinicId}/doctors`: Clinic doctors, assigned rooms, specialties, and slot duration.
- `GET/PUT /api/v1/clinics/{clinicId}/working-hours`: Clinic & per-doctor weekly schedules.
- `GET/POST/DELETE /api/v1/clinics/{clinicId}/days-off`: Clinic & per-doctor holidays/days off.
- `GET /api/v1/clinics/{clinicId}/number-blocks` & `POST /api/v1/clinics/{clinicId}/number-blocks/reserve`: Concurrency-safe `Receipt` and `PatientFile` number block allocation using SQL Server `sp_getapplock` + `Serializable` transaction isolation.
- `GET /api/v1/clinics/{clinicId}/audit`: Owner/auditor clinic-scoped audit log viewer (`audit.read`).

## 7. Patients, Medical History, Attachments & Scoping (Phase 5)
- `GET /api/v1/clinics/{clinicId}/patients`: Search clinic patients by Arabic-normalized name (`ArabicNormalizer`), phone, or file number; enforces doctor & specialty patient scoping (`404` out-of-scope).
- `GET /api/v1/clinics/{clinicId}/patients/{patientId}`: Patient detail (`patients.read`) including allergies (`patient-history`), notes (filtering `IsDoctorPrivate` to authorized doctors/clinical staff), and attachments (`attachments`); degrades cleanly when optional modules are off.
- `POST /api/v1/clinics/{clinicId}/patients/duplicates/check`: Check duplicate patient warnings by normalized name and phone (`409` or warning list).
- `POST /api/v1/clinics/{clinicId}/patients`: Create patient with `OperationId` idempotency (`ProcessedOperations`), duplicate-warning confirmation flag, and atomic `PatientFile` number-block assignment (`sp_getapplock`).
- `DELETE /api/v1/clinics/{clinicId}/patients/{patientId}`: Soft-void patient (`IsVoided = true`).
- `GET/PUT /api/v1/clinics/{clinicId}/patients/scoping`: Configure per-clinic `ScopePatientsByDoctor` and `ScopePatientsBySpecialty` flags.
- `PUT /api/v1/clinics/{clinicId}/patients/{patientId}/history`: Update chronic medical history (`patient-history`).
- `POST /api/v1/clinics/{clinicId}/patients/{patientId}/allergies` & `DELETE /api/v1/clinics/{clinicId}/patients/{patientId}/allergies/{allergyId}`: Add or soft-void patient allergies (`patient-history`).
- `POST /api/v1/clinics/{clinicId}/patients/{patientId}/notes`: Add patient clinical/shared or doctor-private note.
- `POST /api/v1/clinics/{clinicId}/patients/{patientId}/attachments` & `GET /api/v1/clinics/{clinicId}/patients/{patientId}/attachments/{attachmentId}/download` & `DELETE .../{attachmentId}`: Upload/download/soft-void attachments (`attachments`) behind `IFileStorage` with magic-byte validation, SHA-256 hash, and image thumbnails.
- `GET /api/v1/platform/clinics/{clinicId}/inspect/patients/{patientId}`: Super Admin read-only patient inspect endpoint logging `PatientAccessLog` via `IPlatformAccess`.


## 8. Phase 6: Scheduling, Availability, Real-Time Queue & Waiting List Endpoints
- `GET /api/v1/clinics/{clinicId}/scheduling/settings` & `PUT .../scheduling/settings`: Read/update VIP jump limit, late grace minutes, max queue pushes, default slot step, min booking lead minutes, and reschedule preset options.
- `GET /api/v1/clinics/{clinicId}/scheduling/categories` & `POST .../categories` & `PUT .../categories/{categoryId}`: Manage specialty appointment categories (`DefaultDurationMinutes`, `DefaultPrice`, `BookableOnline`).
- `PUT /api/v1/clinics/{clinicId}/scheduling/doctors/{doctorId}/booking-settings`: Per-doctor online booking opt-out and max daily online bookings cap.
- `GET /api/v1/clinics/{clinicId}/scheduling/free-slots`: Live availability query merging clinic/doctor working hours, days off, active non-cancelled/non-rescheduled appointments, and doctor buffers.
- `GET /api/v1/clinics/{clinicId}/scheduling/appointments` & `GET .../appointments/{appointmentId}` & `POST .../appointments`: List (doctor-scoped when required), read (`404` out-of-scope), and concurrency-safe appointment booking (`OperationId` idempotent, `sp_getapplock` + Serializable double-booking protection, `scheduling.priority` required for `Vip`/`Rush`).
- `POST /api/v1/clinics/{clinicId}/scheduling/appointments/{appointmentId}/status`: Explicit state-machine transition (`Booked -> Confirmed/Arrived/NoShow/Cancelled`, auto-enqueues into `queue` on `Arrived`, triggers `SlotOffer` generation on `Cancelled`).
- `POST /api/v1/clinics/{clinicId}/scheduling/appointments/{appointmentId}/reschedule` & `POST .../scheduling/close-day` & `GET .../scheduling/doctors/{doctorId}/timetable`: Close-day cascade, preset (`after_week`/`after_month`) or custom rescheduling, and daily doctor timetable view.
- `GET /api/v1/clinics/{clinicId}/queue/board` & `POST .../queue/walk-in` & `POST .../queue/entries/{queueEntryId}/push-late` & `POST .../queue/entries/{queueEntryId}/call-next`: Live queue board (`queue` module, degrades gracefully when off), bounded VIP jump insertion, late push-back / `NoShow` threshold, and patient call-next.
- `GET /api/v1/clinics/{clinicId}/queue/waiting-list` & `POST .../queue/waiting-list` & `GET .../queue/slot-offers` & `POST .../queue/slot-offers/{offerId}/accept`: Waiting list enrollment, cancellation-triggered `SlotOffer` listing, and one-click slot offer booking.
- `SignalR Hub /hubs/schedule` (`JoinClinic(clinicId)` / `LeaveClinic(clinicId)`): Tenant-verified real-time broadcasts (`PatientArrived`, `QueueChanged`, `SlotFreed`) isolated to `clinic:{clinicId}` group using zero-PII IDs only.
- `GET /api/v1/platform/clinics/{clinicId}/inspect/schedule`: Super Admin read-only schedule & queue inspect snapshot via `IPlatformAccess`.

## 9. Phase 7: Clinical Visits, Prescriptions, Dental Chart & Specialty Plugins
- `GET /api/v1/clinics/{clinicId}/clinical/icd10?query=...`: Search curated bilingual ICD-10 diagnosis catalog.
- `POST /api/v1/clinics/{clinicId}/clinical/visits`: Start or resume a clinical visit session (`OperationId` idempotent, linked to an appointment or walk-in patient, transitions appointment to `InSession`, enforces server-side elapsed & overtime tracking across reloads).
- `GET /api/v1/clinics/{clinicId}/clinical/visits/{visitId}` & `GET .../patients/{patientId}/visits`: Load full clinical visit read model or patient visit history (doctor-scoped via `404 VISIT_NOT_FOUND`).
- `PUT /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/notes`: Update structured consultation notes (`ChiefComplaint`, `HistoryOfPresentIllness`, `Examination`, `DiagnosisSummary`, `Advice`, `PrivateDoctorNotes`, `FollowUpDate`, `IsVisibleToPatient`).
- `POST /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/vitals`: Record patient vitals and automatically compute `Bmi`.
- `POST /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/diagnoses` & `POST .../procedures`: Record ICD-10 diagnoses and clinical procedures on a visit.
- `POST /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/prescriptions` & `GET .../prescriptions/{prescriptionId}/pdf`: Issue allergy-checked prescriptions (`409 ALLERGY_WARNING_CONFIRMATION_REQUIRED` unless `AcknowledgeAllergyWarning = true` and audited) and generate printable `%PDF-1.4` prescription PDF (`application/pdf`).
- `POST /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/lab-orders` & `POST .../lab-orders/{labOrderId}/result`: Create lab/dental lab orders and record results.
- `GET /api/v1/clinics/{clinicId}/clinical/forms` & `POST .../forms` & `POST .../visits/{visitId}/form-data`: Define clinic-scoped specialty JSON Schema forms (`ClinicalForms`) and validate/persist structured specialty answers (`VisitFormData`).
- `POST /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/dental-chart` & `GET .../patients/{patientId}/dental-chart`: Append permanent or primary tooth state records in FDI or Universal numbering (`DentalNotationConverter`) and query derived current tooth states across visit history (`ToothRecords`).
- `POST /api/v1/clinics/{clinicId}/clinical/visits/{visitId}/complete`: Complete visit, calculate final duration/overtime with optional excused reason, transition linked appointment & queue entry, and enqueue `SessionCompleted` **once** in `OutboxMessages`.
- `GET /api/v1/platform/clinics/{clinicId}/inspect/clinical`: Super Admin read-only clinical visits and specialty forms inspect snapshot via `IPlatformAccess` (`PatientAccessLog`).

## 10. Phase 8: Billing, Ledger, Cash Closing & Suppliers
- `GET /api/v1/clinics/{clinicId}/billing/price-list` & `POST .../billing/price-list`: Read and upsert clinic price-list items (`PriceCode`, Arabic/English names, unit cost, default patient price; online-only mutation).
- `GET /api/v1/clinics/{clinicId}/billing/charges` & `POST .../billing/charges` & `POST .../billing/charges/{chargeId}/post`: List, calculate/create, and post charges (`BillingCalculator` for session fee, free session with required reason, tools/materials cost vs charged price & markup, extra charges with required reason, discount with required reason, and clinic rounding rules); auto-drafted on clinical visit completion (`CompleteVisitAsync`) when `billing` is enabled.
- `POST /api/v1/clinics/{clinicId}/billing/payments` & `GET .../billing/payments/{paymentId}/pdf`: Record single or split multi-method payments (`Cash`, `Visa`, `InstaPay`, `Wallet`) or deposits (`IsDeposit`), allocate sequential clinic receipt numbers from `NumberBlock` (`Kind = "Receipt"`), post negative `LedgerEntry` rows, and render printable `%PDF-1.4` receipt PDFs (`application/pdf`).
- `POST /api/v1/clinics/{clinicId}/billing/skipped-receipts` & `GET .../billing/skipped-receipts`: Record and list damaged/skipped receipt numbers with mandatory reason (`409 RECEIPT_ALREADY_USED` if already issued).
- `POST /api/v1/clinics/{clinicId}/billing/refunds`: Issue online-only append-only partial/full refunds with mandatory reason, writing compensating positive `LedgerEntry` rows without deleting original payments.
- `GET /api/v1/clinics/{clinicId}/billing/patients/{patientId}/ledger`: Query patient's append-only ledger statement (`Charge`, `Payment`, `Refund`) and derived balance due.
- `POST /api/v1/clinics/{clinicId}/billing/lab-work` & `GET .../billing/lab-work`: Record and list dental/medical lab work orders storing clinic `LabCostAmount` separately from patient-facing `PatientPriceAmount` (and posting corresponding `Expense` rows when `finance` is enabled).
- `GET /api/v1/clinics/{clinicId}/billing/suppliers` & `POST .../billing/suppliers` & `POST .../billing/suppliers/payments` & `GET .../billing/expenses` & `POST .../billing/expenses` (`finance` module): Manage clinic suppliers, supplier payments, and operational expenses (`403/404 MODULE_DISABLED` when `finance` is off).
- `GET /api/v1/clinics/{clinicId}/billing/closing` & `POST .../billing/closing/close` & `POST .../billing/closing/reopen`: Preview daily cash reconciliation per payment method (excluding `finance` expenses gracefully with `FinanceExpensesExcluded = true` when `finance` is off), lock the business day (`409 DAY_CLOSED_LOCKED` on subsequent mutations), and audited owner reopen (`finance.reopen_day` / `billing.refund` with mandatory reason).
- `GET /api/v1/platform/clinics/{clinicId}/inspect/billing`: Super Admin read-only billing, charges, and daily closings inspect snapshot via `IPlatformAccess` (`PatientAccessLog`).

## 11. Phase 9: Treatment Plans
- `GET /api/v1/clinics/{clinicId}/treatment-plans` & `GET .../treatment-plans/{planId}`: List and retrieve patient treatment plans with `FixedTotal` or `PerSession` totals, completion percentage, internal staff-only materials cost, session date windows (`TargetDate`, `EarliestAllowedDate`, `LatestAllowedDate`), and installment schedules.
- `POST /api/v1/clinics/{clinicId}/treatment-plans`: Create a multi-session treatment plan (`FixedTotal` with upfront + installment schedule, or `PerSession` with per-session charges), calculating session target windows and validating contract balance (`TreatmentPlanCalculator`).
- `POST /api/v1/clinics/{clinicId}/treatment-plans/{planId}/sessions/{sessionId}/suggest-slot`: Recommend candidate open clinic dates inside `[EarliestAllowedDate, LatestAllowedDate]` (filtering closed weekdays and `ClinicDayOff` ranges) and optionally book an appointment directly through `ISchedulingService`.
- `POST /api/v1/clinics/{clinicId}/treatment-plans/{planId}/sessions/{sessionId}/slip`: Record a delayed/slipped session date with mandatory reason and automatically recalculate target dates and allowed windows for all downstream unfinished sessions.
- `POST /api/v1/clinics/{clinicId}/treatment-plans/{planId}/sessions/{sessionId}/complete`: Mark a plan session completed, automatically create and post the per-session billing `Charge` via `IBillingService` in `PerSession` mode (no duplicate per-session charge in `FixedTotal` mode), and recompute completion percentage.
- `POST /api/v1/clinics/{clinicId}/treatment-plans/{planId}/installments/{installmentId}/pay` & `POST .../installments/{installmentId}/refund`: Collect upfront deposits or scheduled installments (issuing receipt numbers via `IBillingService`) and issue online-only append-only deposit/installment refunds with mandatory reason without deleting prior payments.
- `PUT /api/v1/clinics/{clinicId}/treatment-plans/{planId}/portal-cost-visibility`: Toggle `ShowCostToPatient` (default derived from clinic configuration) controlling whether treatment plan pricing and installments are exposed to `ITreatmentPlanPortalQueries`.
- `GET /api/v1/platform/clinics/{clinicId}/inspect/treatment-plans`: Super Admin read-only treatment-plans inspect snapshot via `IPlatformAccess` (`PatientAccessLog`).

## 12. Phase 10: Patient Portal (Public Clinic Page, Read-Only Record & Staff Management)
All `/api/v1/c/{slug}/portal/**` responses enforce `Cache-Control: no-store`, `Pragma: no-cache`, and `X-Robots-Tag: noindex, nofollow`. Patient endpoints use isolated `__Host-Portal-Token` (`realm = "PatientPortal"`) and never accept a patient ID from client input.

### Public & Patient Portal Endpoints (`/api/v1/c/{slug}/portal/...`)
- `GET /api/v1/c/{slug}/portal/landing`: Anonymous public clinic landing resolving active clinic info, welcome/terms copy, and available module sections; historical slugs set `IsRedirect = true` with `X-Canonical-Slug` & `X-Redirect-Target`, while unknown, suspended, pending-onboarding, or `portal`-disabled clinics return identical `404 CLINIC_NOT_FOUND`.
- `POST /api/v1/c/{slug}/portal/activate`: Activate invited `PatientAccount` using 6-digit HMAC email verification code and set password; issues portal session + `__Host-Portal-Token` cookie.
- `POST /api/v1/c/{slug}/portal/register-pin`: Verify patient file number + date of birth + 10-character single-use printed PIN and send 6-digit email verification code for account completion.
- `POST /api/v1/c/{slug}/portal/login` & `POST .../logout`: Sign in with isolated `PatientPortal` JWT & `__Host-Portal-Token` cookie, or sign out.
- `POST /api/v1/c/{slug}/portal/forgot-password` & `POST .../reset-password`: Enumeration-safe 6-digit password reset (`ResetPassword` rotates `SecurityStamp`, invalidating existing portal tokens).
- `GET /api/v1/c/{slug}/portal/overview` (`portal.patient`): Patient home overview combining demographics/blood type, allergies & shared medical background (`patient-history`), shared attachments (`attachments`), upcoming appointments (`scheduling`), ended visible visits (`clinical`), and treatment plan progress without material costs (`treatment-plans`); logs `PatientAccessLog` (`ActorType = "Patient"`).
- `GET /api/v1/c/{slug}/portal/medical-record` (`portal.patient`): Dedicated medical background (allergies, chronic conditions, medications, surgeries, non-private visible notes, shared attachments).
- `GET /api/v1/c/{slug}/portal/visits` & `GET .../visits/{visitId}` (`portal.patient`): List ended visits marked `VisibleToPatient` and read visit detail (`404 VISIT_NOT_FOUND` across patients or when hidden; never includes `PrivateDoctorNotes`).
- `GET /api/v1/c/{slug}/portal/treatment-plans` (`portal.patient`): Patient treatment plans with session targets/status and patient-facing costs only when `ShowCostToPatient` is true (`materialsCost` omitted).
- `POST /api/v1/c/{slug}/portal/prescriptions/{prescriptionId}/signed-url` & `POST .../attachments/{attachmentId}/signed-url` & `GET .../downloads/{kind}/{resourceId}?token=...`: Issue HMAC-signed 5-minute single-resource download URLs and stream authorized prescription PDFs or shared attachments (`401 INVALID_OR_EXPIRED_DOWNLOAD_TOKEN` after TTL expiry or patient mismatch).

### Staff Portal Management & Super Admin Inspect
- `GET /api/v1/clinics/{clinicId}/portal/patients/{patientId}`: Read patient portal status (`NotInvited`, `Invited`, `Active`, `Disabled`), consent metadata, and last login.
- `POST /api/v1/clinics/{clinicId}/portal/patients/{patientId}/invite` & `POST .../resend` & `PUT .../status`: Invite patient with mandatory documented consent (via email 6-digit code or single-use 10-character printed PIN for no-email/guardian accounts), resend activation code (60s cooldown, hourly cap), and enable/disable portal access (`portal.invite`).
- `PUT /api/v1/clinics/{clinicId}/portal/patients/{patientId}/profile` & `PUT .../notes/{noteId}/visibility` & `PUT .../attachments/{attachmentId}/visibility` & `PUT .../visits/{visitId}/visibility`: Update portal profile fields (email, blood type, guardian details) and control per-item patient visibility (`IsDoctorPrivate` notes are rejected from sharing).
- `GET /api/v1/clinics/{clinicId}/portal/settings` & `PUT .../settings`: Manage `ShowPricesByDefault`, `ShowDoctorNames`, `DefaultVisitVisibility`, bilingual welcome/terms copy (`portal.manage`).
- `GET /api/v1/clinics/{clinicId}/portal/access-logs`: Clinic-scoped patient portal access log audit viewer (`portal.manage`).
- `GET /api/v1/platform/clinics/{clinicId}/inspect/portal`: Super Admin read-only portal account & view summary (`PatientAccessLog` recorded via `IPlatformAccess`).

## 13. Phase 11: Patient Portal — Self-Booking & Cancellation
- `GET /api/v1/c/{slug}/portal/availability?categoryId=&doctorId=&fromUtc=&toUtc=` (`portal.patient`): Retrieve online-bookable categories (including price when `ShowPricesWhenAvailable` & `ShowPricesByDefault` allow), online-bookable doctors, available open slots outside clinic blocks/existing appointments, clinic booking mode (`AutoConfirm` vs `RequiresApproval`), cancellation cutoff hours, active future booking count vs cap, and patient online-booking block status (`403 MODULE_DISABLED` if `portal-booking` is off).
- `POST /api/v1/c/{slug}/portal/appointments` (`portal.patient`, online-only with client `OperationId`): Book a future appointment from the portal (`patientId` comes strictly from the portal JWT/cookie). Enforces patient no-show online block (`403 PATIENT_ONLINE_BOOKING_BLOCKED`), bookable category & doctor opt-in, lead time & booking horizon, per-patient active future cap, clinic & doctor daily portal caps under `Serializable` + `sp_getapplock`, returns `409 SLOT_TAKEN` on collision, and sets status `Booked` (`AutoConfirm`) or `Requested` with `ApprovalExpiresAtUtc` hold (`RequiresApproval`).
- `POST /api/v1/c/{slug}/portal/appointments/{appointmentId}/cancel` (`portal.patient`, online-only): Cancel patient's own `Booked` or `Requested` appointment (`404` across patients or clinics), enforcing `CancellationCutoffHours` (`CANCELLATION_CUTOFF_PASSED`), freeing the slot, and triggering `WaitingList` automatic `SlotOffer` matching.
- `GET /api/v1/clinics/{clinicId}/portal/booking-settings` & `PUT .../booking-settings`: Read and update clinic portal booking policy (`Mode`, `MinLeadTimeMinutes`, `MaxDaysAhead`, `SlotStepMinutes`, `MaxActiveFutureBookingsPerPatient`, `MaxBookingsPerDay`, `CancellationCutoffHours`, `AutoBlockAfterNoShows`, `ApprovalHoldHours`, `ShowPricesWhenAvailable`; `portal.booking.manage`).
- `GET /api/v1/clinics/{clinicId}/portal/booking-requests?status=` & `POST .../booking-requests/{appointmentId}/approve` & `POST .../decline` & `POST .../expire-stale`: Reception queue for reviewing `RequiresApproval` requests (`409 REQUEST_EXPIRED` when hold expired) and releasing stale holds (`portal.booking.requests.review`).
- `PUT /api/v1/clinics/{clinicId}/portal/patients/{patientId}/booking-block`: Manually block or unblock a patient's online portal booking (`NoShowCount` auto-block override; `portal.booking.manage`).
- `GET /api/v1/platform/clinics/{clinicId}/inspect/portal-booking`: Super Admin read-only portal booking settings, pending requests, today's portal booking count, and blocked patient count (`PatientAccessLog` via `IPlatformAccess`).

