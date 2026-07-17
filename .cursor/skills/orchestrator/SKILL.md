---
name: orchestrator
description: >-
  Route Ticketing API work by phase (B-Prep, 0–6) through investigation, scaffold,
  verification, or the two-stage full-phase cycle that commits development phases
  before separately approved Postman verification. Use at the start of any feature,
  phase endpoint, full phase, repeated phase delivery, or bug fix.
  Invoke with /orchestrator.
---

# Ticket Orchestrator

Coordinate Ticketing System backend (.NET 10). Sources: DESIGN-v6, Backend Guide, FE Phases, openapi, scenarios.  
Does not replace `AGENTS.md` or `.cursor/rules/`.

## Step 0 — Classify

| Signal | Route |
|--------|-------|
| Run/start/continue/complete all remaining phases | → **phase-cycle** development loop, then Postman approval gate |
| Phase work / new endpoint / new Command·Query | → **scenario-contract**, wait approval |
| Domain: Auto-Assign / Reassign / Reopen | → **scenario-contract** + DESIGN §§6–8; unit tests required |
| Simple bug in one known handler | → fix (`AGENTS.md` §11) → **verify-feature** |
| User approves a standalone contract | → **feature-scaffold** |
| User approves Postman after all development commits | → **phase-cycle** Postman stage |

## Step 1 — Pre-task

```
Phase I am on: [B-Prep | 0–6]
Area: [auth | admin | pm | agent | cm | requester | shared]
Sections I will read: [...]
Files I will CREATE: [...]
Files I will MODIFY: [...]
Assumptions I am making: [...]
```

Map phase → Backend Guide section + FE Phases DoD (`AGENTS.md` §3).

## Step 2 — Discovery

codebase-memory MCP first; Shell with `rtk` + `RTK ▸`; no Grep/Glob for `.cs` first.

## Step 3 — Investigation gate

If `phase-cycle` applies, hand control to that skill. One explicit kickoff approval authorizes all remaining development phases and their scoped commits. After all development commits, stop for the separate real Postman/database approval.

Otherwise, if scenario-contract applies: follow that skill → **STOP** until `approved` / `تایید شد`.

## Step 4 — Execute

Only endpoints listed for the stated phase. No MVP out-of-scope (SignalR, TicketAssignmentLogs, …).

## Step 5 — Verify

Use `verify-feature` in `development` mode before each phase commit. Use it again in `postman` mode only after the separate database approval. A phase is not API Verified until all of its real Postman scenarios pass.

## Kickoff

Full phase cycle:

```
/phase-cycle
Start: [B-Prep | 0-6 | next]
Mode: all-remaining
```

Single feature or endpoint:

```
/orchestrator
Phase: [B-Prep | 0-6]
Area: [auth | admin | pm | agent | cm | requester | shared]
Task: [one paragraph]
```
