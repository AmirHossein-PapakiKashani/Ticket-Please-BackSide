---
name: verify-feature
description: >-
  Verify Ticketing API changes: build, phase DoD smoke tests, scenario coverage,
  and FE handoff. Use after feature-scaffold or any code change.
---

# Verify feature — Ticketing System

## 1. Build

```powershell
rtk dotnet build src/Ticket/Ticket.slnx
```

Show `RTK ▸` first. Fix errors before continuing.

## 2. Diagnostics

IDE diagnostics on edited files only.

## 3. Phase smoke (when routes changed)

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

## 4. Scenario coverage

| SC-id / route | Expected | Result | Evidence |
|---------------|----------|--------|----------|
| … | … | Covered / Failed / Blocked | status + note |

## 5. FE handoff (required at phase DoD)

```
Phase N API ready
Credentials: ...
Sample IDs: ...
Known gaps / blocked: ...
Locked FE answers (TTL, CORS, attachments) if updated: ...
```

## 6. Persistence

Report exact `dotnet ef` migration command if schema changed; do not generate files unless asked.

## 7. Done rule

Do not mark Phase N complete while Backend Guide checklist for N is unchecked or FE sync gate fails.
