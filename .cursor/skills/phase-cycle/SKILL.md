---
name: phase-cycle
description: >-
  Run all remaining Ticket backend phases in two stages: first implement and
  business-verify each phase with a separate standards-compliant Git commit,
  then wait for explicit approval before testing every phase sequentially through
  Postman/Newman against a real approved test database until all documented
  scenarios pass. Use when the user asks to start, continue, or finish the
  complete phased delivery cycle, or invokes /phase-cycle.
---

# Ticket two-stage phase cycle

Coordinate `scenario-contract`, `feature-scaffold`, and `verify-feature` across B-Prep and Phases 0-6. Follow `AGENTS.md` and `.cursor/rules/` throughout.

## Invocation and authorization boundary

Accept:

```text
/phase-cycle
Start: [B-Prep | 0-6 | next]
Mode: all-remaining
```

Treat explicit approval to start this cycle as authorization to implement, business-verify, and commit every remaining phase on the cycle branch without pausing between successful phases.

Do not treat that approval as authorization to:

- connect to a database;
- start Postman/Newman real-API testing;
- run or generate migrations;
- perform destructive cleanup;
- push commits or create a pull request.

Require one separate explicit approval after all development phases are committed before starting the real Postman stage.

## State machine

Track two distinct stages in `.cursor/phase-cycle-state.json`:

```text
development:
  contractChecked -> implemented -> businessVerified -> committed -> nextPhase
  -> developmentComplete -> awaitingPostmanApproval

postman:
  postmanApproved -> phaseTesting -> phasePassed -> nextPhase
  -> allPhasesPassed
```

Store only stage, current phase, branch, commit hashes, report paths, and approval status. Never store credentials, tokens, connection strings, or populated Postman environment values.

Workflow-owned state and reports may remain unstaged during the active cycle. Recognize only these exact paths as workflow artifacts; never ignore unrelated user changes:

- `.cursor/phase-cycle-state.json`
- `Documents/Phase-Reports/Phase-*-development.md`
- `Documents/Phase-Reports/Phase-*-postman.md`
- `Documents/Phase-Reports/development-summary.md`
- `Documents/Phase-Reports/postman-summary.md`

## Stage 1 - Prepare one development branch

Print the repository pre-task block before code work:

```text
Phase I am on: [B-Prep | 0-6]
Area: [auth | admin | pm | agent | cm | requester | shared]
Sections I will read: [...]
Files I will CREATE: [...]
Files I will MODIFY: [...]
Assumptions I am making: [...]
```

Prepare Git safely:

1. Show and run `rtk git status --short --branch`.
2. Stop for any pre-existing user change. Never stash, reset, discard, or overwrite it automatically.
3. Switch to `main` with `rtk git switch main`.
4. Update with `rtk git pull --ff-only`. Stop if it cannot fast-forward.
5. Create or resume one cumulative branch named `codex/all-phases`.
6. Inspect recent subjects with `rtk git log -10 --pretty=format:%s` to discover the repository commit convention.
7. Use Conventional Commits when no stronger repository convention exists.

Never implement directly on `main`. Never push automatically.

## Stage 1 - Implement each phase

Process B-Prep and Phases 0-6 in order, starting from the first incomplete phase.

For each phase:

1. Invoke `scenario-contract` in active phase-cycle mode.
2. Read the matching Backend Guide, FE phases, API scenarios, `openapi.json`, and relevant DESIGN sections.
3. Use codebase-memory MCP first for C# discovery and blast-radius analysis.
4. Produce the endpoint contract and business-rule checklist internally and in the development report.
5. Stop only for a real contract conflict or scope decision. Do not request routine per-phase approval after the full cycle has been approved.
6. Invoke `feature-scaffold` for only the current phase.
7. Add or update unit tests for business rules, tenancy, ownership, status transitions, validation, and side effects relevant to the phase.
8. Run the current phase's focused tests, affected regression tests, and the full build.
9. Diagnose, fix, rebuild, and re-run until business verification passes or a hard blocker is proven.

Stage 1 must not use Postman/Newman or a real database as acceptance evidence. It verifies implementation against contracts and executable business tests; it does not claim end-to-end API verification.

## Stage 1 - Commit each successful phase

Commit the phase only when all conditions are true:

- contracted phase scope is implemented;
- build is green;
- required unit/business tests pass;
- affected regression tests pass;
- documentation and OpenAPI changes are synchronized when required;
- no secret or populated local environment file is staged;
- the staged diff contains only current-phase work and intentional shared prerequisites.

Before committing:

1. Show `rtk git status --short`.
2. Stage explicit phase paths; never use an unreviewed blanket add.
3. Review `rtk git diff --cached --check` and `rtk git diff --cached`.
4. Remove unrelated or secret-bearing files from the index without discarding their working-tree contents.
5. Commit only after the staged diff is clean.

Use a repository-compatible subject, falling back to:

```text
feat(phase-0): implement authentication and profile APIs
feat(phase-1): implement admin management APIs
fix(phase-4): enforce agent reassignment business rules
```

Do not create an empty commit. Record the commit hash in phase state and `Documents/Phase-Reports/Phase-N-development.md`, then immediately start the next phase.

## Development completion gate

After every remaining phase is business-verified and committed:

1. Create `Documents/Phase-Reports/development-summary.md` listing phase commits, business tests, build results, and known limitations.
2. Mark the development stage `developmentComplete`.
3. Prepare the real Postman test plan, required identities, target API process, expected database writes, and cleanup approach.
4. Present the summary and exact planned database actions.
5. Stop at `awaitingPostmanApproval`.

Continue only after explicit approval to connect to the named test database and start real Postman testing. General permission to continue development is insufficient.

## Stage 2 - Approve and prepare real testing

After approval:

1. Resolve the connection string from approved configuration or secret storage without printing it.
2. Prove the target is local/test and not production or an ambiguous shared database.
3. Confirm the API uses that target.
4. Discover the actual identity schema and prepare the minimum scenario actors.
5. Start with three reusable identities, but add actors when scenarios require them; reassignment may require Requester, Agent A, Agent B, and ProviderManager.
6. Prefer supported seed or API paths. Generate direct SQL only when explicitly requested and after verifying hashing and foreign keys.
7. Use unique run markers and scoped cleanup. Preserve soft-delete rules.

Never run migrations, hard deletes, broad updates, or destructive cleanup without separate explicit approval.

## Stage 2 - Test each phase through Postman

Use the repository Postman collection and Newman. If missing, create the smallest phase-organized collection mapped exactly to the documented scenarios and `openapi.json`.

Use the real running API and approved test database. Mock responses or mocked persistence do not count as Passed.

Test phases in order. Treat B-Prep as environment and Swagger prerequisites when it has no callable scenario. For every applicable phase:

1. Execute every documented happy, validation, authentication, authorization, tenancy, ownership, conflict, and side-effect scenario.
2. Assert expected status, response shape, and important values.
3. Re-read persisted state when persistence or notification side effects are expected.
4. Record sanitized request data and reproducible evidence.
5. Keep the current phase active until every required scenario passes.

For a fixable failure, do not stop:

```text
fail -> diagnose -> fix -> build -> restart if needed
     -> rerun failed scenario -> rerun related regressions -> repeat
```

Use codebase-memory MCP first for diagnosis. Keep fixes in scope. If a later-phase fix can affect an earlier phase, re-run every affected earlier Postman group before proceeding.

## Stage 2 - Commit test-driven changes

After the entire current phase passes:

- commit code fixes as `fix(phase-N): ...`;
- commit durable sanitized Postman coverage as `test(phase-N): ...`;
- do not commit tokens, passwords, connection strings, raw database dumps, or populated environments;
- do not create an empty commit when execution passes without file changes.

Create `Documents/Phase-Reports/Phase-N-postman.md` with every scenario result and evidence. Only then advance to the next test phase.

After all phases pass, create `Documents/Phase-Reports/postman-summary.md`, set state to `allPhasesPassed`, and provide the complete development and test commit list. Do not push without explicit approval.

## Completion meanings

Use distinct labels:

- `Development Complete`: implementation, build, and business tests passed and the phase was committed.
- `API Verified`: every real Postman scenario for the phase passed against the approved test database.
- `Project Complete`: all phases are both Development Complete and API Verified.

Never call a phase API Verified or Project Complete during Stage 1.

## Hard blockers

Persist through ordinary implementation and test failures. Stop only when safe progress is impossible, including:

- pre-existing user changes overlap the cycle;
- `main` cannot fast-forward;
- source contracts materially conflict;
- a required external dependency or credential is unavailable;
- the database is production or cannot be proven safe;
- a destructive action or migration needs new approval;
- a required scenario depends on an unavailable external system;
- repository state makes a safe, scoped commit impossible.

Report the exact blocker, completed work, current phase, last green commit, and the smallest user action needed to resume.
