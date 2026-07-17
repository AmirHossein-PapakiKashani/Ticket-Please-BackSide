# Ticketing System — Cursor Agent Skills

Personalized for this repo from DESIGN-v6, Backend Implementation Guide, and FE Implementation Phases.

## Skills

| Skill | When |
|-------|------|
| **orchestrator** | Start every task — state Phase + Area |
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
User request
  → orchestrator (Phase + Area)
  → scenario-contract → WAIT تایید شد
  → feature-scaffold (only that phase’s routes)
  → verify-feature (Backend DoD + FE handoff)
  → done
```

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
