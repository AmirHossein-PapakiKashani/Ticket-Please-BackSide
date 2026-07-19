# Postman Summary — Stage 2

**Status:** `allPhasesPassed` (smoke DoD coverage)  
**Branch:** `codex/all-phases`  
**API:** `http://localhost:5218/api/v1`  
**DB:** local Postgres `TicketForDol` (Host=localhost) — proven non-production  
**Runner:** Newman 6.2.2 + `postman/Ticket-Phase-Integration.postman_collection.json`

## Why Newman (not Postman cloud run)

- Postman MCP `getAuthenticatedUser` returned 401 Invalid API Key after `mcp_auth`.
- `runCollection` is listed under `excludedFromGeneration` and was not callable via MCP.
- Phase-cycle allows Newman with a real API + approved test DB.

## Phase results

| Phase | Assertions | Status |
|-------|------------|--------|
| 0 | 9/9 | API Verified |
| 1 | 8/8 | API Verified |
| 2 | 7/7 | API Verified |
| 3 | 6/6 | API Verified |
| 4 | 15/15 | API Verified |
| 5 | 5/5 | API Verified |
| 6 | 3/3 | API Verified |

## Coverage note

This collection covers Backend Guide **minimum smoke DoD** per phase (not every SC-id card in SCENARIOS-v2). Expand folder requests if full scenario matrix is required.

## Artifacts

- Collection: `postman/Ticket-Phase-Integration.postman_collection.json`
- Environment: `postman/local.postman_environment.json` (demo seed credentials only)
- Reports: `Documents/Phase-Reports/Phase-*-postman.md`

## Project status

- Development Complete: yes (prior)
- API Verified (smoke): yes for phases 0–6
- Push: not performed
