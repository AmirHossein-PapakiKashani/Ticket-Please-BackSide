# Phase 1 — Postman Report

**Status:** API Verified (smoke DoD)  
**Runner:** Newman against `http://localhost:5218` + DB `TicketForDol`

| Scenario | Result |
|----------|--------|
| PM → admin 403 | Covered |
| Create Plan → Provider → Subscription | Covered |
| New PM login + profile subscription | Covered |

Assertions: 8/8 passed.
