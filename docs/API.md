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
