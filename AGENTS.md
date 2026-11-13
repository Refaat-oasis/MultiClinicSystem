# AGENTS.md: Instructions for coding agents

You are building a **multi-clinic, multi-specialty clinic management website**: ASP.NET Core 10 API plus a React PWA, Arabic RTL first. The owner of the project is Ahmed (Platform Super Admin).

Read `docs/PLAN.md` first. It defines the phases, rules and tests. This file defines how you must work.

---

## 1. Operating rules

1. **Work one phase at a time, in order.** Never start a phase until the previous Phase Integration Gate is green.
2. **Tests first.** Write the tests from the plan's rules, see them fail, then write the code. Money rules always come first.
3. **Stay in scope.** Build only what the current phase lists. Do not add features, libraries or modules that the plan does not mention.
4. **Do not change locked decisions** (PLAN.md section 1). If you think one is wrong, write the reason in `docs/DECISIONS.md` and ask Ahmed. Do not work around it.
5. **Do not guess business rules.** If a rule is missing or unclear, add it to the open items in `docs/DECISIONS.md`, ask Ahmed one clear question, and continue with other work meanwhile.
6. **Keep a log.** After each task update `docs/PROGRESS.md` (phase, task, status, tests added, what is left).
7. **Branch per task/phase/edit and push.** For every new task, plan, phase, or code adjustment/edit, create a dedicated branch (e.g., `git checkout -b phase-N/<short-name>` or `fix/<short-name>`), commit with message format `phase-N(module): short description`, and push the branch to the remote (`git push -u origin <branch-name>`).
8. **Never claim a test passes without running it.** Show the real command output summary in `docs/PROGRESS.md`.

---

## 2. Golden rules (never break these)

### Tenancy and privacy
- Every tenant table implements `ITenantEntity` (`ClinicId`). The EF global query filter and the SQL Server Row-Level Security policy must cover it. A new table without both is a bug.
- A clinic can see **only its own data and patients**. Cross-clinic access returns **404**, not 403.
- The Super Admin bypass goes only through `IPlatformAccess`. Every bypass read of patient data writes a `PatientAccessLog` row.
- An owner's combined view of his own clinics goes through `IOwnerPortfolio`: it runs each clinic's query under that clinic's own tenant context (Row-Level Security stays on), only for clinics he owns, and returns aggregates only. Never bypass the filter for owners.
- Never put patient data in logs, URLs, error messages, or analytics. Log IDs, not names, phones or medical text.
- Never ship default credentials. Never commit secrets. Use environment variables or a secret store.

### Sites, accounts, modules, dashboards and the patient portal
- **Two sites:** the clinic site and the admin site, each with its own host name, web app and cookie. `/platform/**` exists only on the admin host and returns 404 elsewhere. Super admins live in `PlatformAdmins`; clinic accounts never work on the admin site and the reverse.
- **Super admin sign-in** is email + password + a one-hour time-based code (3600-second period): mandatory enrollment, replay refused, single-use recovery codes stored hashed, step-up password for module changes, owner deactivation and clinic moves. Never weaken or bypass it for convenience, not even in development builds (use test helpers).
- **No default, temporary or emailed passwords.** Every emailed verification is a 6-digit code the person types in (activation, forgot password, email change, patient activation). Store only an HMAC with a server-side key bound to user and purpose (a plain hash of 6 digits is reversible). Codes expire, allow few attempts (default 5), have a resend cooldown and an hourly cap, and verification is rate limited. Never log, return or put a code in a URL.
- Forgot-password and resend endpoints give the same answer whether or not the email exists. A password reset or email change rotates the security stamp, revokes refresh tokens and notifies the account.
- **Owners add their own clinics** (`POST /clinics`), within an optional quota the super admin sets. A new clinic is `PendingOnboarding` with no modules enabled. Until onboarding is finished only onboarding, own profile, clinic list, add clinic, switch and logout work for it (`ONBOARDING_REQUIRED`).
- **Permissions:** effective permission = (role claims + user grants - user denies) intersected with the permissions of enabled modules. Deny wins. Reserved owner-only permissions cannot go to custom roles. A user can grant only claims they hold. Only the super admin changes modules.
- **Dashboards:** every module registers dashboard widgets in its manifest. One bounded endpoint per widget, authorized by permission, module flag and scope. Widgets return aggregates, never patient identifiers. A failing widget must not break its page.
- **Patient portal:** a separate principal (`PatientAccount`) with its own cookie, realm and `portal.*` policies. The patient ID comes only from the cookie: no portal endpoint accepts a patient ID from the client. Portal reads go through each module's `IPortalSection` and `IPortalQueries` and return purpose-built DTOs, never entities. The only portal writes are booking and cancelling the patient's own appointments, through Scheduling's `IPortalBooking` with the same validation as staff.
- Never show a patient doctor-private notes, tool cost, markup, lab cost, or any other patient's or clinic's data. Portal responses use `Cache-Control: no-store` and `X-Robots-Tag: noindex`. Every portal read of medical data writes a `PatientAccessLog` row (actor type Patient). An unknown slug and a clinic with the portal off return the same 404.

### Money and data integrity
- **Never delete financial records.** Void or reverse only. The ledger is append-only.
- Money is `decimal(18,2)` through the `Money` type. Never use `double` or `float` for money.
- Receipt numbers and patient file numbers come only from number blocks. Never generate them with `MAX()+1`.
- Closed days are locked. Reopening needs a claim and is audited.
- Every audited action (edits, deletions, discounts, free sessions, price changes, refunds, permission changes, logins, device approvals) must write an audit row. Do it through the interceptor, not by hand in each handler.

### Modules
- A module owns its schema and its tables. **Never read another module's tables.** Use `Contracts` interfaces or domain events.
- Core is the only module everyone may reference.
- Domain events use the **transactional outbox**. Handlers must be idempotent.
- Every feature is a module with a per-clinic flag (`ClinicModules`) and declared prerequisites (PLAN.md section 2A). Only the super admin changes flags. Turning a module off must never break a module that does not depend on it (billing works with inventory, finance and clinical off; scheduling works with clinical off). Each module ships an `IModuleManifest` and contributes permissions, menu items, dashboard widgets, settings sections and portal sections through it. Never hard-wire a feature into the menu, dashboard, settings page or portal.
- **Do not use MediatR or AutoMapper.** Use plain handler classes, the small event dispatcher, and hand-written mapping.
- Architecture tests must stay green. If one fails, fix the code, not the test.

### Sync-ready (every endpoint and entity, from Phase 1)
- Accept client-generated GUID v7 IDs.
- Every write takes an `OperationId` and is idempotent through `ProcessedOperations`.
- Every tenant table has `RowVersion` and supports the change feed.
- No hard deletes. Use `IsVoided` or a status.
- Declare a conflict class for each entity: `AppendOnly`, `ServerValidated`, or `LastWriterWins`.
- Online-only actions (closing the day, refunds, price list, roles and claims, adding clinics, onboarding, module changes, the whole admin site, password and email changes, every portal endpoint including booking, cross-clinic reports) must reject sync calls with a clear error code.

---

## 3. Backend conventions (ASP.NET Core 10)

- Layout per module: `Domain`, `Application`, `Infrastructure`, `Contracts`.
- **Domain:** entities, value objects, domain events, no EF or HTTP references.
- **Application:** handler classes (one handler per use case), validation, authorization checks.
- **Infrastructure:** EF Core mappings, repositories, file storage, integrations.
- Schema name = module name (`core`, `patients`, `scheduling`, `clinical`, `billing`, `finance`, `inventory`, `reporting`).
- Times in UTC (`IClock`, never `DateTime.Now`). Display in the clinic's time zone.
- IDs are GUID v7. Money is `Money`. Enums are stored as strings.
- API: versioned routes, `ProblemDetails` for errors with a stable error `code`, cursor or page-based list endpoints, never return unbounded lists.
- Authorization: every endpoint has a policy based on a permission claim, or an explicit `[AllowAnonymous]`. A reflection test enforces this.
- Auth: ASP.NET Core Identity for users, JWT in an HttpOnly Secure cookie, rotating refresh token with reuse detection, device ID in the token. Serve each React app from the same origin as its API (clinic app on the clinic host, admin app on the admin host). Use CSRF protection. Staff, patients and super admins use separate auth schemes, cookies and policy namespaces. Email goes through `IEmailSender` from the outbox, never directly from a handler.
- Permissions are loaded server-side and cached by a version stamp. Do not put them all in the token.
- Logging: Serilog with request ID on every call. Business audit log is separate from technical logs.
- Heavy report queries may use Dapper. Everything else uses EF Core.
- Background work uses Hangfire. Jobs must be idempotent.
- Files are stored behind `IFileStorage`, never in SQL. Downloads use short-lived signed URLs after an authorization check.
- Arabic text: normalize (alef forms, ya and alef-maqsura, ta marbuta, diacritics) in a dedicated normalizer used for search columns.

---

## 4. Frontend conventions (React + TypeScript)

- Vite, TypeScript strict mode, npm workspaces: `web/apps/clinic` (staff app and portal routes), `web/apps/admin` (super admin site), shared `web/packages/ui`, `web/packages/features/<module>` and `web/packages/api-client`. The admin app mounts feature screens read-only to inspect a clinic.
- Server data: TanStack Query. Small UI state: Zustand. Forms: React Hook Form with Zod schemas.
- **RTL first.** Use CSS logical properties (`margin-inline-start`, not `margin-left`). Arabic is the default language; English is secondary. All text goes through i18n keys. No hard-coded strings.
- Light and dark themes through design tokens. Check both themes for every screen.
- UI shows or hides items from the user's claims. This is for convenience only; the server enforces everything.
- All writes go through a single `useOfflineMutation` helper that creates the `OperationId`, writes to the outbox, and updates the UI optimistically. Do not call `fetch` directly for writes. The helper has an `onlineOnly` option (onboarding, account security, admin actions, portal) that shows an offline message instead of queueing.
- The patient portal is a separate lazy-loaded route tree (`web/apps/clinic/src/portal`) with no staff code, mobile-first, with the same RTL, i18n and theme rules.
- Dashboards are built from widget components that each call one widget endpoint; a failing widget shows an error tile. Code input for emailed codes shows digits left-to-right inside RTL pages.
- Show an obvious connectivity indicator and a pending-changes count once Phase 15 starts. Until then, keep the helper in place with online behavior only.
- Local (IndexedDB) data holds only what is needed. Encrypt sensitive fields. Wipe on logout or device revoke.
- Allergy warnings are always visible on the patient file and session screens.
- Target modern evergreen browsers only. No Windows 7/8 support.
- Accessibility basics: labels, focus order, keyboard use, sufficient contrast.

---

## 5. Testing rules

| Layer | Tool |
|---|---|
| Unit | xUnit |
| Integration | xUnit + Testcontainers (real SQL Server, never an in-memory fake) |
| Architecture | NetArchTest or ArchUnitNET |
| Frontend | Vitest + React Testing Library |
| End to end | Playwright (use `context.setOffline` for offline flows) |
| Gate | xUnit in `/tests/Gates` |

- Name tests `Given_<state>_When_<action>_Then_<result>`.
- Money rules, then scheduling rules, then permissions and data scoping, then module boundaries get tested first.
- Use a fake `IClock`. Never depend on the real time or on test order.
- Reset the database between integration tests (Respawn).
- **Every phase must pass these sweeps:** tenant sweep, policy sweep, module-off sweep, architecture tests, migration test, plus verification-code sweep and anonymous-endpoint allowlist (from Phase 1), admin-host sweep (from Phase 2), permission-ceiling, module-combination and dashboard sweeps (from Phase 4), and portal isolation sweep (from Phase 10).
- **After every phase, run the Phase Integration Gate** from PLAN.md, then run all earlier gates (cumulative regression). A failing earlier gate blocks the phase.
- Bug fix rule: write a failing test that reproduces the bug first, then fix it.
- Never delete or weaken a test to make the build pass. If a test is wrong, explain why in `docs/DECISIONS.md` and ask.

---

## 6. Definition of done (per task)

- [ ] Tests written first and passing (unit, integration, frontend where relevant).
- [ ] Tenant isolation proven for any new table or endpoint.
- [ ] Endpoint has a policy, is sync-ready, and returns `ProblemDetails` errors.
- [ ] Audit rows are written for audited actions.
- [ ] Arabic RTL screen checked in light and dark themes.
- [ ] Module manifest updated and the module's dashboard widgets tested.
- [ ] Read-only inspect-mode view exists for the admin site.
- [ ] New emails exist in Arabic and English and are captured by the fake sender in tests.
- [ ] No new warnings, no secrets, no patient data in logs.
- [ ] `docs/PROGRESS.md` and `docs/API.md` updated.

Definition of done (per phase): the Phase Exit Checklist in PLAN.md section 5.

---

## 7. When to stop and ask Ahmed

Stop and ask (one clear question, with your recommended default) when:

- A business rule is missing or two documents disagree.
- You need to change a locked decision or the schema in a way the plan does not show.
- A library license looks restrictive (check QuestPDF and any assertion library before locking versions).
- A security or privacy trade-off appears (for example, what staff may see, what gets logged).
- Open items listed in PLAN.md section 6 become blocking: queue defaults, receipt block size, subscription expiry, negative stock policy, access-log scope, the hourly-code method, clinic quotas, portal enrollment and visibility defaults, booking mode and limits, email provider, code and password settings.

Do not stop for small naming or layout choices. Make the choice, note it in `docs/DECISIONS.md`, and continue.

---

## 8. Things you must not do

- Do not add doctor commission. It was removed.
- Do not add an X-ray-specific feature. Use the general attachments feature (images, files, documents).
- Do not add support for Windows 7/8, a clinic-PC host, Tauri, or a database per clinic.
- Do not store files in the database.
- Do not use hard deletes on any tenant data.
- Do not read another module's tables or bypass the Contracts.
- Do not generate receipt or file numbers by any method except number blocks.
- Do not skip or disable a failing test, sweep, or gate.
- Do not add reminders by any channel (WhatsApp, SMS, email), or online payments. Transactional account emails (activation codes, resets, security notices) are allowed and are the only emails.
- Do not let the portal write anything except booking and cancelling the patient's own appointments.
- Do not email passwords, create default or temporary passwords, or store verification codes unhashed.
- Do not let the super admin sign in on the clinic site, or any clinic account sign in on the admin site.
- Do not give a portal account any staff claim, and do not accept a patient ID from the client in a portal endpoint.
- Do not let a custom role hold owner-only reserved permissions.
- Do not let an owner enable or disable modules.

---

## 9. Quick commands (adjust to the real repo)

```
dotnet build
dotnet test tests/UnitTests
dotnet test tests/IntegrationTests
dotnet test tests/ArchitectureTests
dotnet test tests/Gates
cd web && npm run lint && npm run test && npm run build
cd web && npx playwright test
```

Run the relevant commands before saying a task is done, and record the results in `docs/PROGRESS.md`.
