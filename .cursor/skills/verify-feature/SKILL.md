---
name: verify-feature
description: >-
  Verify Ticketing changes in two modes: development business verification before
  each phase commit, and real Postman/API verification after the separate database
  approval. Use after feature-scaffold, code changes, or during phase-cycle.
---

# Verify feature — Ticketing System

Select one mode and state it explicitly:

- `development`: build, focused unit/business tests, affected regressions, contract/DoD review; no real-API claim.
- `postman`: all documented scenarios through the real API and approved test database.

## 1. Build

```powershell
rtk dotnet build src/Ticket/Ticket.slnx
```

Show `RTK ▸` first. Fix errors before continuing.

## 2. Diagnostics

IDE diagnostics on edited files only.

## 3. Development business verification

Before a phase development commit:

1. Run focused unit/business tests for the phase.
2. Run affected regression tests.
3. Confirm routes, DTOs, enums, statuses, tenancy, ownership, and side effects against the contract tables.
4. Confirm the staged diff is phase-scoped and contains no secrets.
5. Mark only `Development Complete` when green.

Do not require Postman or real database access in development mode. Do not call the phase API Verified.

## 4. Postman phase verification

Use Backend Guide **Backend DoD** for the stated phase.

| Phase | Minimum smoke |
|-------|----------------|
| 0 | Login + refresh; 400 field error; bearer required → 401 |
| 1 | Plan → Provider → Subscription; PM login; non-admin → 403 on `/admin` |
| 2 | Create Agent + Client; limit 400; dashboard scoped |
| 3 | Create ticket 201; Auto-Assign or unassigned; colleague cannot POST message |
| 4 | Reassign A→B; Resolve; notes absent from Requester messages |
| 5 | Requesters CRUD; CM cannot hit chat routes |
| 6 | Full cycle + Swagger vs openapi diff |

If JWT unavailable: `Blocked: auth not available` + still require green build.

Use the real API and approved test database. Mock responses do not count as Passed. Keep diagnosing, fixing, and re-running until every required scenario in the current phase passes or a hard external/safety blocker is proven.

## 5. Scenario coverage

| SC-id / route | Expected | Result | Evidence |
|---------------|----------|--------|----------|
| … | … | Covered / Failed / Blocked | status + note |

## 6. FE handoff (required after API verification)

```
Phase N API ready
Credentials: ...
Sample IDs: ...
Known gaps / blocked: ...
Locked FE answers (TTL, CORS, attachments) if updated: ...
```

## 7. Persistence

Report exact `dotnet ef` migration command if schema changed; do not generate files unless asked.

## 8. Done rule

Use these exact meanings:

- `Development Complete`: green build, business tests, contract review, and phase commit.
- `API Verified`: complete real Postman scenario coverage against the approved test database.
- `Project Complete`: all phases have both statuses.

Do not mark Phase N API Verified while any required scenario is Failed or Blocked, or while the Backend Guide checklist or FE sync gate fails.
