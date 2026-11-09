# Clinic Platform: Implementation Plan

Multi-clinic, multi-specialty clinic management website. ASP.NET Core 10 API + React PWA. Arabic RTL first.

Read `AGENTS.md` before starting any phase. This plan says WHAT to build and HOW to prove it works. `AGENTS.md` says HOW to behave while building.

---

## 0. How this plan works

- The work is split into **17 phases (0 to 16)**. Build them in order. Do not start a phase until the previous phase's gate is green.
- Every phase has: goal, tasks, rules, tests, and a **Phase Integration Gate**.
- **Tests are written first** from the rules in each phase, then the code. Money rules always come first.
- The **Phase Integration Gate** is a set of scenarios that run through all the modules built so far, against a real SQL Server container. A phase is not done until its gate passes AND every earlier gate still passes (cumulative regression).
- Every phase ends with the **Phase Exit Checklist** (section 5).
- Every API endpoint and entity built in any phase must be **sync-ready** (section 4), even though the offline client is built in Phase 15.

---

## 1. Locked decisions

| Area | Decision |
|---|---|
| Delivery | Website with a hosted database. No clinic PC hosting. Installable as a PWA. |
| Clients | Modern browsers only. Windows 7/8 support removed. |
| Backend | ASP.NET Core 10, EF Core 10, modular monolith. Plain handler classes, a small event dispatcher, hand-written mapping. No MediatR, no AutoMapper. |
| Database | SQL Server. One shared database, `ClinicId` on every tenant table, one schema per module. |
| Tenant isolation | Layer 1: EF Core global query filters. Layer 2: SQL Server Row-Level Security using `SESSION_CONTEXT('ClinicId')`. |
| Frontend | React, TypeScript, Vite, PWA (Workbox). TanStack Query, Zustand, React Hook Form + Zod, Dexie (IndexedDB). Arabic RTL, English secondary, light and dark themes. |
| Real time | SignalR, one group per clinic. |
| Jobs | Hangfire with SQL Server storage. |
| Documents | QuestPDF (check license before locking), ClosedXML. Dapper for heavy report queries. |
| Logging | Serilog to rolling files plus a structured sink, with a request ID on every call. Separate from the business audit log. |
| IDs | GUID v7 (time-ordered), generated on the client or server. |
| Time | Store UTC. Display in the clinic's time zone (default Africa/Cairo). |
| Money | `decimal(18,2)`, one currency per clinic (default EGP). Rounding rule is a clinic setting. |
| Financial data | Never deleted. Void or reverse only. |
| Removed | Doctor commission. X-rays as a feature. Windows 7/8. Clinic-PC host. Tauri. |
| Attachments | Images, files and documents (general feature). Stored in object storage, never in SQL. |
| Inventory | A module that the super admin enables per clinic. |
| Average-time engine | Own module/phase. Server-side. Doctor can switch it on or off. |
| Clinic creation | The super admin creates only the owner account. The owner adds his own clinics (one or many, optional quota set by the super admin). A new clinic has no modules until the super admin enables them. The super admin sees every owner, his clinics and their data. |
| Modules | Every feature is a module with a per-clinic flag, declared prerequisites and a manifest (section 2A). Only the super admin changes flags. Platform foundation (accounts, clinics, roles, settings, profile, audit, dashboard host) is always on. |
| Verification codes | No default, temporary or emailed passwords. Emailed 6-digit codes that the person types in confirm the email and activate accounts, reset passwords and change emails. Stored as HMAC, limited attempts, expiry, cooldown. |
| Super admin site | Separate site and host name, separate account store. Sign-in = email + password + a one-hour time-based code. Platform routes exist only on the admin host. |
| Permissions | Effective permission = (role claims + user grants - user denies) intersected with enabled-module permissions. Deny wins. Reserved owner-only permissions cannot go to custom roles. A grantor can grant only claims they hold. |
| Dashboards | Every module has a dashboard built from registered widgets. The owner has a clinic overview and a combined view of his own clinics. The super admin has a platform dashboard and can open any clinic's dashboards read-only. |
| Clinic URL | Path-based: `https://<host>/c/<clinic-slug>`. Slug chosen in onboarding, unique, ASCII, old slugs keep redirecting. Subdomains can be added later through the same resolver. |
| Patient portal | Module `portal` (read-only record) and module `portal-booking` (free slots, book, cancel own appointments). Separate principal (`PatientAccount`), cookie and policies; online-only; a patient never holds a staff claim. |
| Email | Transactional account emails only (activation codes, resets, security notices), sent through `IEmailSender` from the outbox. Provider is an open item. Reminders by any channel stay out of scope. |

---

## 1A. Architecture and design patterns

The architecture is a **modular monolith** with a shared database and two sites (clinic site and super admin site) served by one API deployment. It is the right size for this project: one deployment, real transactions where money is involved, strict module boundaries that can become services later if one module ever needs to scale alone.

| Concern | Pattern | Why |
|---|---|---|
| Overall shape | Modular monolith. Each module has the layers Domain, Application, Infrastructure, Contracts (onion style, dependencies point inward). | One deploy and one transaction when needed; boundaries enforced by architecture tests. |
| Feature switching | **Module manifest and registry.** Every module ships an `IModuleManifest` (key, prerequisites, permissions, menu items, dashboard widgets, settings sections, portal sections, report sections). The host builds menus, dashboards, settings and the portal only from manifests of modules enabled for the clinic. | One mechanism makes every feature switchable and keeps the UI, permissions and dashboards consistent. |
| Use cases | One handler class per use case. CQRS-lite: commands change state, queries only read. No MediatR. | Explicit, easy to test, no hidden pipeline. |
| Persistence | EF Core `DbContext` per module as the unit of work. No generic repository. Dapper only for heavy reports and dashboard queries. | A generic repository only hides EF and leaks anyway. |
| Talking between modules | Query interfaces in `Contracts`, and domain events through the transactional outbox. | No module reads another module's tables. |
| Multi-tenancy | Shared database, `ClinicId` on every tenant row, EF global filter plus SQL Server Row-Level Security. One `ITenantResolver` with two sources: token claim (staff) and clinic slug in the route (public pages and portal). | Defense in depth; one place decides the clinic. |
| Two sites | Same API deployment, two host names: the clinic site and the admin site. Host-based routing, separate cookies, auth schemes and web apps. Platform routes exist only on the admin host. | A stolen clinic session can never reach platform functions. |
| Super admin sign-in | Separate account store, password plus a time-based code with a 1-hour period (RFC 6238 style), recovery codes, step-up for risky actions. | The most powerful account gets the strongest, separate protection. |
| Authorization | Permission-based policies. An `EffectivePermissions` evaluator (role claims, user grants, user denies, intersected with enabled modules). Resource scoping (doctor sees own patients) as small specification objects. | Fine-grained, per clinic, testable as pure functions. |
| Verification | Typed 6-digit emailed codes, stored as HMAC with a server key, limited attempts, expiry, cooldown. One engine for activation, email confirmation, password reset, email change and patient activation. | One audited, tested security path for every emailed verification. |
| Workflows | Explicit state machines: clinic status, onboarding, appointment, account, visit. | Invalid transitions become impossible, not just unlikely. |
| Specialties | Plugin plus strategy: `ISpecialtyModule` registers forms, default categories and optional tables. Forms are JSON Schema. | A new specialty needs a definition, not new tables. |
| Dashboards | **Widget registry.** Each module contributes widgets; one bounded, cached endpoint per widget; pages are composed from the registry. Owner portfolio view loops over the owner's own clinics, each under its own tenant context. | Every feature gets a dashboard without a giant reporting service, and tenant isolation stays intact. |
| Patient portal | Separate principal type and a read model: each module exposes `IPortalQueries` (and Scheduling exposes `IPortalBooking`) with purpose-built DTOs (anti-corruption layer). | Patients never touch domain entities, so leaking private fields is structurally hard. |
| Settings page | `ISettingsSection` registry: each module contributes its own section and permission. | The settings page grows without one giant form. |
| Offline and sync | Client-generated IDs, idempotent operations, outbox on the client, change feed on the server. | Already in section 4. |
| Frontend | npm workspaces: clinic app, admin app, shared feature packages and design system. Route-level code splitting (staff app and portal are separate chunks), a `useCan` capability hook, TanStack Query for server state, one mutation helper. | Patients never download staff code; the admin site can reuse the same screens read-only. |

Deliberately avoided: microservices (team size, cost, and money needs transactions), a database per clinic (already rejected), generic repositories, MediatR, AutoMapper, sharing entities through `Contracts`.

---

## 2. Roles and tenancy model

```
Platform Super Admin (Ahmed), works only on the ADMIN SITE
   |-- creates Owner accounts (name, email) and may set a clinic quota per owner
   |-- sees every owner, every clinic under each owner, and all their data and logs
   |-- enables or disables every module, per clinic
   |-- sets clinic status (Active, ReadOnly, Suspended) and subscription dates
Clinic Owner ("clinic master"), works on the CLINIC SITE
   |-- activates the account with the emailed code, then adds his own clinics (one or many)
   |-- finishes onboarding for each clinic and switches between them
   |-- sees the modules the super admin enabled for each clinic (read-only)
   |-- creates Roles inside a clinic; each Role has Claims taken only from enabled modules
   |-- invites Users by email and assigns Roles
   |-- edits the clinic's general data on the settings page (Owner and Super Admin only)
   |-- sees the clinic dashboard, every feature dashboard, and a combined view of his clinics
   |-- adds Specialties (professions), Rooms, Doctors; sets prices and discount rules
Clinic staff: Doctor, Assistant, Receptionist, custom roles
   |-- see only what their claims allow, only inside their clinic
Patient (portal principal, only when the Portal module is on)
   |-- belongs to exactly one clinic; reads their own record and books free slots at /c/<slug>
```

### Principal types

| Principal | Store | Site, auth scheme and cookie | Policies |
|---|---|---|---|
| Super admin | `PlatformAdmins` (own table, not Identity users) | Admin site only, own `__Host-` cookie | `platform.*` |
| Owner and staff | Staff realm (Identity), linked to clinics | Clinic site, staff cookie, active-clinic claim | Permission claims |
| Patient | `PatientAccounts` (Portal module), patient realm | Clinic site, portal cookie (own name and path) | `portal.*` only |

No cookie works across principal types. A super admin login on the clinic site and a clinic login on the admin site both fail with the same generic error. A patient registered in two clinics has two separate accounts.

### Ownership and clinic lifecycle

- `Clinics.OwnerId` points to the owner who created it. An owner can add as many clinics as the optional quota allows (no limit unless the super admin sets one). The super admin can move a clinic to another owner (audited).
- A clinic created by an owner starts as `PendingOnboarding` with **no modules enabled**. The owner finishes the wizard, then waits for the super admin to enable modules. The super admin sees "new clinics awaiting setup" on the platform dashboard.
- `PendingOnboarding` -> `Active` (owner finished) -> `ReadOnly` (subscription grace over) -> `Suspended`. Only the super admin moves a clinic to `ReadOnly` or `Suspended`.

### Rules

- A clinic can see **only its own data and patients**. Nothing from clinic B is ever visible to clinic A. Cross-clinic access returns 404. Missing permission inside your own clinic returns 403.
- The super admin sees everything through one explicit service (`IPlatformAccess`). Every read of patient data through it writes a `PatientAccessLog` row that the clinic owner can see.
- An owner's combined view of his own clinics goes through `IOwnerPortfolio`: it runs each clinic's query under that clinic's own tenant context (Row-Level Security stays on), only for clinics he owns, and returns aggregates only.
- An owner with several clinics picks an **active clinic**; the token carries that `ClinicId`; switching issues a new token. Onboarding is per clinic.
- **Effective permission** = (claims of the user's roles + user grants - user denies) intersected with the permissions of the modules enabled for the clinic. Deny wins. A claim of a disabled module stays stored but is dormant.
- **Delegation ceiling:** a user can grant a claim only if they hold it. The Owner holds every non-reserved claim of the enabled modules.
- **Reserved permissions** (clinic settings, roles management) belong to the Owner role only and cannot be given to a custom role.
- Only the super admin changes modules. The owner can see which are on, nothing more.
- **No default or temporary passwords.** Every account is created by an invitation: the person receives a code by email, types it in and chooses their own password. The first super admin is seeded from environment variables and must change the password and enroll the hourly code on first login.

---

## 2A. Module catalog (every feature is a module)

**Always on (platform foundation, not switchable):** accounts and sign-in, clinics and onboarding, roles and permissions, clinic structure (specialties, rooms, doctors, hours), settings, profile, audit, the dashboard host and the module registry.

**Switchable features.** The super admin turns each one on or off per clinic. Prerequisites must be on first.

| Module key | What it contains | Prerequisites |
|---|---|---|
| `patients` | Patient registry: file, phones, search, duplicate detection, optional email | none |
| `patient-history` | Allergies, conditions, medications, surgeries, habits, flags, notes, allergy banner | `patients` |
| `attachments` | Images, files and documents on patients, visits and plans | `patients` |
| `scheduling` | Categories, appointments, status machine, calendar, availability engine, closed days, doctor timetable | `patients` |
| `queue` | Waiting queue, late and push rules, VIP priority, waiting list, slot offers | `scheduling` |
| `clinical` | Visits, session timer, vitals, diagnoses, procedures, prescriptions, lab orders, forms, specialty plugins | `patients`, `scheduling` |
| `billing` | Price list, charges, ledger, payments, receipts, refunds, debts, daily closing, manual charges | `patients` |
| `finance` | Suppliers, supplier payments, expenses, fixed costs, payroll, income versus expenses | none |
| `treatment-plans` | Multi-session plans, installments | `patients`, `scheduling`, `billing` |
| `inventory` | Items, batches, purchases, automatic deduction, expiry and low-stock alerts | none |
| `time-engine` | Average session time learning and suggestions | `scheduling`, `clinical` |
| `reports` | Report center and Excel or PDF exports (sections appear only for enabled modules) | none |
| `excel-import` | Excel import wizard (offers only the data types of enabled modules) | `patients` |
| `portal` | Public clinic page, patient accounts, read-only record view | `patients` |
| `portal-booking` | Patients see free slots, book and cancel their own appointments | `portal`, `scheduling` |
| `offline-sync` | Offline PWA and device sync | none |

Which modules are switchable and the prerequisites above are the plan's defaults (confirm in section 6).

**Rules**
1. **Manifest.** Each module ships an `IModuleManifest`: key, prerequisites, permissions, menu items, dashboard widgets, settings sections, portal sections and report sections. Nothing is hard-wired into the menu, dashboards, settings or portal.
2. **Dependencies.** A module cannot be enabled while a prerequisite is off. A module that other enabled modules need cannot be disabled (409 `MODULE_HAS_DEPENDENTS`, listing them). An architecture test compares each module's declared prerequisites with its real project references.
3. **Optional links.** Where one module only enriches another, the link is an event or a contract call that is simply skipped when the other module is off. Examples: billing works with `clinical` off (staff add manual charges); lab work posts its cost to expenses only when `finance` is on; the portal shows sections only for enabled modules; the allergy banner exists only when `patient-history` is on.
4. **Off means:** menu items, screens, settings sections, portal sections, report sections and dashboard widgets disappear; endpoints return `MODULE_DISABLED`; events are ignored; background jobs skip the clinic; claims stay stored but dormant; data is kept. Turning it on again restores everything. Tables of every module are migrated for every clinic; flags only gate behavior.
5. **Who:** only the super admin changes modules (admin site, step-up password, audited). A change publishes `ClinicModuleChanged` through the outbox: permissions refresh and open sessions update their menus.
6. **Proof:** the module-combination sweep (section 5) runs the gate scenarios of each module with only its prerequisites on, and with each module off on its own.

---

## 2B. Dashboards

Every module has its own dashboard, the clinic owner has an overview of everything, and the owner and the super admin have combined views.

- **Widget contract (`IDashboardWidget`):** key, module, title keys (ar, en), required permission, size, scope (`Clinic`, `Doctor`, `Portfolio`), and a query that takes a date range. Types: KPI number, chart, list, alert.
- **One bounded endpoint per widget:** `GET /dashboards/widgets/{key}?from=&to=`. Maximum range 366 days, short cache (about 60 seconds), clinic time zone and currency, authorization on permission, module flag and scope. A slow or failing widget shows its own error tile and never breaks the page. Heavy ones use Dapper or event-fed read models.
- **No patient identifiers in widgets.** Widgets return counts, sums and aggregates. A drill-down opens the normal screen, which enforces its own permission.
- **Pages:**
  1. **Owner clinic overview** (`/dashboard`): KPI strip, alerts feed (low stock, near expiry, closing differences, sync conflicts, pending online requests), then widgets of every enabled module the user may see. The user can pin and reorder widgets (`UserDashboardLayout`, last writer wins).
  2. **Feature dashboard** (`/<module>/dashboard`): all widgets of one module with a date-range filter. Permission `<module>.dashboard.view`.
  3. **Doctor view:** the same widgets scoped to the doctor's own numbers (scope `Doctor`).
  4. **Owner portfolio** (`/portfolio`): one card per clinic he owns, aggregate widgets marked `PortfolioSafe`, built by `IOwnerPortfolio`.
  5. **Admin site:** platform dashboard, owner detail page, and each clinic's dashboards in read-only inspect mode (Phase 2).
- Each phase that adds a module also adds that module's widgets, dashboard page and tests.

---

## 3. Repository layout

```
/src
  /Platform.Api                 (host, composition root, SignalR hubs, Hangfire; serves the clinic site and, on its own host name, the admin site)
  /BuildingBlocks               (shared kernel: Result, Money, Clock, GuidV7, ITenantEntity,
                                 Outbox, EventDispatcher, Audit interceptor, Tenancy,
                                 IEmailSender, verification codes, rate limiting, module manifest and widget contracts)
  /Modules
    /Core         (Domain, Application, Infrastructure, Contracts)
    /Patients     (...)
    /Scheduling   (...)
    /Clinical     (...)  + /Clinical.Dental, /Clinical.<Specialty> plugins
    /Billing      (...)
    /Finance      (...)  (suppliers, expenses, fixed costs, salaries)
    /Inventory    (...)  (flagged module)
    /Reporting    (...)
    /Portal       (...)  (flagged modules: patient accounts, public clinic pages, portal booking)
/tests
  /UnitTests  /IntegrationTests  /ArchitectureTests  /Gates (phase gate scenarios)
/web
  /apps/clinic   (clinic staff app and the patient portal routes)
  /apps/admin    (super admin site, own origin)
  /packages/features/<module>   (screens per module, shared by both apps)
  /packages/ui  /packages/api-client  (design system, RTL, themes, i18n, offline helpers)
  /e2e  (Playwright)
/docs   (PLAN.md, AGENTS.md, PROGRESS.md, DECISIONS.md, API.md)
```

Module rules (enforced by architecture tests):

1. A module owns its schema. No module reads another module's tables.
2. Modules talk only through `Contracts` interfaces or domain events.
3. Core is the only module everyone may depend on.
4. Domain events go through a **transactional outbox** (written in the same transaction as the change) and are handled idempotently.
5. Switching a module off for a clinic must not break any module that does not depend on it.
6. Every module ships an `IModuleManifest` (key, prerequisites, permissions, menu, dashboard widgets, settings sections, portal sections, report sections). Menus, dashboards, settings and the portal are built only from manifests of enabled modules. An architecture test compares declared prerequisites with real project references.
7. The Portal module reads other modules only through purpose-built `IPortalQueries` contracts and books only through Scheduling's `IPortalBooking`. It never receives entities.

---

## 4. Sync-ready rules (apply from Phase 1 onward)

Every entity and endpoint must support the offline design from the start:

1. **Client-generated IDs** (GUID v7). The server accepts an ID from the client on create.
2. **Idempotency**: every write carries an `OperationId`. The server stores it in `ProcessedOperations`. Replaying the same operation returns the original result and changes nothing.
3. **Change feed**: every tenant table has a `RowVersion`, and the pull endpoint can list changes since a cursor, scoped to the clinic.
4. **Soft state only**: no hard deletes. Use `IsVoided` or a status.
5. **Conflict class** is declared per entity:
   - `AppendOnly`: money, stock movements, clinical notes. Never overwritten.
   - `ServerValidated`: appointments and queue. The server checks and may raise a `SyncConflict`.
   - `LastWriterWins`: simple profile fields and settings. The old value goes to the audit log.
6. **Numbering**: receipt numbers and patient file numbers come from **number blocks** reserved per device (see Phase 4 and Phase 15).
7. **Offline allowed / online only** is listed in Phase 15. Online-only actions must return a clear error code when called through the sync endpoint.
8. Account security (activation, login, verification codes, password reset, email change), onboarding, clinic creation, module changes, the whole admin site and every Portal endpoint (including portal booking) are online-only and never part of sync.

---

## 5. Test strategy and exit checklist

| Layer | Tool | Purpose |
|---|---|---|
| Unit | xUnit | Domain rules (money, queue, state machines, calculations) |
| Integration | xUnit + Testcontainers (real SQL Server) | Handlers, EF mappings, migrations, tenancy, RLS, outbox |
| Architecture | NetArchTest or ArchUnitNET | Module boundaries |
| Frontend unit | Vitest + React Testing Library | Components, hooks, forms, permission-aware UI |
| End to end | Playwright (incl. `context.setOffline`) | Key flows, offline flows |
| Gate | xUnit in `/tests/Gates` | Cross-module scenarios after every phase |

Naming: `Given_<state>_When_<action>_Then_<result>`.

### Tests that run in EVERY phase (do not skip)

- **Tenant sweep**: for every endpoint that takes an ID, call it as clinic B with clinic A's ID. Expect 404 (not 403, to avoid leaking existence). This test is generated by reflecting over the routes.
- **Policy sweep**: every endpoint has an authorization policy or an explicit `[AllowAnonymous]`.
- **Module-off sweep**: with a module disabled for a clinic, its endpoints return the "module disabled" error and no other module breaks.
- **Architecture tests**: module boundaries.
- **Migration test**: all schemas migrate from empty, and from the previous phase's database snapshot.
- **Verification-code sweep** (from Phase 1): every emailed-code flow stores only HMAC values, accepts a code once, dies after the attempt limit, rejects expired codes, never logs or returns a code, and answers the same for known and unknown emails.
- **Anonymous-endpoint allowlist** (from Phase 1): every `[AllowAnonymous]` endpoint must appear in an approved list inside the test, so a new one forces a review.
- **Admin-host sweep** (from Phase 2): every `/platform/**` route exists only on the admin host, requires the super admin principal, and returns 404 on the clinic host.
- **Permission-ceiling sweep** (from Phase 4): every catalog permission is refused when its module is off, reserved permissions cannot go to custom roles, and nobody can grant a claim they lack.
- **Module-combination sweep** (from Phase 4): for each module, its gate scenarios pass with only its prerequisites enabled, and every module that does not depend on module M still passes its gate with M switched off on its own.
- **Dashboard sweep** (from Phase 4): every widget declares a module, a permission and a scope; is refused when its module is off; and its DTO has no patient identifier fields (reflection test).
- **Portal isolation sweep** (from Phase 10): for every portal route, another patient's resource IDs (same clinic or another clinic) return 404, and a staff cookie is rejected.

### Phase Exit Checklist

- [ ] All new unit, integration, frontend and E2E tests pass.
- [ ] The Phase Integration Gate passes.
- [ ] All earlier gates still pass (cumulative regression).
- [ ] Tenant, policy, module-off and architecture sweeps pass.
- [ ] Migrations apply cleanly. Seed data is idempotent.
- [ ] Every new endpoint is sync-ready (section 4).
- [ ] Arabic RTL screens checked in light and dark themes.
- [ ] Module manifest updated (permissions, menu, widgets, settings and portal sections) and the module's dashboard and widgets are tested.
- [ ] Read-only inspect-mode views for the admin site exist for the new screens.
- [ ] Every new email exists in Arabic and English and is captured by the fake sender in tests.
- [ ] `docs/PROGRESS.md` and `docs/API.md` updated. Open questions logged in `docs/DECISIONS.md`.

### Cross-module integration matrix (what each Phase Integration Gate exercises)

| Phase | Modules exercised together | Mechanism | Key cross-module assertion |
|---|---|---|---|
| 0 | BuildingBlocks, host, email, two sites | Outbox, tenancy, `IEmailSender`, host routing | Email sent once and never on rollback; `/platform/**` 404 on the clinic host |
| 1 | Core, identity, email | Invitation service, verification codes | Code email sent once; code works once and dies after 5 failures; sessions revoked on reset |
| 2 | Platform admin, identity, audit | Admin host, hourly code, `IAccountInvitations` | Three-factor login; clinic credentials fail on admin site; owner invitation email |
| 3 | Core clinics, Clinical plugins, files, platform dashboard | `ClinicCreated` event, `ISpecialtyModule`, `IFileStorage` | Owner adds several clinics; onboarding gate; admin sees owner's clinics |
| 4 | Module registry, roles, dashboard host, admin site | `ClinicModuleChanged` event, manifests, widget registry | Module off makes claims dormant and widgets vanish live; portfolio shows only own clinics |
| 5 | Patients, history, attachments, Core | Number blocks, scoping, manifests | File numbers unique under concurrency; history off removes the allergy banner |
| 6 | Scheduling, queue, Patients | Availability engine, SignalR | Offered slot is always bookable; queue off leaves appointments working |
| 7 | Clinical, Scheduling, Patients | `SessionCompleted` | Visit completion publishes once; clinical off leaves scheduling working |
| 8 | Billing, Finance basics, Clinical | `SessionCompleted` consumer, number blocks | Ledger equals payments; billing works with clinical, finance and inventory off |
| 9 | Plans, Scheduling, Billing | Contracts, events | Fixed-total sessions create no charge; deposit refund reverses |
| 10 | Portal, Patients, history, Scheduling, Clinical, Billing, Plans | `IPortalSection`, `IPortalQueries`, `IClinicDirectory`, verification codes | Patient sees only own data; no cost or markup leaks; sections follow enabled modules |
| 11 | Portal booking, Scheduling, Queue | `IAvailabilityQueries`, `IPortalBooking` | Race for one slot has one winner; cancel offers the waiting list |
| 12 | Inventory, Clinical, Billing, Finance | `SessionCompleted`, Finance contract | Stock deducted once; billing unaffected when off |
| 13 | Time engine, Clinical, Scheduling | Event consumer, contract | Engine off changes nothing; applied duration feeds availability |
| 14 | Finance, Reports, Import, all | Read contracts, Dapper | Totals equal ledgers to the cent; sections follow enabled modules |
| 15 | Sync, all writable modules | `/sync/push`, `/sync/pull` | Replay changes nothing; online-only actions refused; portal bookings respected |
| 16 | Everything | Fuzzer, restore drill, module combinations | Zero cross-clinic leaks in any module combination |

Each gate also reruns every earlier gate.

---

## Phase 0: Foundation and walking skeleton

**Goal:** a running, tested skeleton that proves tenancy, logging and the test harness before any business feature.

**Backend tasks**
- Solution, projects and module folders as in section 3. CI pipeline (build, test, lint).
- Building blocks: `Result`, `Money`, `IClock`, `GuidV7`, `ITenantEntity`, `IAuditable`, outbox + dispatcher, idempotency store.
- Tenancy: `ICurrentClinic`, EF global filter convention, `SESSION_CONTEXT` set per connection, RLS policy script, `IPlatformAccess` bypass with audit.
- Serilog with request ID middleware. ProblemDetails error format. Health checks. Hangfire set up.
- Notifications building block: `IEmailSender` with an outbox-backed sender (Hangfire retries), an Arabic and English template renderer, a fake sender for tests that captures messages, and a local mail catcher (for example Mailpit) in the dev compose file.
- Security building blocks: `IVerificationCodes` (6-digit codes, HMAC with a server key bound to user and purpose, attempt counter, expiry, resend limits), rate-limit policies (per IP and per account), helpers for `Referrer-Policy: no-referrer` and `Cache-Control: no-store`.
- `ITenantResolver`: resolves the clinic from the token claim (staff) or from the clinic slug in the route (public pages and portal). One abstraction, two sources.
- **Two sites:** host-based routing with an admin host and a clinic host. A route group registered for one host returns 404 on the other. Separate cookie names and auth scheme placeholders.
- **Module manifest contracts:** `IModuleManifest`, `IDashboardWidget`, `ISettingsSection`, `IPortalSection` interfaces in BuildingBlocks (the registry arrives in Phase 4).
- EF Core migrations, one schema per module.
- Test harness: shared SQL Server Testcontainers fixture, per-test database reset (Respawn), architecture test project.

**Frontend tasks**
- Set up npm workspaces: `web/apps/clinic`, `web/apps/admin`, `web/packages/ui`, `web/packages/features`, `web/packages/api-client`. The admin app starts as an empty shell served on its own origin.
- Vite + TypeScript, RTL layout, i18n (ar/en), light/dark theme tokens, sidebar shell, API client, TanStack Query setup, auth shell, empty PWA manifest and service worker (offline logic comes later), Vitest and Playwright set up.

**Tests**
- Unit: `GuidV7` is time-ordered; `Money` rounding modes; `IClock` fake works.
- Integration: a dummy tenant entity is invisible across clinics through the EF filter; a raw SQL query with the wrong `SESSION_CONTEXT` returns nothing (RLS); the bypass writes a platform audit row; the outbox event is saved with the entity and delivered once, even if the handler throws and retries.
- Architecture: module boundary rules, Contracts-only references.
- Unit: verification code generate and verify (HMAC, attempts, expiry); an email template renders in Arabic and English with the right direction.
- Integration: an email queued inside a transaction is sent once, and not at all if the transaction rolls back. A route registered for the admin host answers 404 on the clinic host and the other way round.
- Frontend: theme switch, RTL direction, i18n fallback.

**Phase 0 Integration Gate (walking skeleton)**
1. API starts in a container, migrates every schema, health check is green.
2. Create a dummy record as clinic A; clinic B cannot see it by list, by ID, or by raw SQL.
3. The request ID appears in the log for a failing call.
4. The built React app is served from the same origin as the API and shows the login shell in Arabic RTL.
5. A test email is queued in a transaction, delivered once to the mail catcher, and never delivered when the transaction rolls back.
6. A test platform route answers on the admin host and returns 404 on the clinic host; both web apps build and are served from their own origin.

---

## Phase 1: Accounts and clinic-site identity

**Goal:** owners, staff (and later patients) are invited, confirm their email by typing a code, set their own password, sign in, reset a forgotten password, and use approved devices. No temporary password exists anywhere.

**Backend tasks and data**
- **Users:** ASP.NET Core Identity store for the Staff realm (owners and staff): email, `EmailConfirmed`, full name, phone, locale, theme, active flag, security stamp. The Patient realm uses the same services in Phase 10. Super admins live in a separate store (Phase 2).
- `Owners` (user, `MaxClinics` nullable meaning no limit, status). Clinics are linked to owners in Phase 3. `UserClinics` (user, clinic, role) is created now.
- **Verification codes** (`VerificationCodes`, in Core, shared by all realms): purpose (`Invitation`, `EmailConfirmation`, `PasswordReset`, `EmailChange`, `PatientActivation`), user, realm, `CodeHash`, expiry, attempts, max attempts, used-at, payload (for example the new email).
  - 6 digits from a secure random generator.
  - Stored as an **HMAC-SHA256 with a server-side key**, bound to user ID and purpose. A plain hash of a 6-digit number can be reversed in milliseconds, so it is never used.
  - Few attempts per code (default 5), then the code is dead and a new one is needed. Single use. A new code of the same purpose invalidates the older one.
  - Expiry is a setting (defaults: invitation 72 hours, everything else 15 minutes).
  - Resend has a cooldown (default 60 seconds) and an hourly cap, per account and per IP. Verification attempts are also rate limited per IP and per account.
  - Codes are never logged, never returned by any API, and never put in URLs.
- **Invitation and first access:** `IAccountInvitations.Invite(realm, email, name, role)` creates a passwordless, unconfirmed user and queues the code email (Arabic or English). The email contains the activation page address (no secret) and the code. On the activation page the person enters email, code, new password and confirmation. On success: email confirmed, password set, account active, signed in. Until then, login answers with a generic message and offers "enter code" and "resend code". The first users are created through a seed command and test helper; Phase 2 adds the admin site that creates owners, Phase 4 lets owners invite staff, Phase 10 lets clinics invite patients.
- **Email change** (later used on the profile page): new email receives a code; the email changes only after the code is entered; a notice goes to the old email.
- **Login:** email and password, lockout after repeated failures (defaults: 5 attempts, 15 minutes), generic errors, device check, JWT in an HttpOnly Secure cookie with a rotating refresh token and reuse detection. A `Suspended` clinic cannot log in; a `ReadOnly` clinic can read but not write (both apply once clinics exist, Phase 3).
- **Forgot and reset password:** `POST /auth/forgot-password {email}` always answers 202 with the same body and similar timing; if the account is active, a code is emailed. `POST /auth/reset-password {email, code, newPassword}` checks the code. On success: password set, security stamp rotated, every refresh-token family revoked, code marked used, audit row, and a "your password was changed" email.
- **Password rules:** minimum length and a common-password check (defaults in section 6); never equal to the email.
- **Devices:** random device ID per install; admin approves or revokes; device ID in the token; a revoked device is rejected.
- **Audit:** an EF interceptor records who, what, when, old and new values for edits, deletions, discounts, free sessions, price changes, refunds, permission changes, logins, password resets, device approvals.
- **Subscription fields** on the clinic (expiry, grace days) are created now and used from Phase 3.

**Frontend tasks (clinic app)**
- Login, activation page (email, code, password), code-entry component (6 boxes, paste support, digits always left-to-right inside RTL, resend countdown, remaining attempts), forgot-password (two steps), reset-password, forced device approval message.
- Arabic RTL and English, light and dark. Forms show the same generic messages the server gives.

**Tests (write these first)**
- Unit: code generation and HMAC verification (wrong code counts an attempt, dead after max attempts, expired, replaced by a newer code, reuse refused); resend cooldown and hourly cap; lockout arithmetic; password rules.
- Integration:
  - An invitation email reaches the fake sender with a code; activation works once with the right code; a wrong code five times kills the code; an expired code is refused.
  - An unconfirmed account cannot log in.
  - The database never contains a raw code (only HMAC values) and the code never appears in logs or API responses.
  - Forgot-password returns an identical response for known and unknown emails; reset revokes all refresh tokens and sends the notice email.
  - Refresh-token reuse revokes the family; a revoked device is rejected; rate limits and lockout work.
  - Audit rows exist for each audited action.
- Frontend: code-entry component (paste, backspace, RTL), activation validation, generic error display.

**Phase 1 Integration Gate (modules: Core, identity, outbox, email)**
1. The seed command invites owner O1; the email arrives in the fake sender with a code; O1 cannot log in before activating.
2. O1 enters email, code and a password; the same code no longer works; O1 logs in. Five wrong codes on another invitation kill that code, and a resend issues a fresh one.
3. O1 forgets the password; an unknown email gets the same response; the reset with the emailed code works once; old sessions are revoked; the notice email is sent.
4. A revoked device and a reused refresh token are both rejected.
5. The audit trail shows every step.
6. Phase 0 gate, tenant sweep, policy sweep, verification-code sweep and anonymous-endpoint allowlist pass.

---

## Phase 2: Super admin site

**Goal:** a separate site for the platform owner (Ahmed) on its own host name. Sign-in needs email, password and a code that changes every hour. From it he creates owners, sees every owner with the clinics under him and all their data, and watches the platform.

**Backend tasks and data**
- **Separate account store:** `PlatformAdmins` (email, password hash, encrypted TOTP secret, `TotpEnrolled`, recovery codes stored hashed, lockout fields, last login). Not part of the clinic Identity users.
- **Hourly code:** a time-based one-time code (RFC 6238 style) with a **3600-second period**, 6 digits.
  - Accepted: the current period, and the previous period only during the first 5 minutes of a new period (clock skew and boundary).
  - Replay refused: the last accepted time step is stored and a code can be used once.
  - The secret is encrypted at rest with the data-protection key.
- **Enrollment (forced):** the seeded account (email and first password from environment variables) must change the password and enroll the code before anything else works. The page shows a QR code and the manual key; enrollment completes only when a real code is entered. Then 10 single-use recovery codes are shown once. Some authenticator apps ignore a custom period (many fixed 30-second apps do), so Ahmed must verify that the app he will use shows a 1-hour code; enrollment fails safely if it does not.
- **Break-glass:** if the device and recovery codes are lost, a server-side command (`platform-admin reset-2fa`, needs host access and an environment secret) resets enrollment. It writes an audit row.
- **Host separation:** the admin API (`/platform/**`) and the admin web app exist only on the admin host. On the clinic host `/platform/**` returns 404. Cookies use the `__Host-` prefix, SameSite Strict, no CORS. Clinic credentials fail on the admin site and super admin credentials fail on the clinic site, with the same generic error. Optional IP allowlist from configuration. Idle timeout 20 minutes, absolute lifetime 8 hours.
- **Step-up:** re-entering the password is required for module changes, deactivating an owner, moving a clinic, changing quotas and break-glass tools.
- **Owners:** create an owner (name, email, phone, optional `MaxClinics`); this calls `IAccountInvitations` from Phase 1, so the owner gets the code email. Resend code, deactivate or reactivate an owner (a deactivated owner and his clinics cannot sign in). Owner list with search.
- **Owner detail page:** profile, status, quota, and the clinics under him (name, slug, status, subscription, modules enabled, last activity).
- **Clinic directory:** all owners and clinics in one tree, search by owner, clinic name or slug. Set clinic status (`Active`, `ReadOnly`, `Suspended`) and subscription dates. Move a clinic to another owner (audited).
- **Inspect clinic (read-only):** `/platform/clinics/{id}/...` read endpoints that call the same module query handlers with the clinic set explicitly through `IPlatformAccess`. Every read of patient data writes a `PatientAccessLog` row (actor type SuperAdmin). Writes are refused except explicit admin actions (status, subscription, modules in Phase 4, settings). Each later phase adds its read-only views to this mode as part of its definition of done.
- **Platform audit viewer** (all clinics, all admin actions) and **platform dashboard** widgets: owners, clinics by status, new clinics awaiting module setup, subscriptions expiring soon, module adoption, recent audit entries, failed logins, outbox and job health, storage used.

**Frontend tasks (admin app, own origin)**
- Login page with three fields (email, password, hourly code) and a recovery-code option; forced enrollment screens; owners list and detail; clinic tree; inspect-clinic shell (clinic feature screens from the shared feature packages mounted read-only with a "viewing as platform admin" banner); platform dashboard; audit viewer. Arabic RTL and English, light and dark.

**Tests (write these first)**
- Unit: time-step computation for a 3600-second period; boundary window (accepted in the first 5 minutes of the next period, refused after); replay refused; recovery codes single use; lockout; step-up expiry. Use a fake `IClock` across period boundaries.
- Integration:
  - Password alone fails; password plus a wrong code fails; password plus a reused code fails; all three succeed.
  - Enrollment is forced and nothing else works before it; a recovery code works once.
  - `/platform/**` returns 404 on the clinic host; clinic app routes are not served on the admin host; clinic credentials and super admin credentials fail on the wrong site.
  - Creating an owner sends the Phase 1 invitation code email; deactivating an owner blocks his login.
  - Inspect mode refuses writes and logs every patient-data read; the clinic owner can see those log rows.
  - Moving a clinic to another owner is audited and the old owner loses access.
  - Every admin action writes a platform audit row; step-up is enforced.
- Frontend: three-field login, enrollment flow, owner detail rendering, read-only banner and hidden write controls in inspect mode.

**Phase 2 Integration Gate (modules: Platform admin, Core identity, email, audit)**
1. The seeded super admin signs in on the admin host, is forced to change the password and enroll the hourly code, and receives recovery codes.
2. Sign-in then requires email, password and the current code; the same code cannot be reused; a code from the previous hour works only inside the 5-minute window.
3. The super admin creates owner O1 (quota 3); the invitation code email arrives; O1 activates on the clinic site.
4. The same URL paths behave correctly on both hosts: `/platform/**` is 404 on the clinic host; O1 cannot sign in on the admin host.
5. The owner detail page lists O1 and his clinics (none yet); the platform dashboard shows the new owner.
6. Break-glass reset works and is audited.
7. Phases 0 to 1 gates, tenant sweep, policy sweep, admin-host sweep and anonymous-endpoint allowlist pass.

---

## Phase 3: Owner clinics, onboarding, clinic settings and profile

**Goal:** the owner adds his own clinic(s), is welcomed and guided through onboarding for each one, can switch between them, edit the clinic's general data, and manage his own profile.

### 3.1 Owner adds clinics

- `Clinics` (owner, internal id, name, status, time zone, currency, rounding rule, subscription fields from Phase 1, onboarding status). `POST /clinics` is available to an owner who has no clinic yet or who is under his quota (`Owners.MaxClinics`, empty means no limit). Over quota returns `CLINIC_QUOTA_REACHED`.
- A new clinic is `PendingOnboarding` with **no modules enabled**. A `ClinicCreated` event goes through the outbox: the platform dashboard shows "awaiting module setup" and the super admin may also get an email (setting).
- Login for an owner with no clinic lands on "add your first clinic". With one or more clinics, a clinic picker and a header switcher appear. The token carries the active `ClinicId`; with no active clinic, only profile, clinic list, add clinic and logout work.
- "My clinics" page: a card per clinic (name, logo, status, modules enabled, onboarding progress) and an "add clinic" button.

### 3.2 Onboarding wizard (the first thing a new owner sees)

- `Clinics.OnboardingStatus`: `NotStarted` -> `InProgress` -> `Completed`. `OnboardingProgress` (clinic, step key, completed-at, by) makes the wizard resumable.
- **Onboarding guard:** while a clinic is not `Completed`, every endpoint of that clinic except onboarding returns 409 with code `ONBOARDING_REQUIRED`. Profile, clinic list, add clinic, switch and logout still work. Super admin inspect mode is exempt and read-only.
- **Steps:**
  1. Welcome (once per owner): greets the owner by name and explains the steps.
  2. Clinic details: name (required), logo (optional), address (street, city, governorate; required), main phone (required, normalized), extra phones and email (optional).
  3. Clinic type: one or several from the catalog (for example Dental, Cardiology, Ophthalmology, Dermatology, Pediatrics, ENT, Orthopedics, Gynecology, Internal medicine, General practice, Physiotherapy) or "Other" with a free-text name. Each choice creates a clinic `Specialty` and loads that type's defaults from its `ISpecialtyModule` (clinical form, default appointment categories, the Dental plugin for dental). Everything stays editable. Forms and plugins become usable only when the `clinical` module is on.
  4. Public link: slug suggested from the name, live availability check, preview of `https://<host>/c/<slug>`. The slug is reserved even if the Portal module is off.
  5. Review and finish: sets `Completed`, writes an audit row, opens the clinic dashboard. If no module is enabled yet, the dashboard shows "waiting for the platform admin to enable your features" and a checklist of what the owner can already do (roles, staff, rooms, doctors).
- **Slug rules:** 3 to 40 characters, `a-z 0-9 -`, no leading, trailing or double hyphen, case-insensitive unique across the platform, reserved words (`admin`, `api`, `app`, `c`, `login`, `portal`, `platform`, `static`, `assets`, `health`, `www`, and similar). `ClinicSlugs` keeps history: an old slug is never reused and keeps redirecting. Arabic names are transliterated to ASCII for the suggestion, with a fallback such as `clinic-4f9a`.
- **Logo:** png, jpg or webp, 2 MB maximum, SVG rejected. The server decodes and re-encodes the image (removes metadata and polyglot tricks) and stores two sizes behind `IFileStorage`. The logo is intentionally public. `IFileStorage` and a minimal `ClinicAssets` table are built here; Phase 5 builds general attachments on top.
- **Phone:** validated and normalized (Egyptian mobile and landline formats, international format accepted).

### 3.3 Clinic settings page (general data)

- `ClinicProfile`: name, logo, address, phones, email, clinic types and specialties, slug, and (read-only after the first financial entry; confirm in section 6) time zone, currency and rounding rule.
- Only the Owner (reserved permission `clinic.settings.edit`) and the super admin can change it. Another user in the same clinic gets 403.
- Conflict class `LastWriterWins`; the old value goes to the audit log. Slug changes follow the slug rules and keep the old slug redirecting.
- The page is built from an `ISettingsSection` registry. This phase provides the General section; later modules add their own sections (queue, billing, numbering, portal, portal booking) with their own permissions, shown only when their module is on.

### 3.4 User profile page

- Any signed-in user edits their own: full name, phone, avatar (optional), language (ar, en), theme (light, dark, system).
- **Change email:** new address receives a code; the email changes only after the code is typed; a notice goes to the old address. Online only.
- **Change password:** needs the current password; rotates the security stamp and can sign out other devices. Online only.
- Own devices and active sessions with "sign out"; clinic memberships (read-only).
- Policy `profile.self` (authenticated, no permission claim). It can never touch roles, claims or another user.

**Frontend tasks**
- Add-clinic flow, My clinics, clinic switcher, onboarding wizard (stepper, resumable, logo preview and crop, live slug check, phone input), settings page, profile page. Arabic RTL first, light and dark.

**Tests (write these first)**
- Unit: onboarding state machine; slug generation and validation (Arabic names, reserved words, collisions); phone normalization; quota rule; owner-only settings rule.
- Integration:
  - An owner adds clinics up to his quota; the next attempt returns `CLINIC_QUOTA_REACHED`; an owner with no quota can add many; owner B never sees owner A's clinics (404).
  - A new clinic has no modules enabled and the platform dashboard shows it as awaiting setup.
  - Until onboarding is complete every other clinic endpoint returns `ONBOARDING_REQUIRED`; after completion it works.
  - Completing onboarding creates the specialties and the type's defaults; a non-dental clinic gets no dental data.
  - Logo upload rejects wrong type, oversize and SVG and strips metadata.
  - Concurrent requests for the same slug: exactly one wins; an old slug redirects.
  - Settings: owner can edit, another user gets 403, clinic B gets 404 for clinic A's IDs.
  - Profile: name change; email change only after the code; password change needs the current password and revokes other sessions.
  - Audit rows exist for clinic creation, onboarding, settings and profile security changes.
- Frontend: onboarding validation and resume, quota message, settings visibility, profile forms, switcher.

**Phase 3 Integration Gate (modules: Core clinics, Clinical specialty plugins, files, audit, platform dashboard)**
1. Owner O1 (quota 3) signs in with no clinic, adds clinic A, and is blocked from everything except onboarding.
2. O1 enters name, logo, address and phone, selects Dental and Cardiology, picks a slug and finishes. Specialties exist; the dashboard shows "waiting for features".
3. O1 adds clinic B and C; adding a fourth is refused. Switching shows each clinic's own state and none of the others' data.
4. The super admin's owner detail page shows O1's three clinics, each with its status and no modules; the platform dashboard lists them as awaiting setup.
5. Only O1 edits clinic A's general data; the slug rules and redirect work.
6. O1 changes email through the code and changes password; other sessions are revoked.
7. Phases 0 to 2 gates, tenant sweep, policy sweep, admin-host sweep and verification-code sweep pass.

---

## Phase 4: Modules, roles and claims, dashboards, clinic structure

**Goal:** the super admin opens and closes every feature per clinic; the owner sees what he was given, creates roles with claims, invites staff, and has a dashboard host ready for every module; the clinic structure and number blocks exist.

### 4.1 Module registry and assignment

- `IModuleManifest` and the registry (section 2A). `ClinicModules` (clinic, module key, enabled, changed by, changed at, note). Dependency checks as in section 2A (`MODULE_PREREQUISITE_OFF`, `MODULE_HAS_DEPENDENTS`).
- Only the super admin changes modules, from the admin site (`/platform/clinics/{id}/modules`, step-up password, audited). A `ClinicModuleChanged` event goes through the outbox: the permission version stamp is bumped and SignalR tells the clinic group to refresh its menu and permissions.
- The module guard returns `MODULE_DISABLED`. Off means what section 2A says. Background jobs consult the same guard.
- Owner endpoint `GET /modules/mine`: the modules enabled for the active clinic, read-only ("My features" page).
- Every module that exists in this phase (none yet) and every later phase registers its manifest. A placeholder manifest test proves the host builds the menu from manifests only.

### 4.2 Roles, claims and staff users

- `Roles` (per clinic; the system role `Owner` is read-only), `RoleClaims`, `UserClaims` (grant or deny per user). A role is voided, never deleted, and only when no user holds it.
- Code-defined permission catalog (for example `patients.view`, `payments.create`, `discount.apply`, `inventory.manage`, `portal.invite`, `<module>.dashboard.view`). Each permission has a module key, a group, and a `Reserved` flag (owner-only: `clinic.settings.edit`, `roles.manage`).
- `EffectivePermissions` evaluator as defined in section 2. Permissions load server-side and are cached by a version stamp keyed by user and clinic. Role, claim, membership and module changes bump the stamp.
- The assignable catalog for a clinic = permissions of enabled modules, not reserved. The matrix API returns only that list; dormant claims are listed separately.
- **Delegation ceiling:** a grantor grants only claims they hold. The last Owner of a clinic cannot be removed.
- **Staff invitations:** a user with `users.manage` invites by email and picks roles; the invitee activates with the Phase 1 code flow. A user can belong to several of the same owner's clinics, with different roles in each.
- Suggested role templates (Doctor, Assistant, Receptionist) can be cloned as ordinary editable roles.

### 4.3 Dashboard host

- `IDashboardWidget` registry and the widget endpoint as defined in section 2B. `UserDashboardLayout`. Alerts feed.
- Pages built now: owner clinic overview, feature dashboard shell, doctor view shell, owner portfolio (`IOwnerPortfolio`). They show only widgets of enabled modules the user may see. Platform-level widgets exist from Phase 2; the admin app can open any clinic's dashboards in inspect mode.
- This phase ships a small set of core widgets (staff count, roles, devices awaiting approval, onboarding progress, enabled modules) so the host can be tested end to end. Every module phase adds its own.

### 4.4 Clinic structure and numbering

- `Specialties`, `Rooms`, `Doctors` (linked to a user), `DoctorSpecialties`, `DoctorRooms`, `WorkingHours` (with per-doctor overrides), `DaysOff` (date ranges).
- `NumberBlocks` (clinic, device, kind: Receipt or PatientFile, from, to, next, status). Endpoint to reserve a block; numbers are unique per clinic and kind.
- Owner audit viewer: the owner sees only his own clinic's audit.

**Frontend tasks**
- Admin app: module toggles per clinic with dependency messages and a step-up prompt.
- Clinic app: My features, roles and claims matrix, users and invitations, specialties, rooms, doctors, working hours, days off, audit viewer, menu built from the manifest, owner overview dashboard, portfolio page.

**Tests (write these first)**
- Unit: effective permissions (role + grant - deny, intersection with modules, reserved, delegation ceiling, deny wins); last-owner rule; module dependency rules; widget authorization rules; number block arithmetic.
- Integration:
  - Only the super admin toggles modules; the owner gets 403; the owner sees exactly the enabled modules.
  - Enabling a module with a prerequisite off is refused; disabling one with dependents is refused with the list.
  - A claim of a disabled module cannot be assigned and is dormant; re-enabling restores it; the permission cache refreshes and open sessions update their menu.
  - A user with `users.manage` cannot grant a claim they lack; a reserved permission cannot go to a custom role.
  - Every permission in the catalog is enforced (matrix test); audit rows exist for every permission and module change.
  - Dashboard host: a widget of a disabled module returns `MODULE_DISABLED` and disappears from pages; a user without the permission gets 403 for the widget; doctor-scoped widgets show only the doctor's own numbers; the portfolio shows only clinics the owner owns and never another owner's (404).
  - Two concurrent requests for number blocks never overlap.
- Frontend: matrix shows only enabled modules' claims, menu and dashboard hide what is off, step-up prompt on module change.

**Phase 4 Integration Gate (modules: Core roles, module registry, dashboard host, admin site, audit)**
1. The super admin enables a test module for clinic A only. Owner O1's "My features" shows exactly that one; the owner of another clinic sees none.
2. O1 creates a "Receptionist" role: the matrix lists only claims of enabled modules; 3 are granted; a user is invited by email, enters the code, activates and signs in from an unapproved device and is blocked.
3. O1 approves the device; the user can use only the 3 claims.
4. The super admin disables the module: its claims go dormant, its endpoints return `MODULE_DISABLED`, its widgets vanish from the dashboards, and the open menu updates live. Enabling it restores everything. Disabling a module that another enabled module requires is refused.
5. The owner overview and portfolio render for O1 with three clinics, each card aggregate-only; a second owner's clinics never appear.
6. Two devices reserve receipt blocks at the same time; no overlap. The owner audit viewer shows only his clinic.
7. Phases 0 to 3 gates, tenant sweep, policy sweep, module-off sweep, permission-ceiling sweep, dashboard sweep and module-combination sweep pass.

---

## Phase 5: Patients and attachments

**Goal:** complete, searchable, tenant-isolated patient records.

**Backend tasks and data**
- `Patients`: file number (from number block), Arabic and English names (4-part), national ID, date of birth, `AgeIsEstimated`, gender, marital status, nationality, occupation, blood type, photo, referral source, VIP flag, status, email (optional, normalized; verified only by the portal in Phase 10).
- `PatientPhones` (several, WhatsApp flag), `PatientAddresses`, `PatientContacts` (emergency, guardian for minors), `PatientInsurance`.
- Medical: `PatientAllergies` (substance, type, severity, reaction, date), `PatientConditions` (since when, controlled or not), `PatientMedications` (dose, frequency, start/stop), `PatientSurgeries`, `PatientFamilyHistory`, `PatientHabits`, `PatientFlags` (blood thinners, pregnancy and weeks, lactation, pacemaker, diabetes, hypertension, heart disease), `PatientNotes` (typed, pinned).
- **Search:** fast by name or phone. Arabic normalization (alef forms, ya/alef-maqsura, ta marbuta, diacritics). Indexes on the normalized columns.
- **Duplicate detection:** same phone, same national ID, or similar name plus same date of birth.
- **Age** derived from date of birth; estimated age allowed when unknown.
- **Data scoping:** a doctor sees only their own patients; scoping per specialty can be turned on by the owner.
- **Attachments** (general): `Attachments` (owner type and ID, file name, content type, size, storage key, hash, uploaded by, category, status). Storage behind `IFileStorage` (local disk in dev, object storage in production). Allowed-type list, size limit, hash, malware scan hook, authorized download through short-lived signed URLs. Voiding is a status, not deletion.
- **Access log:** `PatientAccessLog` with an actor type (SuperAdmin, Staff, Patient) for super admin reads, patient portal reads, and optionally staff reads of sensitive records.
- **Attachments, building on Phase 3:** `IFileStorage` already exists from the clinic logo; this phase adds the general `Attachments` feature on top of it.
- **Portal hooks (used in Phase 10):** `Attachments.SharedWithPatient` and `PatientNotes.VisibleToPatient`, both default false, plus `IPortalSection` and `IPortalQueries` for the patient summary and medical background.

**Module and dashboard**
- Module keys: `patients` (no prerequisites), `patient-history` (needs `patients`), `attachments` (needs `patients`). Each has its own flag, manifest, permissions (including `<module>.dashboard.view`) and dashboard.
- Dashboard widgets: `patients` (new patients per period, active and VIP counts, age and gender split, duplicate flags); `patient-history` (patients with allergy flags, chronic-condition mix, notes added); `attachments` (uploads per day, storage used, files awaiting scan). Pages `/patients/dashboard`, `/patient-history/dashboard`, `/attachments/dashboard`; the widgets also appear on the owner overview.
- Off: with `patient-history` off the background tabs and the allergy banner disappear; with `attachments` off the upload controls disappear.
- Inspect mode: patient screens are available read-only on the admin site.

**Frontend tasks**
- Patient list with fast search, patient file page (tabs: basics, medical background, notes, attachments), allergy banner always visible, duplicate warning on create, upload with progress.

**Tests**
- Dashboard and module: widgets match source data; each module can be switched off alone and the others still pass.
- Unit: age calculation, estimated age, Arabic normalization cases, duplicate scoring.
- Integration: file numbers unique per clinic under concurrency; search finds Arabic variants; doctor scoping on list and by ID; clinic B cannot fetch clinic A's patient or attachment even with the storage key; type and size rejection; same hash is stored once; super admin read creates an access log row visible to the owner.
- Frontend: allergy banner, duplicate warning, search debounce, upload states.

**Phase 5 Integration Gate**
1. Register a patient in A with allergies, conditions, medications; attach an image and a PDF.
2. Search by name variants and phone finds the patient; clinic B cannot find or download anything.
3. Doctor 1 cannot see doctor 2's patient when scoping is on.
4. Super admin reads the patient; the owner sees the access log entry.
5. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
6. Phases 0 to 4 gates pass.

---

## Phase 6: Scheduling and queue

**Goal:** appointments, queue, priorities, waiting list, live updates.

**Backend tasks and data**
- `AppointmentCategories` (specialty, name, default duration, default price), editable by the doctor.
- `Appointments` (patient, doctor, room, category, start, end, status, priority, push count, source, plan session link for later).
- **Status machine:** Booked, Arrived, In session, Completed, No-show, Cancelled, Late. Only valid transitions are allowed.
- **Validation:** inside working hours, not on a closed day, no double-booking of a doctor or a room.
- **Closed days:** when a day is newly closed, list its appointments so they can be rescheduled.
- **Priority:** Normal, VIP, Rush; needs a claim; audited; the owner sets the VIP jump limit; the rush fee is a billing line later.
- **Queue:** `QueueEntries`. First come first served by default. A patient not marked Arrived within the grace period moves back one place; after the maximum pushes the patient becomes Late or No-show. Grace period and maximum pushes are clinic settings.
- **Waiting list:** a freed slot is offered to waiting patients.
- **Rescheduling options** defined by the owner (after a week, after a month, custom).
- **Doctor timetable:** today's sessions, count, categories.
- **Availability engine:** `IAvailabilityQueries.GetFreeSlots(category, doctor?, from, to)` is a pure function over working hours (with doctor overrides), days off, closed days, existing appointments, room occupancy, category duration (the applied duration from the time engine when that module is on, otherwise the default), slot step and lead time. Staff use it for "next free slot".
- **Online booking hooks (used in Phase 11):** `AppointmentCategories.BookableOnline` and `DoctorBookingSettings.AcceptsOnlineBooking` (both default false), `Appointments.Source` (Staff, Portal), and an `IPortalBooking` contract that books and cancels through the same validation as staff booking.
- **Portal hook:** `IPortalSection` and `IPortalQueries` returning a patient's upcoming appointments (date, time, doctor, specialty, status).
- **SignalR:** one group per clinic; events for arrival, queue change, slot freed.

**Module and dashboard**
- Module keys: `scheduling` (needs `patients`), `queue` (needs `scheduling`).
- Dashboard widgets: `scheduling` (today's appointments by status, no-show and cancellation rate, doctor and room utilization, load of the next 14 days, share by source); `queue` (current queue length, average wait, late arrivals and pushes, waiting-list size, slot offers accepted). Pages `/scheduling/dashboard`, `/queue/dashboard`.
- Off: with `queue` off the queue board, push rules and waiting list disappear and appointments still work; with `scheduling` off modules that need it cannot be on.

**Frontend tasks**
- Calendar by doctor and room, reception queue board, doctor timetable page, waiting list panel, live updates.

**Tests**
- Availability: unit tests for free-slot computation (hour boundaries, overlaps, room clash, doctor overrides, days off, closed days, lead time); integration invariant: every offered slot can be booked and a booked slot is never offered.
- Dashboard and module: widgets match source data; `queue` off leaves appointments working.
- Unit: state machine (all valid and invalid transitions); overlap detection; push-back rules; VIP jump limit.
- Integration: double-booking rejected under concurrent requests; closed-day and working-hour validation; doctor override hours; closing a day lists affected appointments; cancel offers the slot to the first matching waiting patient; a SignalR client from clinic B never receives clinic A's events; doctor sees only their own appointments.
- Frontend: queue board reorders live; status buttons only show valid next states.

**Phase 6 Integration Gate**
1. Patients from Phase 5 book appointments for two doctors in two specialties.
2. They arrive; one is late and gets pushed; a VIP jumps within the limit.
3. A cancellation offers the slot to the waiting list.
4. The owner closes a day; affected appointments are listed and rescheduled.
5. A clinic B client connected to SignalR receives nothing from clinic A.
6. A free-slot search returns slots; booking one succeeds and removes it from the list.
7. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
8. Phases 0 to 5 gates pass.

---

## Phase 7: Visits, clinical records and specialty plugins

**Goal:** the session itself: timer, vitals, diagnoses, prescriptions, forms, dental chart.

**Backend tasks and data**
- `Visits` (appointment, doctor, specialty, room, chief complaint, history of present illness, examination, diagnosis, advice, follow-up date, started, ended, overtime, excused flag and reason).
- **Server-side timer:** start and end timestamps are stored; closing the app loses nothing. If the doctor does not press Done, overtime is recorded.
- `VisitVitals` (blood pressure, pulse, temperature, weight, height, BMI, SpO2, glucose), `VisitDiagnoses` (ICD-10 catalog), `VisitProcedures` (procedure, tooth or site, notes).
- `Prescriptions`, `PrescriptionItems`; printable prescription (QuestPDF) with clinic branding; warn when a drug matches a recorded allergy.
- `LabOrders`, `LabResults`.
- **Clinical forms:** `ClinicalForms` (specialty, JSON Schema, version) and `VisitFormData` (answers). A new specialty needs a form definition, not new tables.
- **Specialty plugin mechanism:** an `ISpecialtyModule` registers forms and optional tables. **Dental plugin:** `ToothRecords` (FDI default, Universal option, permanent and primary teeth, conditions: healthy, caries, filled, crown, missing, implant, root canal, extracted, bridge; per-surface marks). Every change links to a visit; the current state is derived from the latest record.
- **Session screen data:** history, chronic issues, allergies (prominent), notes, and live doctor notes.
- Completing a visit publishes `SessionCompleted` through the outbox.
- **Portal hook:** `Visits.IsVisibleToPatient` (the doctor can hide a visit), and `IPortalSection` plus `IPortalQueries` returning a patient-facing summary (date, start and end time, doctor, procedures, diagnosis, advice) and the prescription PDF. Doctor-private notes are never part of it.

**Module and dashboard**
- Module key: `clinical` (needs `patients`, `scheduling`).
- Dashboard widgets: sessions per day, sessions by category and specialty, top diagnoses and procedures, average session length (raw), overtime sessions. Doctors see their own numbers (scope `Doctor`). Page `/clinical/dashboard`.
- Off: no visits or forms; appointments still work; no `SessionCompleted` event is produced, so billing uses manual charges.

**Frontend tasks**
- Session screen with timer, vitals, diagnosis, procedures, notes, prescription, dental chart (FDI/Universal toggle), generic form renderer for other specialties.

**Tests**
- Dashboard and module: widgets match source data; `clinical` off leaves scheduling and billing working.
- Unit: overtime calculation; FDI to Universal mapping; derived tooth state; form schema validation and versioning.
- Integration: timer survives restart; `SessionCompleted` published exactly once; dental tables are not needed for a non-dental clinic; prescription PDF content check; allergy warning; doctor scoping on visits.
- Frontend: dental chart interactions, form renderer from schema.

**Phase 7 Integration Gate**
1. Appointment goes Arrived, In session, with vitals, a dental chart change, a diagnosis and a prescription, then Completed.
2. `SessionCompleted` appears once in the outbox and is handled once.
3. In clinic B (ophthalmology), the same flow works with a JSON form and no dental data.
4. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
5. Tenant, policy and module-off sweeps pass; Phases 0 to 6 gates pass.

---

## Phase 8: Billing, ledger, cash closing and suppliers

**Goal:** all money rules. This phase has the strictest test-first requirement.

**Backend tasks and data**
- `PriceList` (set by the owner, per category or service).
- `Charges`, `ChargeLines`: session price plus tools used. Tool lines carry cost and charged price; the owner sets the markup (cost 50, charged 60, income 10). The doctor sees the total before charging. Tools are free-text items when Inventory is off.
- **Special permissions:** free session, custom price (lower or higher), discount, extra charge. Each needs a claim and a reason, and is audited.
- `LedgerEntries`: one append-only patient ledger (charge, payment, refund, adjustment, reversal). Balance is derived.
- `Payments`: Cash, Visa, InstaPay, Wallet; partial payments; who received it; device.
- **Receipts:** printable receipt (QuestPDF). Number comes from the device's cached block; the server enforces uniqueness per clinic; skipped numbers are recorded.
- **Refunds:** a reversing entry that needs a claim, reason and method; the original payment is untouched; covers session, plan and deposit.
- **Debts:** outstanding balances with overdue follow-up.
- **Lab work:** `LabWork` (patient, tooth, lab, cost, patient price). Price posts to the ledger, cost posts to expenses.
- **Finance-lite (needed here):** `Suppliers` (type includes lab), `SupplierPayments` (full or partial, remaining balance), `Expenses` (category, amount, method). These live in the Finance module, which has its own flag: billing works with it off (lab cost and expenses are then not recorded and the closing says so).
- **Daily closing:** `DailyClosings` and `ClosingMethodTotals`. Expected cash is cash payments minus cash refunds minus cash expenses; expected totals are also computed for Visa, InstaPay and Wallet. The counted amount is entered and the difference recorded. A closed day is locked; reopening needs a claim and is audited.
- A `SessionCompleted` consumer creates the draft charge.
- **Manual charges:** with `clinical` off, or for anything outside a visit, staff create a charge by hand (patient, items from the price list, or free text) through the same ledger rules.
- **Portal hook:** `IPortalQueries` returning only patient-facing amounts (charged price, paid, remaining). Never cost, markup or lab cost.

**Module and dashboard**
- Module key: `billing` (needs `patients`). The `finance` flag controls the suppliers, supplier payments and expenses built here; the rest of finance arrives in Phase 14.
- Dashboard widgets: income today and in range by payment method, outstanding debts and aging, refunds and discounts, daily closing status and differences. Page `/billing/dashboard`.
- Off and links: `billing` works with `clinical`, `finance` and `inventory` each off; with `finance` off the closing excludes expenses and shows that clearly.

**Frontend tasks**
- Charge screen with tool lines and total, payment dialog (several methods in one payment), receipt print, patient ledger and debts, closing screen showing per-method totals, supplier and expense screens.

**Tests (write these first)**
- Dashboard and module: widgets match ledger totals; a manual charge works with `clinical` off; billing works with `finance` off.
- Unit money rules: total = session price + tools charged; markup math; free session; custom price lower and higher; discount percent and fixed; rounding setting; partial payment leaves the correct balance; refund reverses without touching the original; closing formula; closed day locked.
- Integration: discount without claim rejected; reason required; receipt numbers unique under concurrent devices; skipped numbers recorded; ledger balance equals sum of entries; closing per-method totals match payments; reopening a closed day is audited; lab work posts both price and cost; supplier balance after partial payments; clinic isolation of every money table.
- Frontend: charge total updates live; permission-gated discount controls.

**Phase 8 Integration Gate**
1. Visit completes (Phase 7), a charge is drafted with tools and a discount, a receipt number is issued.
2. The patient pays partly in cash and partly by InstaPay; a debt remains.
3. A refund reverses part of a payment; the original stays; the ledger balance is correct.
4. The day is closed with per-method totals (cash, InstaPay, wallet, Visa) and a recorded difference; the day is locked.
5. A lab order posts a patient price and a lab expense; a supplier is partly paid.
6. With Inventory off the whole flow works with free-text tools.
7. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
8. Phases 0 to 7 gates pass.

---

## Phase 9: Treatment plans

**Goal:** multi-session treatments with sessions spread over different periods.

**Backend tasks and data**
- `TreatmentPlans` (patient, doctor, name, mode: Fixed total or Per session, total, status).
- `PlanSessions` (plan, sequence, target date, earliest and latest allowed date, interval from previous session, linked appointment, status).
- `PlanPaymentSchedule`: upfront part and installments, applied to the plan balance.
- **Fixed total mode:** sessions inside the plan carry no charge of their own; materials are included in the plan price with their cost still tracked.
- **Per session mode:** each session posts its own charge.
- Progress, remaining sessions and remaining balance per plan.
- Delay handling: when a session slips, later target dates are recalculated within their allowed windows.
- **Portal hook:** `TreatmentPlans.ShowCostToPatient` (set by the doctor; the default comes from the clinic portal settings) and `IPortalSection` plus `IPortalQueries` for plan progress, session dates and times, and patient-facing costs.

**Module and dashboard**
- Module key: `treatment-plans` (needs `patients`, `scheduling`, `billing`).
- Dashboard widgets: active plans, completion percentage, delayed sessions, installments due and overdue, plan revenue. Page `/treatment-plans/dashboard`.
- Off: no plan screens; scheduling and billing are unaffected.

**Frontend tasks**
- Plan builder (sessions with intervals), plan progress page, suggested next appointment date, installments view.

**Tests**
- Dashboard and module: widgets match source data; `treatment-plans` off leaves scheduling and billing working.
- Unit: date window and interval calculation; delay recalculation; remaining balance in both modes; installment application.
- Integration: fixed-total sessions create no charge; per-session sessions create charges; completing a plan session updates progress; a deposit refund reverses correctly; the closing includes the upfront payment.
- Frontend: plan builder validation.

**Phase 9 Integration Gate**
1. A 4-session plan spread over weeks, with a fixed total, an upfront part and installments.
2. Appointments are suggested from the windows; one session slips and later targets move.
3. A per-session plan posts its own charges.
4. Daily closing includes the upfront and installment payments; a deposit refund works.
5. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
6. Phases 0 to 8 gates pass.

---

## Phase 10: Patient portal (public clinic page and read-only record)

**Goal:** each clinic with the `portal` module on has a public URL, and its patients can sign in and see their own record. Booking comes in Phase 11.

**Depends on:** `portal` needs only `patients`. Its sections appear only for modules that are on (patient history, scheduling, clinical, billing, treatment plans). Built after Phases 5 to 9 so those sections can be tested for real.

**URLs (path-based, same origin as the clinic site)**
- `/c/<slug>`: public clinic page (name, logo, address, phone, "patient login"). `/c/<slug>/login`, `/activate`, `/forgot-password`, `/reset-password`.
- `/c/<slug>/portal/...`: signed-in pages (home, medical record, sessions, treatment plans).
- The clinic is resolved from the slug by `ITenantResolver` through a Core contract (`IClinicDirectory`), cached. An old slug redirects to the current one. An unknown slug and a clinic with the portal off both return the same 404, so nothing leaks about which clinics exist.

**Backend tasks and data (schema `portal`)**
- `PatientAccounts` (clinic, patient, email, email confirmed, password hash, status, last login). Separate realm, cookie and policies from staff. A patient never holds a staff claim.
- **Enrollment (confirm in section 6; recommended default):**
  - A staff member with `portal.invite` presses "Invite to portal" on the patient file. The patient needs an email on file and a recorded consent (`PortalConsents`: who accepted, when, terms version).
  - `IAccountInvitations` (Phase 1) emails a 6-digit code; the patient enters email, code and a password at `/c/<slug>/activate`.
  - A patient with no email gets a printed one-time activation code (10 characters, short expiry, limited attempts, bound to the file number). The patient enters it with the date of birth, then registers an email that is confirmed by an emailed code.
  - Self-registration with only a file number and date of birth is not allowed.
  - A minor's account is a guardian account tied to the guardian contact on the file (confirm).
- **Login, forgot password, email confirmation:** same code engine and rules as staff (Phase 1), patient realm. Lockout, rate limit, generic errors.
- **Portal sections registry:** each module contributes an `IPortalSection` and its `IPortalQueries` in its `Contracts`, with purpose-built DTOs. The Portal module only composes them; it never reads another module's tables and never receives entities. The portal home lists only sections of enabled modules.
- **What the patient sees:**
  1. Summary: name, file number, age, blood type (`patients`).
  2. Medical background (`patient-history`): allergies always at the top, conditions, current medications, surgeries. Notes appear only if marked visible to the patient; doctor-private notes never appear.
  3. Sessions (`scheduling`, `clinical`): upcoming (date, time, doctor, specialty, status) and ended (date, start and end time, doctor, procedures, diagnosis, advice, prescription as PDF, shared attachments). A completed visit is visible unless the doctor hides it (confirm default).
  4. Treatment plans (`treatment-plans`): name, status, total, completed and remaining session counts, each session's sequence, target date or window, actual date and time, status. Costs (plan total or per-session cost, paid, remaining, installments, from `billing`) are shown only when the doctor or clinic allows it for that plan.
  5. Never shown: tool cost, markup, lab cost, internal notes, other patients' data, other clinics' data.
  6. Not in this phase: booking (Phase 11), payments, receipts, editing data, messaging.
- **Security rules:**
  - The patient ID comes only from the portal cookie. No portal endpoint accepts a patient ID. Resource IDs (visit, plan, attachment) must belong to that patient, otherwise 404 (same clinic or another).
  - Every portal read of medical data writes a `PatientAccessLog` row with actor type Patient, visible to the owner. Logins and failed logins are logged.
  - All portal responses: `Cache-Control: no-store`, `X-Robots-Tag: noindex`. No patient data in URLs, logs or analytics. The service worker (Phase 15) never caches portal data.
  - Shorter idle timeout than staff, no "remember this device". Prescription PDFs and attachments download through short-lived signed URLs after authorization.
  - The owner or super admin can disable a patient's portal access; audited.
  - Clinic `Suspended`: portal off. Clinic `ReadOnly`: portal still reads (confirm).
- **Portal hooks in earlier modules:** `Attachments.SharedWithPatient`, `PatientNotes.VisibleToPatient`, `Visits.IsVisibleToPatient`, `TreatmentPlans.ShowCostToPatient`, and `IPortalSection` plus `IPortalQueries` in Patients, Patient history, Scheduling, Clinical, Billing and Plans. Flags live in the module that owns the data.
- **Clinic portal settings** (an `ISettingsSection`, owner only): show prices by default, show doctor names, default visit visibility, terms text, welcome message.

**Module and dashboard**
- Module key `portal` (needs `patients`). Permissions: `portal.invite`, `portal.manage`, `portal.dashboard.view`.
- Dashboard widgets: accounts invited, active and disabled; logins this week; failed logins; patients who viewed a record this month; access-log summary. Dashboard page `/portal/dashboard`.
- Off: public page returns 404, portal API returns `MODULE_DISABLED`, existing accounts and data are kept.

**Frontend tasks**
- A separate lazy route tree and build chunk (`web/apps/clinic/src/portal`) with no staff code. Mobile-first, Arabic RTL first, light and dark, clinic logo and name in the header, no sidebar.
- Pages: public clinic page, login, activate, forgot and reset password, home, medical record with allergy banner, sessions list and detail, treatment plan with progress and per-session list.
- Staff side: a "Portal" tab on the patient file (not invited, invited, active, disabled; invite, resend, disable; print activation code), "visible to patient" toggles on visits, attachments and notes, a cost-visibility toggle on plans.

**Tests (write these first)**
- Unit: activation code generation, expiry and attempt limit; visibility rules (private notes excluded, price hidden unless allowed); a DTO shape test that fails if any portal DTO has a cost, markup or internal-note field; slug resolution and redirect; sections appear only for enabled modules.
- Integration:
  - A patient cannot read another patient's visit or plan in the same clinic (404); a patient of clinic B using clinic A's IDs gets 404.
  - A staff cookie is rejected on portal routes and a portal cookie on staff routes.
  - No portal route accepts a patient ID (reflection test).
  - Portal off: landing 404, API `MODULE_DISABLED`, staff app unaffected. A section's module off removes only that section.
  - Invitation without email or consent is rejected; the activation code locks after the attempt limit; codes are single use.
  - Hiding a visit or turning prices off changes the patient's view at once.
  - Access-log rows are written and visible to the owner.
  - Response headers are correct; a log scrape of seeded patient names finds nothing; signed URLs expire; an old slug redirects.
  - Dashboard widgets match the source data.
- Playwright: patient flow on a phone-size screen in Arabic RTL (activation, record, sessions, plan).

**Phase 10 Integration Gate (modules: Portal, Patients, Scheduling, Clinical, Billing, Plans, Core, email)**
1. Clinic A (`portal` on, slug `smile-dental`): staff invite a patient by email; the code email is captured; the patient enters the code and a password and signs in at `/c/smile-dental`.
2. The patient sees allergies, conditions and medications; an ended session with time, doctor, diagnosis and prescription PDF; an upcoming session; and a 4-session treatment plan with completed and remaining counts, dates, times and costs. No response contains tool cost or markup.
3. The doctor hides one visit and turns off cost display for the plan; the patient's view updates.
4. The patient cannot reach any other patient's data in clinic A or any data of clinic B.
5. The super admin turns `portal` off: the public page returns 404 and the API returns `MODULE_DISABLED`; scheduling, billing and the staff app keep working. Turning `treatment-plans` off removes only the plans section.
6. The owner sees the patient's access-log entries; a patient password reset with a code works and revokes the patient's sessions.
7. Portal dashboard widgets match the data; all earlier gates (Phases 0 to 9) pass; portal isolation sweep passes.

---

## Phase 11: Patient portal booking

**Goal:** a signed-in patient sees the clinic's free session times and books, and cancels, their own appointments.

**Depends on:** `portal-booking` needs `portal` and `scheduling`. Uses the availability engine and booking contract built in Phase 6.

**Backend tasks and data**
- **Free times:** `GET /portal/availability` (authenticated patient) takes a category (only those marked `BookableOnline`), optionally a doctor (only doctors with `AcceptsOnlineBooking`) and a date range, and returns free slots grouped by day in the clinic's time zone. It comes from `IAvailabilityQueries`, the same engine staff use, so an offered slot is always bookable. It returns only free times: no other patient's data, no room details. Prices appear only if the clinic's price-visibility setting is on.
- **Booking:** `POST /portal/appointments` (idempotent through `OperationId`) goes through `IPortalBooking`, which runs the same validation as staff booking (working hours, closed days, days off, doctor and room availability, no double booking). The appointment gets `Source = Portal`, normal priority, and the patient taken from the cookie, never from the request. When the slot was just taken the answer is `SLOT_TAKEN` with a fresh list.
- **Booking rules** (clinic settings, owner only, an `ISettingsSection`):
  - Mode: `AutoConfirm` (default, confirm in section 6) or `RequiresApproval`.
  - Minimum lead time, maximum days ahead, slot step, maximum active future bookings per patient (default 2), maximum bookings per day.
  - Cancellation cutoff (default 12 hours before the start).
  - Automatic online-booking block after a number of no-shows (default 3); staff can lift it.
- **Approval mode:** extends the Phase 6 status machine with `Requested` and `Declined`. A request holds the slot until staff approve, decline, or it expires after a set number of hours (default 12). Reception sees a "requests" panel and a SignalR notification; the patient sees the status in "My sessions".
- **Cancel:** the patient cancels their own upcoming appointment before the cutoff. The slot is freed and, if `queue` is on, offered to the waiting list. Reschedule means cancel and book again.
- **No reminders.** The confirmation is shown on screen and in "My sessions". An email confirmation is an open item (section 6).
- **Abuse protection:** per-account and per-IP rate limits on availability and booking; bot protection is an open item.
- **Staff side:** appointments show a "portal" badge; per-doctor "accepts online booking" and per-category "bookable online" toggles; a settings section for the rules; a block and unblock action on the patient file.

**Module and dashboard**
- Module key `portal-booking` (needs `portal`, `scheduling`). Permissions: `portalbooking.manage`, `portalbooking.requests.review`, `portalbooking.dashboard.view`.
- Dashboard widgets: portal bookings per day, cancellations, pending requests, no-shows of portal bookings, slot utilization, share of all bookings that come from the portal. Page `/portal-booking/dashboard`.
- Off: availability and booking endpoints return `MODULE_DISABLED`, the portal shows no booking section, staff scheduling is unaffected, existing portal bookings stay as normal appointments.

**Frontend tasks**
- Portal: choose category and optional doctor, pick a day, see slots, confirm, see the booking in My sessions, cancel. Mobile-first, Arabic RTL, light and dark.
- Staff: requests panel, badges, settings section, patient block action.

**Tests (write these first)**
- Unit: booking rules (lead time, horizon, per-patient limits, cancellation cutoff, no-show block); extended state machine (`Requested` -> `Booked` or `Declined` or expired).
- Integration:
  - Offered-versus-bookable invariant: every slot the portal is offered can be booked, and a booked slot is never offered again.
  - Two patients book the same slot at once: exactly one wins and the other gets `SLOT_TAKEN`.
  - The patient ID is always the cookie's; a request with another patient's ID is ignored or rejected; patient A cannot cancel patient B's appointment (404).
  - Categories or doctors not enabled for online booking never appear; a blocked patient is refused with a clear code.
  - Cancellation before the cutoff frees the slot and offers it to the waiting list; after the cutoff it is refused.
  - Approval mode: a request holds the slot, approval books it, decline or expiry frees it.
  - Module off: portal booking endpoints refuse, staff scheduling unaffected.
  - Dashboard widgets match the source data.
- Playwright: patient books and cancels on a phone-size screen in Arabic RTL; reception sees the portal badge and notification.

**Phase 11 Integration Gate (modules: Portal booking, Portal, Scheduling, Queue, Patients, Core)**
1. Clinic A enables `portal-booking`. The patient from the Phase 10 gate searches free slots for a bookable category and doctor and sees only free times.
2. The patient books one; it appears at reception with the portal badge; the slot disappears from the list; the patient sees it under upcoming sessions.
3. Two patients race for the last slot: one wins, one gets `SLOT_TAKEN`.
4. The patient cancels before the cutoff and the slot is offered to the waiting list; cancelling after the cutoff is refused.
5. Switching the clinic to approval mode produces a request that reception approves; a declined or expired request frees the slot.
6. After the no-show limit the patient is blocked from online booking and staff can lift it.
7. The super admin turns `portal-booking` off: the booking section disappears and staff scheduling is unaffected. Clinic B sees nothing of clinic A's slots.
8. All earlier gates (Phases 0 to 10) pass; portal isolation sweep passes.

---

## Phase 12: Inventory module (flagged per clinic)

**Goal:** stock, purchasing and automatic deduction, switchable by the super admin.

**Backend tasks and data**
- `Items` (name, unit, cost, low-stock threshold, specialty, location), `ItemBatches` (batch number, expiry, quantity left), `StockMovements` (purchased, used, adjusted).
- `PurchaseInvoices`, `PurchaseLines`: pack-based purchasing (a box of 30), pack size and quantity editable during purchase, unit cost = box price ÷ units. Suppliers come from the Finance contract.
- Automatic deduction when tools are used in a session (consumer of `SessionCompleted`), using first-expiry-first-out.
- Low-stock alerts and near-expiry alerts through SignalR. A setting decides whether negative stock is allowed or blocked.
- Supplier account statement (bought, paid, remaining) using Finance contracts.
- When the module is off for a clinic: screens hidden, events ignored, no deduction, Billing unaffected.

**Module and dashboard**
- Module key: `inventory` (no prerequisites).
- Dashboard widgets: low stock, near expiry, stock value, usage by item and category, negative-stock events. Page `/inventory/dashboard`; low-stock and expiry alerts also feed the owner alerts feed.
- Off: no deduction, no screens; billing and clinical are unaffected. Switching it on later starts clean.

**Frontend tasks**
- Inventory list, purchase invoice entry with packs, batch and expiry view, movement history, alerts panel, supplier statement.

**Tests**
- Dashboard and module: widgets match stock data; alerts reach the owner alerts feed.
- Unit: unit cost math and rounding; first-expiry-first-out; pack edits.
- Integration: deduction exactly once per session; negative stock policy; low-stock alert fires once at the threshold; expiry alert; module off ignores `SessionCompleted` and Billing still works; module switched on later starts clean; clinic isolation.
- Frontend: purchase form recalculates unit cost.

**Phase 12 Integration Gate**
1. Buy a box of 30 (pack edited), stock increases, supplier owes the remainder.
2. A visit uses tools; stock drops once, the charge uses the markup; a low-stock alert arrives.
3. Clinic A has Inventory off: the same visit bills fine with no stock changes.
4. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
5. Phases 0 to 11 gates pass.

---

## Phase 13: Average-time engine

**Goal:** learn each doctor's real time per session category and adjust durations.

**Backend tasks and data**
- `SessionDurations` (visit, doctor, category, raw minutes, excused flag and reason).
- `CategoryTimeStats` (doctor, category, sample count, trimmed average, suggested duration, applied duration, engine on or off per doctor).
- A Hangfire job and an event consumer update stats after each completed session.
- Rules: minimum sample size before anything changes; outlier trimming; a maximum percentage the duration may move per update; excused sessions are excluded but the raw data is kept; the doctor switches the engine on or off; the owner sees suggested versus applied durations.
- Scheduling reads the applied duration through a contract.

**Module and dashboard**
- Module key: `time-engine` (needs `scheduling`, `clinical`).
- Dashboard widgets: suggested versus applied durations per doctor and category, sample counts, adoption rate. Page `/time-engine/dashboard`.
- Off: scheduling uses the default durations and the availability engine is unchanged.

**Frontend tasks**
- Doctor settings (engine toggle), suggested versus applied view, excused-session control on the visit.

**Tests**
- Dashboard and module: widgets match source data; the availability engine uses the applied duration only when this module is on.
- Unit: trimmed average, minimum sample, step limit, excused exclusion.
- Integration: engine off changes nothing; new bookings use the applied duration; a single extreme case does not distort the average; job is idempotent for the same session.

**Phase 13 Integration Gate**
1. A doctor completes many sessions of one category with one extreme case; the suggested duration moves within limits.
2. A new appointment for that category uses the applied duration; an excused session is ignored.
3. Engine switched off: new bookings keep the old duration.
4. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
5. Phases 0 to 12 gates pass.

---

## Phase 14: Clinic finance, reports, exports and Excel import

**Goal:** fixed costs, salaries, dashboards, exports.

**Backend tasks and data**
- `FixedCosts` (rent, electricity, water, others; amount, recurrence, paid-from method) feeding the daily and monthly view.
- `Employees`, `Payroll`, `PayrollAdjustments` (advances, deductions, bonuses). Doctor commission is removed.
- Income versus expenses overview (income from the ledger, expenses from `Expenses`, fixed costs and salaries).
- Doctor dashboard (income, costs, what the doctor pays for), sessions per day and per category, per-method totals. Heavy queries use Dapper.
- Exports: Excel (ClosedXML) and PDF (QuestPDF).
- Excel import: downloadable template with exact headers and an example row, validation with a row-by-row error report shown before saving. Patients first, then inventory items and suppliers.

**Module and dashboard**
- Module keys: `finance` (completes the part started in Phase 8: fixed costs, payroll, income versus expenses), `reports` (report center and exports), `excel-import` (needs `patients`). Report sections and import types appear only for enabled modules.
- Dashboard widgets: `finance` (income versus expenses, supplier balances, fixed costs due, payroll summary); `reports` (exports run, scheduled reports); `excel-import` (import history, rejected rows). Pages `/finance/dashboard`, `/reports/dashboard`, `/excel-import/dashboard`.
- Off: each can be off alone without breaking another module.

**Frontend tasks**
- Finance screens, dashboards, export buttons, import wizard.

**Tests**
- Dashboard and module: widgets match ledgers to the cent; report and import options follow the enabled modules.
- Unit: recurrence expansion, payroll totals, income minus expenses.
- Integration: report totals equal ledger and expense sums; exports contain correct totals; import rejects bad rows without saving anything; a super admin cross-clinic report; clinic isolation of every report.
- Frontend: import wizard error display.

**Phase 14 Integration Gate**
1. A simulated month: visits, payments, refunds, expenses, fixed costs, salaries.
2. The income versus expenses report matches the sum of the underlying ledgers to the cent.
3. The Excel and PDF exports match the screen totals.
4. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
5. Phases 0 to 13 gates pass.

---

## Phase 15: Offline PWA and sync

**Goal:** installable app that saves work offline and syncs when the internet returns.

**What works offline**

| Action | Offline? | Sync behavior |
|---|---|---|
| View today's schedule and today's patients | Yes | Cached when online |
| Register a new patient | Yes | Client GUID; duplicate review flag on sync |
| Check-in, status and queue changes | Yes | Server validates and resolves clashes |
| Session notes, vitals, diagnosis, prescription | Yes | Append-only |
| Session timer | Yes | Local timestamps checked against server time |
| Tools used, charge, discount (with claim) | Yes | Idempotent queued charges |
| Take payment and print receipt | Yes | Number from the cached block |
| Book or move an appointment | Yes, with a warning | `ServerValidated`; may create a `SyncConflict` |
| Upload images, files, documents | Queued | Stored in the browser, uploaded when online |
| Treatment plan create or edit | Yes | Sessions sync as new records; later targets recalculated |
| Close the day, refunds, price list, roles and claims, add clinics, super admin screens, cross-clinic reports, onboarding, module changes, password and email changes, all Portal endpoints (including booking) | No | Online only |

**Module and dashboard**
- Module key: `offline-sync` (no prerequisites). With it off for a clinic, the sync endpoints refuse and the app works online only.
- Dashboard widgets: registered devices, last sync per device, pending operations, open conflicts. Page `/offline-sync/dashboard`; conflicts also feed the owner alerts feed.

**Tasks**
- **PWA:** manifest, installable, Workbox precache of the app shell, update prompt. The service worker never caches `/c/*/portal` pages or any `/api/portal` response.
- **Local store (Dexie):** only what is needed (today's schedule, those patients, price list, categories and applied durations, number blocks). Sensitive fields encrypted with WebCrypto. Local data wiped on logout or when the device is revoked.
- **Outbox:** ordered queue of operations with `OperationId`, retry with backoff, and per-entity ordering.
- **Endpoints:** `POST /sync/push` (batch of operations), `GET /sync/pull?cursor=` (changes since cursor, clinic-scoped), `POST /sync/number-blocks`.
- **Number blocks:** prefetch a block when online (size is a clinic setting); refill at a low-water mark; if a block runs out offline, issue a provisional number that is finalized on sync.
- **Auth offline:** a device must have logged in online first; an offline session has a maximum length (setting); expired sessions require reconnecting.
- **Clock skew:** store device time and server receive time; flag large skew.
- **UI:** connectivity indicator, pending-changes count, conflict inbox, "pending upload" badge on files.

**Tests**
- Dashboard and module: widgets match device and conflict data; `offline-sync` off refuses sync calls.
- Unit: outbox ordering, retry and backoff, local encryption round trip.
- Integration: replaying the same operation twice creates one payment; a batch with a failing middle operation leaves the rest correct; two devices offline book the same slot and one lands in the conflict inbox; stale settings edit resolves last-writer-wins with the old value in the audit log; online-only action through sync returns the right error; pull returns only that clinic's changes; revoked device cannot push.
- Playwright (`setOffline`): register patient, check in, complete a session, take payment, go online, assert server state and unique receipt numbers; revoke a device while offline and verify the local wipe on reconnect; install and reload while offline.

**Phase 15 Integration Gate (offline day simulation)**
1. Two devices in clinic A go offline: one registers patients and takes payments, the other runs sessions with tools.
2. Both reconnect. All records sync once, receipt numbers are unique, stock is deducted once, the ledger and closing totals are correct, and one slot clash appears in the conflict inbox.
3. Clinic B is unaffected and cannot see any of it. A portal booking that took a slot during the offline period is respected, and the conflicting offline booking lands in the conflict inbox.
4. Dashboard widgets of this phase's modules match the source data; with a module off its widgets disappear and no module that does not depend on it breaks.
5. Phases 0 to 14 gates pass.

---

## Phase 16: Hardening and release

**Tasks**
- **Security pass:** OWASP ASVS checklist, CSRF protection, security headers, rate limiting, lockout, secrets management, dependency audit.
- **Isolation fuzzing:** automated cross-tenant requests for every route, including SignalR, files, exports and sync.
- **Performance:** k6 load tests (target values agreed with Ahmed), query plans for search and reports, index review.
- **Backups:** managed database backups plus point-in-time restore; run a restore drill; verify files storage backup.
- **Operations:** monitoring, alerts, structured logs, deployment pipeline, migration rollback plan, environment configuration.
- **Data export** for a clinic on request.
- **Pilot:** UAT with one real clinic; bug triage.

**Tests**
- Full module-combination run: every module off on its own, and the minimal prerequisite set of each module.
- Dashboard and portal load tests: widget endpoints stay within their time budget; one slow widget never slows the page.
- Isolation fuzzer, permission matrix replay, k6 thresholds, restore drill checklist, full Playwright regression in Arabic and English.

**Phase 16 Integration Gate (release gate)**
1. The full cumulative gate suite (Phases 0 to 15) passes on a production-like environment.
2. The isolation fuzzer finds zero cross-clinic leaks.
3. A restore drill brings back the database and files with correct data.
4. The pilot clinic completes a full day (booking, visits, billing, closing) without blocking issues.

---

## 6. Open items to confirm with Ahmed (do not decide alone)

**Carried over**
- Queue defaults: grace period, maximum pushes, VIP jump limit.
- Receipt number block size and low-water mark; what to do if a block runs out offline (this plan assumes provisional numbers).
- Subscription expiry behavior (read-only after grace) and the grace length.
- Whether staff reads of patient records should also be logged, or only super admin and patient reads.
- Negative stock policy default.
- Library licenses (QuestPDF and any assertion library) before versions are locked.
- Data protection requirements for medical data in Egypt, before launch. The patient portal and portal booking make this more important.

**Super admin site**
- **How the hourly code is produced.** Default in this plan: a time-based code from an authenticator app with a 1-hour period. The alternative is a code the server emails to you at each sign-in. Some authenticator apps ignore a custom period, so the app must be checked before Phase 2 is locked.
- Recovery codes and the break-glass command (default: 10 single-use recovery codes plus the server command).
- Optional IP allowlist for the admin host (default: off, supported by configuration).

**Accounts and email**
- Email provider and sender domain (SMTP or a transactional email service, with SPF and DKIM). Needed before the Phase 1 gate can run on a real environment.
- Code settings (defaults in Phase 1: 6 digits, 5 attempts, invitation valid 72 hours, other codes 15 minutes, resend after 60 seconds), password rules (recommended: at least 10 characters plus a common-password check) and lockout (recommended: 5 attempts, 15 minutes).
- Two-factor authentication for owners (optional per owner; the super admin already has it).

**Owners, clinics, modules**
- Clinic quota per owner (default: no limit unless you set one) and whether a new clinic needs your approval before the owner can finish onboarding (default: no approval, but no modules are on until you enable them).
- Trial period or subscription start for a clinic an owner creates (default: you set subscription dates yourself).
- Which modules are switchable and their prerequisites (defaults in section 2A).
- The list of reserved owner-only permissions (default: clinic settings and roles management).
- Whether time zone, currency and rounding rule lock after the first financial entry (default: lock).
- Slug changes after onboarding (default: the owner may change it and the old slug keeps redirecting forever).
- Whether you get an email when an owner adds a clinic (default: dashboard notice only).

**Patient portal and booking**
- How patients enroll (default: staff invite by email with a typed code, or a printed one-time code plus date of birth when the patient has no email) and guardian accounts for minors.
- Default visibility: completed visits visible unless the doctor hides them; prices hidden unless the clinic or doctor turns them on.
- Booking mode (default: `AutoConfirm`, with `RequiresApproval` as a clinic setting) and the default limits in Phase 11 (lead time, horizon, 2 active bookings, 12-hour cancellation cutoff, block after 3 no-shows).
- Whether a booking confirmation email is sent (the plan allows only account emails today; default: on-screen confirmation only).
- Bot protection on portal login and booking (CAPTCHA or not).
- Portal behavior when the clinic is `ReadOnly` or `Suspended` (default: read-only still works, suspended is off).
- Whether patients may later see receipts or pay online. Not in this plan.
