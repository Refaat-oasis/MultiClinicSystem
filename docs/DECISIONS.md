# Architecture & Business Decisions Log (`docs/DECISIONS.md`)

This file records resolved architecture/library choices and defaults applied across the platform per `AGENTS.md` Section 4 & Section 7.

## 1. Locked Technical Choices
- **Architecture Style**: Modular monolith (`Core.Domain`, `Core.Contracts`, `Core.Application`, `Core.Infrastructure`, `Platform.Api`, `BuildingBlocks`) with explicit `Contracts` boundaries enforced via `ArchitectureTests`.
- **Database & Tenancy Isolation**: SQL Server with EF Core global query filters **plus** SQL Server Row-Level Security (`sec.TenantSecurityPolicy` via `TenantSessionInterceptor`) across every `ITenantEntity` table.
- **No MediatR / AutoMapper**: Plain application services/handlers, manual mappers, and transactional outbox (`OutboxService`).
- **Two Separate Sites / Hosts**: Clinic app (`app.clinic.*`, staff + `/c/{slug}/portal`) vs Platform Super Admin (`admin.clinic.*`, `/platform/**`), enforced host-wide by middleware.

## 2. Module & Library License Decisions
- **PDF Generation (Phases 7, 8, 14)**: Rather than taking a dependency on QuestPDF (which requires commercial licensing above \$1M USD annual revenue), the platform uses deterministic, self-contained, zero-license-restriction PDF 1.4 generators (`ClinicalPdfGenerator`, `BillingReceiptPdfGenerator`, and `ReportsPdfGenerator` in Phase 14).
- **Excel Generation & Import (Phase 14)**: Uses `ClosedXML` (MIT license) and `Dapper` (Apache-2.0 license) for `.xlsx` export, downloadable import templates, and row-by-row dry-run/commit spreadsheet imports alongside parameterized Dapper reporting queries.

## 3. Applied Defaults for Plan Open Items (Section 6)
- **Hourly Super Admin TOTP Period**: `3600` seconds (RFC 6238 HMAC-SHA1, replay cache + single-use hashed recovery codes + step-up password required for sensitive platform mutations).
- **Emailed Verification Codes**: 6 digits, HMAC-SHA256 bound to `(userId, purpose)` with server key, 5-attempt cap, resend cooldown, hourly cap, short TTL.
- **Receipt & File Number Blocks**: Clinic-scoped atomic number blocks (`NumberBlocks`) — never `MAX()+1`.
- **Negative Stock Policy (Inventory, Phase 12)**: Configurable per clinic (`Block` default, `AllowWithWarning` raises explicit `NegativeStock` alert and records negative balance cleanly).
- **Time Engine Calibration Defaults (Phase 13)**: `MinSampleSize = 5`, `TrimFraction = 0.10m`, `MaxStepChangePercent = 20m`, per-doctor engine default ON with instant per-doctor override reverting Scheduling & Availability to category `DefaultDurationMinutes`.
- **Patient Portal Isolation & Enrollment**: Staff-invited activation with documented consent, patient ID read strictly from cookie claims, `Cache-Control: no-store`, `X-Robots-Tag: noindex`, `PatientAccessLog` on every medical/portal read.

