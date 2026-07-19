# Ticket Backend — Agent Instruction Manual

> Single source of truth for AI agents on **this** Ticketing System repo.
> Derived from project Documents (DESIGN-v6, Backend Guide, FE Phases) — not a generic Elay copy.

**Mandatory before shell or code search:** `.cursor/rules/00-enforcement-gate.mdc`, `RTK.md`, `.cursor/rules/rtk.mdc`.

---

## 0. Document map (read the right file)

| Document | Role | When |
|----------|------|------|
| `Documents/TICKETING-SYSTEM-DESIGN-v6.md` | Business rules, ERD, Auto-Assign, Reassign, Reopen, roles | Before domain logic |
| `Documents/TICKETING-SYSTEM-API-SCENARIOS-v2.md` | Per-endpoint request/response/status | Before any controller action |
| `Documents/openapi.json` | Machine-readable contract | Keep in sync; never invent routes |
| `Documents/TICKETING-SYSTEM-IMPLEMENTATION-PHASES-v1.md` | FE phase order & UI DoD | Mirror same phase numbers |
| `Documents/TICKETING-SYSTEM-BACKEND-IMPLEMENTATION-GUIDE-v1.md` | BE build order, DoD, FE sync gates | Day-to-day coding |

### Golden rule — no FE/BE drift

1. Routes, DTOs, enums, status codes = `openapi.json` + API-SCENARIOS-v2.
2. Business behavior = DESIGN-v6 (especially §§3, 6, 7, 8).
3. Phase order = FE phases **0 → 6**. Backend Phase N must be demo-able in Swagger before FE Phase N.
4. Need a change? Update DESIGN / SCENARIOS / openapi **first**, then code.

---

## Pre-task protocol (before any code)

```
Phase I am on: [B-Prep | 0–6]
Sections I will read: [...]
Files I will CREATE: [...]
Files I will MODIFY: [...]
Assumptions I am making: [...]
```

| Task type | Required reading |
|-----------|------------------|
| Phase / new endpoint | This file + Backend Guide that phase + matching SCENARIOS + DESIGN sections |
| Domain (assign/reassign/reopen) | DESIGN §§6–8 + unit-test expectations in Backend Guide |
| Bug fix | §11, then `verify-feature` |
| Persistence / EF | §6; report migration command (do not generate unless asked) |

---

## Critical overrides (never violate)

1. **Contract first** — camelCase JSON; enums as **strings** in API; status codes from openapi/scenarios.
2. **Phase gate** — do not ship later-phase routes early (except unblock tests, still contract-compliant).
3. **Soft delete** — `IsDeleted` / deactivate via `PATCH .../deactivate`; never hard `DELETE` / `Remove()` for domain rows.
4. **Multi-tenancy** — Shared DB + filter `ProviderId` / `ClientId` from JWT. Wrong tenant → **404**. Same tenant, wrong ownership → **403**.
5. **Chat rules** — only original `Requester` sends messages; other Client Requesters read-only; `ClientManager` never enters chat.
6. **Auto-Assign** — run once on Create Ticket (DESIGN §6). Reopen does **not** re-run assign.
7. **No SignalR** in MVP — polling only.
8. **Out of MVP** — `TicketAssignmentLogs`, general AuditLogs, multi-requester chat write, re-run Auto-Assign on Reopen.
9. **CancellationToken** on all async I/O; prefer primary constructors.
10. **Never commit a broken build.**
11. **Two-stage full cycle** — when the user explicitly approves the all-remaining-phases workflow, implement, business-verify, and commit each phase in order without routine pauses. After all development commits, require separate explicit approval before real Postman/database testing.

---

## 1. Glossary (DESIGN §1 — use these names only)

| Term | Meaning |
|------|---------|
| **Provider** | Nice Mind customer org; provides support |
| **Client** | Org receiving support from a Provider |
| **SuperAdmin** | Platform admin (Nice Mind) |
| **ProviderManager** | Manages Agents + Clients for one Provider |
| **Agent** | Support staff; handles tickets |
| **ClientManager** | Supervisory only; no ticket chat |
| **Requester** | Creates tickets; only creator sends chat messages |

Do not invent synonyms (`Company`, `TenantUser`, etc.).

---

## 2. Roles seed (DESIGN §5.3)

```
1 SuperAdmin
2 ProviderManager
3 Agent
4 ClientManager
5 Requester
```

**User constraint:** Exactly one of `ProviderId` / `ClientId` set — except SuperAdmin (both null). Roles 2–3 → ProviderId; roles 4–5 → ClientId.

**JWT claims (minimum):** `sub`/`userId`, `roleName`, `providerId?`, `clientId?`.

---

## 3. Phase sync matrix (Backend Guide §1)

Do not mark BE Phase Done until FE can consume it.

| Phase | Backend delivers | FE consumes | Shared DoD |
|-------|------------------|-------------|------------|
| **B-Prep** | Solution, DI, JWT/CORS/ProblemDetails, Roles seed, Swagger | — | `/api/v1` + empty area groups |
| **0** | Auth, Profile, Layout, Notifications | Login, layout badge, profile | Login + refresh + 400 field errors |
| **1** | `/admin/*` Plans, Providers, Subscriptions | SuperAdmin screens | Plan → Provider → Subscription |
| **2** | `/pm/*` Agents, Clients, dashboard | ProviderManager screens | ≥1 active Agent + ≥1 Client |
| **3** | `/requester/*` create + chat + reopen | Requester screens | Ticket created; Auto-Assign; chat rules |
| **4** | `/agent/*` chat, status, reassign, notes | Agent screens | Reassign + Resolve + notes isolation |
| **5** | `/cm/*` requesters + read-only tickets | ClientManager screens | No ticket write/chat for CM |
| **6** | CORS, file limits, seed, E2E | Polling, cross-role E2E | Full cycle Pass |

**Recommended start:** B-Prep → Phase 0.

**Manual smoke path for Phase 1+:** Create Plan → Create Provider → Create Subscription.

---

## 4. Stack & layout (Backend Guide §0.2–0.3)

| Topic | Decision |
|-------|----------|
| Runtime | ASP.NET Core **.NET 10** |
| Architecture | Clean Architecture |
| Auth | JWT Bearer (`accessToken` + `refreshToken` rotation) |
| Errors | `ProblemDetails` / `ValidationProblemDetails` |
| Base path | `/api/v1` |
| Chat | Polling only |

```
src/Ticket/
  Ticket.Api/             Controllers, Program.cs, middleware, Swagger
  Ticket.Application/     Commands/Queries, validators, DTOs, interfaces
  Ticket.Domain/          Entities, enums, domain services (AutoAssign)
  Ticket.Infrastructure/  EF Core, JWT, file storage, email/SMS stubs
tests/
  Ticket.UnitTests/       Auto-Assign, Reopen, Reassign
  Ticket.IntegrationTests/
```

**Controllers (fixed prefixes):**

| Controller | Prefix | Role |
|------------|--------|------|
| `AuthController` | `/auth` | Public |
| `ProfileController` | `/profile` | Authenticated |
| `LayoutController` | `/layout` | Authenticated |
| `NotificationsController` | `/notifications` | Authenticated |
| `AdminController` | `/admin` | SuperAdmin |
| `ProviderManagerController` | `/pm` | ProviderManager |
| `AgentController` | `/agent` | Agent |
| `ClientManagerController` | `/cm` | ClientManager |
| `RequesterController` | `/requester` | Requester |

Dependency: `Api → Application → Domain`; `Infrastructure → Application`; Api wires DI only.

---

## 5. Domain invariants (DESIGN)

### Enums (persist as int; serialize as string in JSON)

- **TicketStatus:** `Open=1`, `InProgress=2`, `PendingRequesterResponse=3`, `Resolved=4`, `Closed=5`
- **TicketPriority:** `Low=1`, `Medium=2`, `High=3` (create default **Medium**)
- **NotificationType:** `NewTicketAssigned=1`, `NewMessage=2`, `TicketReassigned=3`, `TicketResolved=4`, `TicketClosed=5`, `TicketReopened=6`

### Auto-Assign (DESIGN §6) — unit-test these

1. No active agents → `AssignedAgentId = null`, no notification
2. Prefer agents with 0 open tickets (`Open`/`InProgress`/`PendingRequesterResponse`)
3. Else lowest open count
4. Tie-break: oldest `LastAssignedAt`; **null wins**
5. Set `LastAssignedAt` + insert `NewTicketAssigned`

### Reassign (DESIGN §7)

- Agent: only own tickets → another active Agent same Provider
- ProviderManager: override any ticket in own Provider
- Update new Agent `LastAssignedAt`; clear `SeenByAgentDate`; notify `TicketReassigned`

### Reopen (DESIGN §8)

- Only creating Requester; only from `Resolved`/`Closed` else **409**
- Status → `Open`; `ReopenCount++`; `CloseDate=null`; **keep same Agent**; clear `SeenByAgentDate`; notify `TicketReopened`

### Notifications matrix (Backend Guide §2)

| Event | Type | Recipient |
|-------|------|-----------|
| Assigned | `NewTicketAssigned` | Selected Agent |
| Chat message | `NewMessage` | Other party (Agent ↔ creator Requester) |
| Reassign | `TicketReassigned` | New Agent |
| Resolved / Closed | `TicketResolved` / `TicketClosed` | Creator Requester |
| Reopened | `TicketReopened` | Same Agent |

### Phase 1 business rules

- Create Provider → also creates first ProviderManager (one transaction)
- Create/Renew Subscription → deactivate previous active sub for that Provider
- Duplicate `managerUsername` → **409**
- Deactivated Provider → dependent users fail login

### Phase 2 business rules

- Implicit filter `ProviderId == token.ProviderId`
- Enforce `MaxAgentCount` / `MaxClientCount` → **400**
- Create Client → also creates ClientManager
- Deactivate Agent → **no** auto-reassign of open tickets

### Phase 5

- ClientManager: **zero** ticket `[SET]`/`[UPDATE]`; no chat routes

---

## 6. CQRS vertical slices (preferred for new work)

```
Ticket.Application/Features/[Feature]/
  Commands/   [Action][Feature]Command.cs + Handler
  Queries/    Get[Feature]Query.cs + Handler
Ticket.Application/DTOs/[Feature]Dtos.cs
Ticket.Application/Errors/[Feature]Errors.cs
Ticket.Domain/Entities/[Feature].cs
Ticket.Api/Controllers/[Area]Controller.cs
```

| Piece | Rule |
|-------|------|
| Controllers | Thin; `[Authorize(Roles=...)]`; route = openapi |
| Handlers | Business + persistence; CT forwarded |
| Validation | FluentValidation → `ValidationProblemDetails` (`errors` map for FE) |
| Errors | Feature errors → ProblemDetails (404/409/…) |
| Mapping | Explicit or Mapster Adapt — **no** AutoMapper `IMapper` |
| Lists | `search` + `pageNumber` (1-based) + `pageSize` (1–100) → `PagedResponse<T>` |

Fat god-services (`IAdminService`) = bootstrap legacy only; new endpoints = slices.

**Coding order per endpoint (Backend Guide §3):**

1. SCENARIOS card → 2. DTO → 3. Handler + DESIGN rules → 4. Controller route → 5. Tests (happy + 400 + 403/404) → 6. Swagger vs openapi → FE handoff note

---

## 7. Pagination & HTTP errors (FE expects)

| Status | FE behavior |
|--------|-------------|
| 400 | `errors` object under fields (`ValidationProblemDetails`) |
| 401 | refresh once; then login |
| 403 / 404 / 409 | `detail` toast |
| 500 | generic message |

Auth specifics: login bad user/password → same **401**; inactive/deleted → **403**; forgot-password always **200** (no enumeration).

---

## 8. Persistence

- Inject abstractions (`IApplicationDbContext` / repos); never leak DbContext into Api.
- Reads: `.AsNoTracking()` when not mutating.
- Exclude `IsDeleted` / inactive where contract requires.
- IDs: `int` per ERD.
- Migrations: **report** `dotnet ef` command; do not add migration files unless asked.

---

## 9. Out of scope (MVP)

From DESIGN §17 / Backend Guide §5:

- `TicketAssignmentLogs`
- General `AuditLogs`
- SignalR
- Multi-requester chat participation (write)
- Re-run Auto-Assign on Reopen

---

## 10. FE open questions (Backend Guide §4 — fill in B-Prep)

| # | Topic | Locked decision |
|---|-------|-----------------|
| 1 | Attachment max size | _TBD_ |
| 2 | Allowed file types | _TBD_ |
| 3 | `accessToken` TTL | _TBD_ |
| 4 | `refreshToken` TTL | _TBD_ |
| 5 | Route prefixes | **Frozen:** `/admin`, `/pm`, `/agent`, `/cm`, `/requester` |
| 6 | CORS origins | _TBD_ |
| 7 | Swagger URL | _TBD_ |

Publish answers in handoff notes so FE Phase 0 interceptors work.

---

## 11. Bug fixes

1. MCP discovery → 2. Minimal edit → 3. Preserve contract → 4. `verify-feature`

---

## 12. Documentation policy

| Layer | Docs |
|-------|------|
| Domain entities / AutoAssign | Required |
| Commands, Queries, Handlers | Required |
| DTOs | Required (Swagger-facing) |
| Feature Errors | Required |
| Controllers | Required |
| Validators | Optional |

---

## 13. Shell & verification

```powershell
rtk dotnet build src/Ticket/Ticket.slnx
```

During the full-cycle development stage: use `verify-feature` in development mode (build + business/unit tests + affected regressions) before each phase commit. This earns `Development Complete`, not API Verified.

After all development phases are committed and the user separately approves database access: run real Postman/Newman scenarios phase by phase. Do not advance until every required scenario in the current phase passes. This earns `API Verified`.

---

## 14. Orchestrated workflow

```
Explicitly approved full cycle
  → phase-cycle on codex/all-phases
  → repeat B-Prep/0–6 in order:
       scenario-contract → implement → business tests/build → scoped phase commit
  → developmentComplete
  → STOP and request explicit Postman/test-database approval
  → repeat phases in order:
       real Postman scenarios → diagnose/fix/retest until every scenario passes
  → allPhasesPassed → Project Complete
```

The full-cycle kickoff authorizes phase implementation and scoped commits only. It does not authorize database access, Postman execution, migrations, destructive cleanup, push, or PR creation.

Standalone endpoint/feature work keeps the existing `scenario-contract` approval gate.

Skills: `.cursor/skills/`. Quickrefs: `references/`.

---

## Cursor Cloud specific instructions

Durable, non-obvious notes for cloud agents (the VM snapshot already has the SDK installed and `dotnet restore` run on startup).

- **SDK / PATH:** .NET 10 SDK lives at `/usr/share/dotnet` and is symlinked to `/usr/local/bin/dotnet`, so plain `dotnet` works from any directory (no `DOTNET_ROOT` export needed). It is baked into the snapshot; the startup update script only runs `dotnet restore src/Ticket/Ticket.slnx`.
- **`rtk` is NOT installed in the cloud VM.** The repo rules/`RTK.md` prescribe an `rtk` shell wrapper, but it does not exist here — run `dotnet`/`git` directly. The `user-codebase-memory-mcp` server is also unavailable in this VM; use normal search tools.
- **Run the API:** from `src/Ticket/Ticket.Api` run `dotnet run --launch-profile http` → serves `http://localhost:5218`. Prefer the `http` profile; the `https` profile triggers a harmless "Failed to determine the https port for redirect" warning and needs a trusted dev cert.
- **Swagger** is only mapped in `Development` (`ASPNETCORE_ENVIRONMENT=Development`, set by the launch profile) at `http://localhost:5218/swagger`.
- **Storage is in-memory** (`InMemoryAdminService`), so data resets on every restart — there is no database to provision or migrate.
- **No auth endpoint yet (Phase 1 only).** All `/api/v1/admin/*` routes require a SuperAdmin JWT, but no `AuthController` exists to mint one. For smoke tests, hand-sign an HS256 token with `Jwt:SigningKey` from `appsettings.json` (`iss=Ticket.Api`, `aud=Ticket.Client`, role claim = `SuperAdmin`). Without a token these routes return 401.
- **No test project exists yet**, so `dotnet test` finds nothing to run. Lint = `dotnet format src/Ticket/Ticket.slnx --verify-no-changes`; build = `dotnet build src/Ticket/Ticket.slnx`.
