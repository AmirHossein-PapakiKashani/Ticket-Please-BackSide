# Phase 0 — Postman Report

**Status:** API Verified (smoke DoD)  
**Target:** `http://localhost:5218/api/v1` against local Postgres `TicketForDol`  
**Runner:** Newman + `postman/Ticket-Phase-Integration.postman_collection.json`

| Scenario | Expected | Result | Evidence |
|----------|----------|--------|----------|
| Login validation | 400 + errors map | Covered | 400 |
| Bad credentials | 401 | Covered | 401 |
| Profile no bearer | 401 | Covered | 401 |
| Login superadmin | 200 + tokens | Covered | 200 |
| Refresh rotation | 200 | Covered | 200 |
| Get profile | 200 | Covered | 200 |

Assertions: 9/9 passed.
