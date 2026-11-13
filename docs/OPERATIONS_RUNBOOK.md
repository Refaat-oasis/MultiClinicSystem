# Multi-Clinic Platform — Operations & Production Runbook

This runbook defines mandatory production security, backup, disaster-recovery, monitoring, and deployment procedures for the Multi-Clinic Platform (`Platform.Api` + SQL Server + Object Storage + React PWA).

---

## 1. Environment Configuration (Zero Hardcoded Secrets)

Store all secrets outside source control (Azure Key Vault, AWS Secrets Manager, or encrypted orchestrator secrets). Never commit `appsettings.Production.json` or `.env`.

| Variable | Purpose | Notes |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Managed SQL Server connection string | Enforce TLS (`Encrypt=True;TrustServerCertificate=False`), least-privilege app login (no `db_owner` for app runtime). |
| `Jwt__SigningKey` | Asymmetric or >= 256-bit secret signing key | Rotate every 90 days cleanly alongside `SecurityStamp` invalidation. |
| `VerificationCode__HmacKey` | 256-bit HMAC key for 6-digit verification codes | Distinct from JWT signing key. |
| `FileStorage__Provider` | Blob/S3 object storage backend (`Local` in dev) | Enable bucket versioning and server-side encryption (SSE-KMS). |
| `Hosts__ClinicHost` | Canonical clinic staff & portal hostname | Serves clinic PWA and `/api/v1/c/{slug}/portal/**`. |
| `Hosts__AdminHost` | Isolated Super Admin hostname | Required for `/platform/**`; all other hosts return `404`. |
| `Admin__IpAllowlist` | Optional CIDR allowlist for `AdminHost` | Strongly recommended in production behind reverse proxy. |
| `Email__SmtpHost` / `Email__ApiKey` | Transactional email relay (SPF + DKIM verified) | Account verification codes & security alerts only. |

---

## 2. Database Backup & Point-in-Time Restore (PITR) Drill

1. **Automated Schedule (SQL Server Managed Instance / RDS / Azure SQL)**:
   - Full backup: Daily at `02:00 UTC`.
   - Differential backup: Every 6 hours.
   - Transaction log backup: Every 5–10 minutes (supports 35-day Point-in-Time Restore).
2. **Object Storage (`IFileStorage`) Backup**:
   - Continuous cross-region replication + object versioning enabled on attachments, clinic logos, and scheduled exports.
3. **Monthly Restore Drill Checklist**:
   - [ ] Restore database to an isolated staging instance from timestamp `T - 15 minutes` (PITR).
   - [ ] Verify SQL Server Row-Level Security policies are enabled (`SELECT name, is_enabled FROM sys.security_policies`).
   - [ ] Verify append-only ledger invariants (`SUM(Debit)` vs `SUM(Credit)` / closing reconciliation) and `NumberBlock` non-overlap.
   - [ ] Restore matching object storage snapshot and verify SHA-256 hashes of sample `PatientAttachments` resolve via signed download URLs.
   - [ ] Run `dotnet test tests/Gates/Gates.csproj` smoke suite against the restored staging clone.
   - [ ] Record RTO (Recovery Time Objective <= 30 min) and RPO (<= 5 min) in the ops log.

---

## 3. Migration & Deployment Rollback Plan

1. Every release runs EF Core idempotent migration scripts in CI/CD **before** draining/swapping application pods.
2. Backward-compatible expand-and-contract schema rule:
   - Never drop/rename columns or tables in the same release as application code depending on old names.
3. Rollback steps:
   - Revert container image tag / App Service slot swap to `N - 1`.
   - Restore forward-only compensating down-migration script or trigger PITR if data corruption occurred.
   - Verify `/api/v1/health` returns `200 OK` and security headers middleware is active (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Strict-Transport-Security`).

---

## 4. Structured Logging, Metrics & Alert Thresholds

- **Serilog**: Structured JSON logs shipped to central sink (Seq / OpenTelemetry / Elastic), with mandatory `X-Request-Id`, `ClinicId`, and zero PII (no patient names, phone numbers, emails, verification codes, or clinical text).
- **Business Audit Log**: Immutable `core.AuditLogs` and `core.PatientAccessLogs` remain separate from technical logs inside SQL Server.
- **Alert Thresholds**:
  - `5xx` error rate > 1% over 5 minutes -> Page on-call engineer.
  - Auth brute-force lockout spikes or `ADMIN_TOTP_REPLAY` / `DEVICE_REVOKED` bursts -> Security alert.
  - Hangfire dead-letter outbox messages > 0 for 5 minutes -> Ops alert.
  - Dashboard widget endpoint P95 > 500 ms or reports Dapper query > 2000 ms -> Performance investigation.
  - Offline sync `LARGE_CLOCK_SKEW` frequency > 5/hr for a clinic -> Support outreach.

---

## 5. Security Hardening Verification (OWASP ASVS Summary)

- Security headers enforced via `SecurityHeadersMiddleware` (`nosniff`, `DENY`, `no-referrer`, `Permissions-Policy`, strict `CSP`, `HSTS` outside Development, `Cache-Control: no-store` on `/api/**` and `/c/**`).
- Three isolated JWT/cookie realms (`Staff`, `PatientPortal`, `PlatformAdmin` with mandatory 1-hour TOTP + recovery codes + step-up password).
- SQL Server Row-Level Security (`FILTER` + `BLOCK` predicates) + EF Core global query filters + cross-clinic `404` policy enforced on every tenant table and route.
- Full owner clinic data export available on-demand via `GET /api/v1/clinics/{clinicId}/export` (`clinic.export`, owner-only, audited, zero secrets).

