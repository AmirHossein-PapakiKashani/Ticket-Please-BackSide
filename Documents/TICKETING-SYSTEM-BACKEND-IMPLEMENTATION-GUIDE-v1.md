# 🛠️ Ticketing System — Backend Step-by-Step Implementation Guide v1

> **Status:** ✅ Active implementation reference for backend (.NET 10)
> **Audience:** Backend developer / implementing agent
> **Goal:** Implement the API in the **same phase order as frontend**, using one shared contract, so BE and FE never diverge.

---

## 0. Document Map (Read This First)

| Document | Role | When to open it |
| -------- | ---- | ---------------- |
| `TICKETING-SYSTEM-DESIGN-v6.md` | Source of truth for **business rules**, ERD, Auto-Assignment, Reassign, Reopen | Before coding domain logic |
| `TICKETING-SYSTEM-API-SCENARIOS-v2.md` | Source of truth for **each endpoint** (input, output JSON, status codes) | Before coding any controller action |
| `openapi.json` | Machine-readable contract (Swagger / codegen / FE clients) | Keep in sync; never invent routes outside it |
| `TICKETING-SYSTEM-IMPLEMENTATION-PHASES-v1.md` | Frontend phase order & UI DoD | Mirror these phases on the backend |
| **This file** | Backend build order, layer checklist, FE/BE sync gates | Day-to-day while coding |

### 0.1 Golden Rule — No FE/BE Drift

1. **Routes, DTOs, enums, status codes** = exactly as `openapi.json` + `API-SCENARIOS-v2`.
2. **Business behavior** = exactly as `DESIGN-v6` (Sections 3, 6, 7, 8).
3. **Phase order** = same numbers as FE phases (0 → 6). Backend Phase N must be **demo-able in Swagger** before FE starts Phase N UI (or at least the endpoints FE needs that week).
4. If you need a change: update **DESIGN / SCENARIOS / openapi** first, then code. Never “just fix it in the API”.

### 0.2 Fixed Stack Decisions (MVP)

| Topic | Decision |
| ----- | -------- |
| Runtime | ASP.NET Core **.NET 10** Web API |
| Architecture | Clean Architecture (Api / Application / Domain / Infrastructure) |
| Auth | JWT Bearer (`accessToken` + `refreshToken` rotation) |
| Multi-tenancy | Shared DB + filter on `ProviderId` / `ClientId` (DESIGN-v6 §3) |
| Delete | Soft (`IsDeleted` / deactivate) — use `PATCH .../deactivate`, not hard `DELETE` |
| Chat | Polling only (no SignalR in MVP) |
| JSON | camelCase; enums as **strings** |
| Errors | `ProblemDetails` / `ValidationProblemDetails` |
| Contract base path | `/api/v1` |

### 0.3 Suggested Solution Layout

```
src/
  Ticket.Api/                 # Controllers, Program.cs, middleware, Swagger
  Ticket.Application/         # Commands/Queries, validators, DTOs, interfaces
  Ticket.Domain/              # Entities, enums, domain services (AutoAssign)
  Ticket.Infrastructure/      # EF Core, JWT, file storage, email/SMS stubs
tests/
  Ticket.UnitTests/           # Auto-Assignment, Reopen, Reassign rules
  Ticket.IntegrationTests/    # API tests against TestServer / WebApplicationFactory
```

Suggested controllers (aligned with SCENARIOS §0.7):

- `AuthController`, `ProfileController`, `LayoutController`, `NotificationsController`
- `AdminController` → `/admin/*`
- `ProviderManagerController` → `/pm/*`
- `AgentController` → `/agent/*`
- `ClientManagerController` → `/cm/*`
- `RequesterController` → `/requester/*`

---

## 1. Sync Matrix — Backend Phase ↔ Frontend Phase

Use this table as a **gate**. Do not mark a backend phase Done until the FE phase can consume these endpoints.

| Phase | Backend delivers | Frontend consumes (see FE phases doc) | Shared DoD |
| ----- | ---------------- | ------------------------------------- | ---------- |
| **0** | Auth, Profile, Layout, Notifications | Login, forgot password, layout badge, profile | Login + refresh + 400 field errors work |
| **1** | `/admin/*` Plans, Providers, Subscriptions | SuperAdmin screens | Plan → Provider → Subscription path works |
| **2** | `/pm/*` Agents, Clients, dashboard | ProviderManager screens | At least 1 active Agent + 1 Client |
| **3** | `/requester/*` create ticket + chat + reopen | Requester screens | Ticket created; Auto-Assign runs; chat rules |
| **4** | `/agent/*` chat, status, reassign, notes | Agent screens | Reassign + Resolve + notes isolation |
| **5** | `/cm/*` requesters + read-only tickets | ClientManager screens | No write endpoints on tickets for CM |
| **6** | Hardening: CORS, file limits, seed, E2E | Polling, cross-role E2E | Full cycle Pass |

---

## Phase B-Prep — Project Bootstrap (Before Phase 0)

### Steps

1. Create solution + 4 projects; wire DI in `Program.cs`.
2. Add EF Core + SQL Server (or LocalDB for local).
3. Configure:
   - JWT options (issuer, audience, access TTL, refresh TTL) — **document values for FE** (answers FE open questions).
   - CORS for FE origin(s).
   - Global exception → ProblemDetails.
   - FluentValidation (or DataAnnotations) → ValidationProblemDetails.
4. Seed `Roles` (DESIGN-v6 §5.3):

   ```
   1 SuperAdmin
   2 ProviderManager
   3 Agent
   4 ClientManager
   5 Requester
   ```

5. Seed one SuperAdmin user for Phase 1 testing.
6. Publish Swagger from the same route shapes as `openapi.json` (compare side-by-side).

### Checklist

- [ ] `/api/v1` prefix applied
- [ ] Swagger opens and lists empty area groups
- [ ] FE has written answers: token TTL, attachment max size/types, CORS origins

---

## Phase 0 — Shared Infrastructure (Mirror FE Phase 0)

**DESIGN refs:** Users, PasswordResetTokens, Notifications  
**CONTRACT refs:** SCENARIOS §1 (Auth & Common) — 14 endpoints  
**FE ref:** IMPLEMENTATION-PHASES §0

### 0.A Implement entities & migrations

Entities from DESIGN-v6 §5: `Users`, `Roles`, `PasswordResetTokens`, `Notifications` (minimal for Phase 0).

### 0.B Auth endpoints (Public)

| Method | Route | Notes |
| ------ | ----- | ----- |
| POST | `/auth/login` | Same 401 message for bad user/password; 403 if inactive/deleted |
| POST | `/auth/refresh-token` | Rotate refresh token |
| POST | `/auth/logout` | Idempotent 200 |
| POST | `/auth/forgot-password` | Always 200 (no user enumeration) |
| POST | `/auth/reset-password` | Token one-time; validate expiry |

**JWT claims (minimum):** `sub`/`userId`, `roleName`, `providerId?`, `clientId?`

### 0.C Authenticated common endpoints

| Method | Route | Roles |
| ------ | ----- | ----- |
| GET/PUT | `/profile` | All |
| PUT | `/profile/change-password` | All |
| GET | `/profile/subscription` | ProviderManager only (403 others; 404 if no active sub) |
| GET | `/layout/profile-summary` | All |
| GET | `/layout/notifications/unread-count` | All |
| GET | `/notifications` | All + pagination |
| PATCH | `/notifications/{id}/read` | Own only → else 404 |
| PATCH | `/notifications/read-all` | All |

### 0.D Backend DoD (must match FE Phase 0 DoD)

- [ ] Login returns `LoginResponse` shape from SCENARIOS
- [ ] Refresh rotation works; expired refresh → 401
- [ ] Validation errors return `errors` map (FE shows under fields)
- [ ] Unauthorized without bearer → 401
- [ ] Swagger: all Phase 0 routes present and match `openapi.json`

**Handoff to FE:** “Phase 0 API ready” + sample SuperAdmin credentials.

---

## Phase 1 — SuperAdmin (Mirror FE Phase 1)

**DESIGN refs:** Providers, Plans, ProviderSubscriptions  
**CONTRACT:** SCENARIOS §2 — 12 endpoints  
**FE ref:** IMPLEMENTATION-PHASES §1

### Why first (same as FE)

Without Plan + Provider + Subscription, no ProviderManager can enter Phase 2.

### Endpoints to implement

| Area | Routes |
| ---- | ------ |
| Dashboard | `GET /admin/dashboard/stats` |
| Providers | `GET/POST /admin/providers`, `GET/PUT /admin/providers/{id}`, `PATCH .../deactivate` |
| Plans | `GET/POST /admin/plans`, `PUT /admin/plans/{id}`, `PATCH .../deactivate` |
| Subscriptions | `GET/POST /admin/providers/{id}/subscriptions` |

### Critical business rules

1. **Create Provider** creates Provider **and** first `ProviderManager` user in one transaction.
2. **Create/Renew Subscription** deactivates previous active subscription for that Provider.
3. Duplicate `managerUsername` → **409**.
4. Deactivate Provider: soft; dependent users must fail login (check Provider `IsActive` on login).

### Backend DoD

- [ ] Manual path works in Swagger: **Create Plan → Create Provider → Create Subscription**
- [ ] Login as new ProviderManager succeeds
- [ ] `GET /profile/subscription` returns plan info (not 404)
- [ ] Non-SuperAdmin → **403** on all `/admin/*`

**Handoff to FE:** Seed/demo Provider + active subscription ready.

---

## Phase 2 — ProviderManager (Mirror FE Phase 2)

**DESIGN refs:** Users (Agent/ClientManager), Clients, plan limits  
**CONTRACT:** SCENARIOS §3 — 12 endpoints  
**FE ref:** IMPLEMENTATION-PHASES §2

### Endpoints

| Area | Routes |
| ---- | ------ |
| Dashboard | `GET /pm/dashboard/stats` (include `unassignedTicketsCount`) |
| Agents | `GET/POST /pm/agents`, `GET/PUT /pm/agents/{id}`, `GET .../stats`, `PATCH .../deactivate` |
| Clients | `GET/POST /pm/clients`, `GET/PUT /pm/clients/{id}`, `PATCH .../deactivate` |

### Critical business rules

1. Implicit filter: `ProviderId == token.ProviderId` everywhere.
2. Cross-tenant access → **404** (not 403).
3. Create Agent: `LastAssignedAt = null`; enforce `MaxAgentCount` from active plan → **400** if exceeded.
4. Create Client: also creates `ClientManager`; enforce `MaxClientCount`.
5. Deactivate Agent: **no auto-reassign** of open tickets (DESIGN / FE note).
6. Deactivate Client: Client users cannot login.

### Backend DoD

- [ ] Create ≥1 active Agent and ≥1 Client (with ClientManager)
- [ ] Limit-exceeded returns clear `detail` for FE toast
- [ ] Dashboard counts scoped to current Provider

**Handoff to FE:** Agent + Client credentials for Phase 3/4.

---

## Phase 3 — Requester Tickets (Mirror FE Phase 3)

**DESIGN refs:** Tickets, TicketMessages, Attachments, §6 Auto-Assignment, §8 Reopen  
**CONTRACT:** SCENARIOS §6 — 6 endpoints  
**FE ref:** IMPLEMENTATION-PHASES §3

> **Note:** FE Phase 3 comes before Agent UI, but you may implement domain Auto-Assign service here. Agent endpoints come in Phase 4; assignment still notifies Agent via Notifications table.

### Endpoints

| Method | Route | Rules |
| ------ | ----- | ----- |
| POST | `/requester/tickets` | multipart; run **AutoAssignAgent** once; default priority `Medium` |
| GET | `/requester/tickets` | Client-scoped; `createdByMe` filter |
| GET | `/requester/tickets/{id}` | Same Client → OK even if not creator |
| GET | `/requester/tickets/{id}/messages` | Same Client → read history |
| POST | `/requester/tickets/{id}/messages` | **Only if `RequesterId == current user`** else 403 |
| PATCH | `/requester/tickets/{id}/reopen` | Creator only; status Resolved/Closed else **409**; keep same Agent |

### Implement Auto-Assignment as Domain Service (DESIGN §6)

Unit-test these cases before wiring the endpoint:

1. No active agents → `AssignedAgentId = null`, no notification
2. Free agents (0 open) preferred
3. Else lowest open count
4. Tie-break: oldest `LastAssignedAt`; `null` wins
5. Updates `LastAssignedAt` + inserts `NewTicketAssigned`

### Backend DoD

- [ ] Create ticket returns `201` + `ticketId`
- [ ] First message stored; attachments saved if present
- [ ] Colleague Requester can GET but not POST messages (403)
- [ ] Reopen keeps `AssignedAgentId`; increments `ReopenCount`; sends `TicketReopened`

**Handoff to FE:** Sample ticket IDs + note whether assigned or unassigned.

---

## Phase 4 — Agent Ticket Handling (Mirror FE Phase 4)

**DESIGN refs:** §7 Reassignment, TicketNotes, chat participation  
**CONTRACT:** SCENARIOS §4 — 11 endpoints  
**FE ref:** IMPLEMENTATION-PHASES §4

### Endpoints

| Method | Route | Rules |
| ------ | ----- | ----- |
| GET | `/agent/tickets` | Provider-scoped; filters status/priority/`assignedToMe` |
| GET | `/agent/tickets/{id}` | Provider ticket or 404 |
| GET/POST | `/agent/tickets/{id}/messages` | POST only if assigned to current Agent → else 403 |
| PATCH | `/agent/tickets/{id}/seen` | Sets `SeenByAgentDate` |
| PATCH | `/agent/tickets/{id}/reassign` | Own tickets only (Agent); update LastAssignedAt; clear Seen; notify |
| PATCH | `/agent/tickets/{id}/status` | Assigned agent; Resolved/Closed set CloseDate + notify |
| PATCH | `/agent/tickets/{id}/priority` | Enum validation |
| GET/POST | `/agent/tickets/{id}/notes` | Never exposed on Requester APIs |
| GET | `/agent/active-agents` | Active agents same Provider |

### Critical rules

1. Internal notes **must not** appear in message history DTOs.
2. After reassign, previous agent cannot send messages (403).
3. `NewMessage` notification to Requester on agent send.

### Optional (same phase or early Phase 6)

ProviderManager override reassign (DESIGN §7) — if not in `openapi.json` yet, **do not invent a route**; add to contract first or defer.

### Backend DoD

- [ ] Reassign A→B works; A loses send rights
- [ ] Resolve triggers Requester reopen eligibility
- [ ] Notes invisible to Requester GET messages
- [ ] Unit tests for Reassign ownership + Reopen no re-run assign

**Handoff to FE:** Two agent accounts for reassign demo.

---

## Phase 5 — ClientManager (Mirror FE Phase 5)

**DESIGN refs:** ClientManager supervisory only  
**CONTRACT:** SCENARIOS §5 — 7 endpoints  
**FE ref:** IMPLEMENTATION-PHASES §5

### Endpoints

| Area | Routes |
| ---- | ------ |
| Dashboard | `GET /cm/dashboard/stats` |
| Requesters | CRUD-style list/create/update/deactivate under `/cm/requesters` |
| Tickets | `GET /cm/tickets` **metadata only** — no chat routes |

### Critical rules

1. **Zero** `[SET]`/`[UPDATE]` on tickets for this role.
2. Calling agent/requester chat routes with ClientManager token → 403.
3. Tenant filter: `ClientId == token.ClientId`.

### Backend DoD

- [ ] Can manage Requesters
- [ ] Ticket list has no message bodies
- [ ] Authorization tests prove no chat access

---

## Phase 6 — Hardening & Joint E2E (Mirror FE Phase 6)

**DESIGN refs:** §3 decisions (Polling), §17 backlog (out of MVP)  
**FE ref:** IMPLEMENTATION-PHASES §6

### Backend tasks

1. **CORS** finalized for FE domains.
2. **Attachment** validation: max size, allowed MIME types — publish in README for FE.
3. **Token TTL** published (access e.g. 15m; refresh e.g. 7d) — match FE interceptor assumptions.
4. Seed script for demo: SuperAdmin, Plan, Provider+PM, Agent×2, Client+CM, Requester×2.
5. Integration test: full cycle  
   `Create ticket → AutoAssign → Agent reply → Reassign → Resolve → Reopen`
6. Confirm no SignalR; FE polling is enough.
7. Diff live Swagger vs `openapi.json` — fix mismatches.
8. Do **not** implement Phase-2 backlog (`TicketAssignmentLogs`, AuditLogs, SignalR) unless contracted.

### Joint E2E checklist (BE + FE)

- [ ] All 5 roles login
- [ ] Full ticket lifecycle
- [ ] Unassigned path when all agents inactive
- [ ] 400 / 401 / 403 / 404 / 409 behaviors match SCENARIOS
- [ ] Polling does not break under refresh-token rotation

---

## 2. Cross-Cutting Implementation Checklist

Use while coding any phase.

### Multi-tenancy

- [ ] Every query filters by `ProviderId` or `ClientId` from claims
- [ ] Wrong tenant → **404**
- [ ] Same tenant, wrong ownership → **403**

### Soft delete / deactivate

- [ ] Lists exclude `IsDeleted` / inactive where required
- [ ] Inactive agents excluded from Auto-Assign and reassign candidate lists

### Pagination

- [ ] All LIST endpoints accept `search`, `pageNumber`, `pageSize`
- [ ] Response shape = `PagedResponse<T>`
- [ ] `pageSize > 100` or `pageNumber < 1` → 400

### Notifications (create on these events)

| Event | Type | Recipient |
| ----- | ---- | --------- |
| Ticket assigned | `NewTicketAssigned` | Selected Agent |
| Chat message | `NewMessage` | Other party (Agent ↔ Requester creator) |
| Reassign | `TicketReassigned` | New Agent |
| Resolved | `TicketResolved` | Requester creator |
| Closed | `TicketClosed` | Requester creator |
| Reopened | `TicketReopened` | Same Agent |

### Authorization policies

Map roles 1:1 to route prefixes. Prefer `[Authorize(Roles = "...")]` + resource checks in handlers.

---

## 3. Recommended Coding Order Inside Each Phase

For every endpoint:

1. Open **SCENARIOS** card for that SC-id → copy request/response/status table.
2. Add/adjust DTO in Application layer to match JSON field names.
3. Implement handler + domain rules from **DESIGN**.
4. Expose controller route **identical** to `openapi.json`.
5. Add unit/integration test for at least: happy path + one 400 + one 403/404.
6. Verify in Swagger; tick FE sync gate.

---

## 4. Decisions Locked for FE Compatibility

Answer FE open questions here and keep this section updated:

| # | Topic | Locked decision (fill during Phase B-Prep) |
| - | ----- | ---------------------------------------- |
| 1 | Attachment max size | e.g. `5 MB` |
| 2 | Allowed file types | e.g. `image/png, image/jpeg, application/pdf` |
| 3 | `accessToken` TTL | e.g. `15 minutes` |
| 4 | `refreshToken` TTL | e.g. `7 days` |
| 5 | Route prefixes | **Frozen** to `/admin`, `/pm`, `/agent`, `/cm`, `/requester` |
| 6 | CORS origins | e.g. `http://localhost:5173` |
| 7 | Swagger URL | e.g. `https://localhost:7xxx/swagger` |

---

## 5. Out of Scope (Do Not Implement in MVP)

From DESIGN-v6 §17:

- `TicketAssignmentLogs`
- General `AuditLogs`
- SignalR real-time chat
- Multi-requester chat participation
- Re-run Auto-Assign on Reopen

---

## 6. How the Implementing Agent Should Use This File

When starting work, say which phase you are on, then:

1. Read **this phase** section + linked DESIGN sections.
2. Implement only endpoints listed for that phase.
3. Stop at **Backend DoD** and write a short handoff note for FE.
4. Do not jump ahead (e.g. Agent endpoints before Requester create) unless unblocking a test — if you must, still keep routes contract-compliant.

**Current recommended start:** Phase B-Prep → Phase 0.

---

## ✅ Quick Start Checklist

- [ ] Solution created (.NET 10 Clean Architecture)
- [ ] Roles seeded; SuperAdmin exists
- [ ] Phase 0 Auth + Profile + Notifications live
- [ ] Phase 1 Plan → Provider → Subscription path live
- [ ] Phase 2 Agent + Client created
- [ ] Phase 3 Create Ticket + Auto-Assign tested
- [ ] Phase 4 Agent chat / reassign / resolve tested
- [ ] Phase 5 ClientManager read-only verified
- [ ] Phase 6 E2E + CORS + file limits + Swagger vs openapi aligned
