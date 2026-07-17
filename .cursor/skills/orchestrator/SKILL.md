---
name: orchestrator
description: >-
  Route Ticketing API work by phase (B-Prep, 0–6) through investigation, scaffold,
  and verification. Use at the start of any feature, phase endpoint, or bug fix.
  Invoke with /orchestrator.
---

# Ticket Orchestrator

Coordinate Ticketing System backend (.NET 10). Sources: DESIGN-v6, Backend Guide, FE Phases, openapi, scenarios.  
Does not replace `AGENTS.md` or `.cursor/rules/`.

## Step 0 — Classify

| Signal | Route |
|--------|-------|
| Phase work / new endpoint / new Command·Query | → **scenario-contract**, wait approval |
| Domain: Auto-Assign / Reassign / Reopen | → **scenario-contract** + DESIGN §§6–8; unit tests required |
| Simple bug in one known handler | → fix (`AGENTS.md` §11) → **verify-feature** |
| User says `تایید شد` / `approved` | → **feature-scaffold** |

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

If scenario-contract applies: follow that skill → **STOP** until `approved` / `تایید شد`.

## Step 4 — Execute

Only endpoints listed for the stated phase. No MVP out-of-scope (SignalR, TicketAssignmentLogs, …).

## Step 5 — Verify

`verify-feature` + FE handoff when phase DoD met.

## Kickoff

```
/orchestrator
Phase: [B-Prep | 0-6]
Area: [auth | admin | pm | agent | cm | requester | shared]
Task: [one paragraph]
```
