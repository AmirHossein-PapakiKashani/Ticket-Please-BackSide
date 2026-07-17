---
name: feature-scaffold
description: >-
  Scaffold Ticketing CQRS slices (Command/Query/Handler/Controller/DTO) for the
  current phase after scenario-contract approval. Follows AGENTS.md + references.
---

# Feature scaffold — Ticketing System

## Prerequisites

- [ ] Phase + Area stated
- [ ] `approved` / `تایید شد` if new endpoint
- [ ] Pre-task protocol done

## Phase 1 — Pre-flight

1. MCP: no duplicate feature/DTO/route
2. Confirm ERD fields (DESIGN §5) + tenant columns
3. Confirm openapi operation + SC-id + phase DoD list

## Phase 2 — File plan (before code)

```
CREATE:
- Ticket.Domain/Entities/[Feature].cs          (if new; match ERD)
- Ticket.Application/DTOs/[Feature]Dtos.cs     (openapi field names)
- Ticket.Application/Errors/[Feature]Errors.cs
- Ticket.Application/Features/[Feature]/Commands|Queries/...
- Ticket.Api/Controllers/[Area]Controller.cs   (fixed area prefixes)

MODIFY:
- IApplicationDbContext / DbContext when EF
- Program.cs only for DI
- Domain Services/AutoAssignAgent when Phase 3 create-ticket
```

See `references/file-locations.md`, `templates.md`, `phase-map.md`.

## Phase 3 — Rules (Ticket-specific)

| Rule | Detail |
|------|--------|
| Routes | Exact openapi under `/api/v1` + `/admin` `/pm` `/agent` `/cm` `/requester` |
| Roles | `[Authorize(Roles=...)]` + handler resource checks |
| Tenant | Claims filter; wrong tenant 404 |
| Soft delete | IsDeleted / deactivate endpoints |
| Validation | FluentValidation → 400 `errors` map |
| HTTP | Contract statuses (201 when specified) |
| Pagination | 1-based pageNumber; pageSize 1–100 |
| Assign | Domain service; unit tests for DESIGN §6 cases |
| Chat | Creator-only POST messages; CM never |
| Notes | Agent-only; never in Requester message history |
| Out of scope | SignalR, TicketAssignmentLogs, AuditLogs |

## Phase 4 — Checklist

- [ ] Only current-phase routes
- [ ] Swagger matches openapi for those routes
- [ ] Notifications fired per Backend Guide matrix when applicable
- [ ] Migration command reported if schema changed

## Phase 5

Run `verify-feature` + FE handoff note.
