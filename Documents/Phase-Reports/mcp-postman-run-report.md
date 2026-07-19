# Postman MCP Run Report — Ticket Phase Ordered

**Date:** 2026-07-19  
**Runner:** Postman MCP `runCollection`  
**API:** `http://localhost:5218/api/v1` (live, not mock)  
**DB target:** local Postgres `TicketForDol`

## Artifacts in Postman (My Workspace)

| Item | Name | UID |
|------|------|-----|
| Environment | Ticket Local API | `25911599-721123ef-2f57-4650-8f20-212509c27a0e` |
| Collection (final) | Ticket Phase Ordered (MCP) | `25911599-22f895bc-fd1f-4450-8f6c-1b31a768a93c` |
| Collection (old/broken order) | Ticket Phase Integration (MCP) | `25911599-bd906655-9556-444a-8697-1b2a67c52a90` |

## Final run result

- Requests: **19**
- Assertions: **20 / 20 passed**
- Failed: **0**
- Success rate: **100%**
- Duration: ~23s

## Coverage (ordered)

| # | Scenario | Result |
|---|----------|--------|
| 00 | Login validation → 400 | Pass |
| 01 | Bad credentials → 401 | Pass |
| 02 | Profile no bearer → 401 | Pass |
| 03 | Login superadmin → 200 | Pass |
| 04 | Refresh token → 200 | Pass |
| 05 | Get profile → 200 | Pass |
| 10–11 | PM login + admin 403 | Pass |
| 20–21 | PM dashboard + unassignedTicketsCount | Pass |
| 30–33 | Create ticket + colleague message 403 | Pass |
| 40–41 | Agent login + list tickets | Pass |
| 50–52 | CM dashboard + agent route 403 | Pass |

## What was fixed

Previous expanded collection failed because requests were out of order (e.g. admin check before PM login). A new ordered collection was created and re-run to 100%.
