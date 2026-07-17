---
name: scenario-contract
description: >-
  Validate Ticketing endpoints against DESIGN-v6, API scenarios, openapi, and the
  current backend/FE phase DoD before coding. Wait for approval on standalone
  work; continue without a per-phase pause inside an already approved phase-cycle.
---

# Scenario contract — Ticketing System

## When

- Any new endpoint / Command / Query
- Starting or continuing Phase B-Prep or 0–6
- Changes to auth, tenancy, soft-delete, Auto-Assign, Reassign, Reopen, chat ownership, notifications

## Steps

### 1. Read sources for this phase

| Source | Check |
|--------|-------|
| Backend Guide phase section + DoD | Endpoints in scope |
| FE Phases doc same phase number | UI DoD / sync gate |
| API-SCENARIOS-v2 cards | Request/response/status |
| openapi.json | Path, schema, status |
| DESIGN-v6 | §§3, 5–8, 11–15 as relevant |

### 2. Blast radius (MCP)

Entities, handlers, controllers, notification writers that could break. Never guess.

### 3. One table per endpoint

| Field | Value |
|-------|-------|
| Phase | |
| SC-id / operationId | |
| Method + `/api/v1/...` route | |
| Role(s) | SuperAdmin / ProviderManager / Agent / ClientManager / Requester / Public |
| Request fields | |
| Success status + body | |
| Errors 400/401/403/404/409 | |
| Tenant filter (ProviderId/ClientId) | |
| Ownership (e.g. creator-only chat) | |
| Side effects (soft-delete, notifications, Auto-Assign) | |
| Status | Pending |

Resolve DESIGN vs openapi conflicts before coding (business → DESIGN; HTTP shape → openapi+scenarios).

### 4. Domain checklist (when tickets touched)

- [ ] Auto-Assign once on create only (DESIGN §6)
- [ ] Reopen keeps Agent; no re-assign (DESIGN §8)
- [ ] Reassign rules by role (DESIGN §7)
- [ ] Notes never in Requester message DTOs
- [ ] ClientManager has no chat/write on tickets

### 5. Report and apply the correct gate

Files/docs read · Blast radius · Tables · Open questions  

- Standalone endpoint or phase work: wait for **`approved`** / **`تایید شد`**.
- Active `phase-cycle` whose full development run was explicitly approved: record the contract report and continue without routine per-phase approval.
- Any material contract conflict, new out-of-scope behavior, destructive requirement, or missing business decision: stop and request direction even during `phase-cycle`.

## After code

During the development stage, use `verify-feature` business mode and mark the phase `Development Complete` only after build and business tests pass.

During the later approved Postman stage, mark rows Covered with real HTTP evidence and use `API Verified` only after every documented scenario passes against the approved test database.
