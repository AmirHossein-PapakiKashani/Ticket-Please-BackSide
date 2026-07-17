# Ticketing System — Cursor Agent Skills

Personalized for this repo from DESIGN-v6, Backend Implementation Guide, and FE Implementation Phases.

## Skills

| Skill | When |
|-------|------|
| **orchestrator** | Start every task — state Phase + Area |
| **phase-cycle** | Commit all remaining development phases, then run separately approved real Postman verification |
| **scenario-contract** | New endpoint / phase — DESIGN + scenarios + openapi + DoD |
| **feature-scaffold** | After `تایید شد` — CQRS slice + area controller |
| **verify-feature** | Build, phase smoke, coverage, FE handoff |

## Quick start

```
/orchestrator
Phase: 0
Area: auth
Task: implement login + refresh per Backend Guide Phase 0 and openapi
```

After investigation report:

```
تایید شد
```

## Flow

```
Approved full cycle
  → phase-cycle development stage
  → for each phase: scenario-contract → implement → business tests → commit
  → all development phases committed
  → WAIT for explicit Postman/database approval
  → for each phase: real Postman scenarios → fix/retest until all pass
  → Project Complete
```

Standalone endpoint work retains its own scenario-contract approval gate.

## Document roles

| Doc | Use for |
|-----|---------|
| DESIGN-v6 | Business, ERD, Auto-Assign, Reassign, Reopen |
| API-SCENARIOS-v2 + openapi | HTTP contract |
| Backend Guide | Phase build order + BE DoD |
| FE Phases | Same phase numbers + UI DoD / sync |

## References

- `AGENTS.md` — full agent manual for Ticket
- `references/phase-map.md` — phase ↔ routes
- `Documents/` — source of truth
