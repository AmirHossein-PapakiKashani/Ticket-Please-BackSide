# Phase map — Ticketing System

Source: Backend Guide + FE Implementation Phases (same numbers).

| Phase | BE focus | Controllers / prefixes | DESIGN refs | Must demo |
|-------|----------|------------------------|-------------|-----------|
| B-Prep | Bootstrap, JWT, CORS, ProblemDetails, Roles seed, Swagger | — | §5.3 Roles | `/api/v1` + Swagger |
| 0 | Auth, Profile, Layout, Notifications | `/auth`, `/profile`, `/layout`, `/notifications` | §§5.4, 5.11–5.12, 10 | Login + refresh + 400 errors |
| 1 | Plans, Providers, Subscriptions | `/admin` | §§5.1, 5.5–5.6, 11 | Plan → Provider → Subscription |
| 2 | Agents, Clients, PM dashboard | `/pm` | §§5.2, 5.4, 12 | ≥1 Agent + ≥1 Client |
| 3 | Create ticket, chat, reopen, Auto-Assign | `/requester` | §§5.7–5.9, 6, 8, 15 | Ticket + assign + chat rules |
| 4 | Agent chat, status, reassign, notes | `/agent` | §§5.7–5.10, 7, 13 | Reassign + Resolve + notes isolation |
| 5 | Requesters + read-only tickets | `/cm` | §14 | No CM chat/write on tickets |
| 6 | Hardening, seed, E2E | all | §3 polling, §17 backlog | Full cycle; no SignalR |

## Dependency chain

```
B-Prep → 0 → 1 → 2 → 3 → 4
                ↘     ↘
                 5 ←──┘
                   → 6
```

Phase 4 needs Phase 2 (Agent) + Phase 3 (ticket). Phase 5 needs Phase 3.

## Out of MVP (any phase)

TicketAssignmentLogs · AuditLogs · SignalR · multi-requester chat write · Auto-Assign on Reopen
