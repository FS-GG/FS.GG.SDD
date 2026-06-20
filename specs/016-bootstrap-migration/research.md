# Phase 0 Research: Bootstrap and Migration Experience

This feature is the Phase 9 bootstrap/migration experience over an already
complete `fsgg-sdd` command surface. The research below records the decisions
that resolve every planning unknown. No `NEEDS CLARIFICATION` markers remain.

## D1 — Smoke harness approach: in-process command workflow, plus a captured CLI process smoke

**Decision**: Implement the automated lifecycle smoke in-process in
`tests/FS.GG.SDD.Commands.Tests/LifecycleSmokeTests.fs`, driving the lifecycle
through the existing `TestSupport` run helpers (`initializeProject`, `runCharter`
… `runShip`, `runAgents`, `runRefresh`) over a disposable `tempDirectory()`
project. Separately capture one real `FS.GG.SDD.Cli` process run (init→ship) as
readiness evidence.

**Rationale**: The command test project already exposes a full per-stage harness
and temp-project machinery, so an in-process drive is deterministic, fast
(< 10 s target), and free of flaky process orchestration — ideal for the
determinism (FR-006) and no-Governance (FR-005) assertions. A separately captured
CLI process smoke satisfies Constitution VI's real-evidence expectation for the
shipped executable path without making the assertion suite depend on subprocess
timing.

**Alternatives considered**: (a) Drive the smoke entirely by spawning the CLI
process — rejected as the primary path: slower, more brittle, and harder to make
byte-deterministic for the two-run comparison, though retained as captured
evidence. (b) Add a new dedicated CLI test project — rejected: unnecessary new
project for a single smoke that the existing harness already supports.

## D2 — Preventing doc/command drift (FR-014)

**Decision**: The smoke asserts the canonical stage order and the `nextAction`
pointer each command emits, and the quickstart documents that exact chain. A
focused assertion ties each documented stage's "next" pointer to the value the
command actually returns, so a behavioral change to the lifecycle ordering or
next-action pointers breaks the smoke.

**Rationale**: This keeps the prose quickstart honest against real command
behavior without building a brittle Markdown parser. It directly satisfies FR-014
and Constitution VII (one shared contract; docs are not a second source of
truth).

**Alternatives considered**: Parsing the quickstart Markdown and diffing against
command output — rejected as over-engineered and fragile. Manual review only —
rejected: it does not prevent silent drift as the commands evolve.

## D3 — Documentation locations and format

**Decision**: Ship three Markdown docs under `docs/` with FsDocs-style
frontmatter matching `docs/initial-implementation-plan.md`:
`docs/quickstart.md` (consumer init→ship walkthrough),
`docs/migration-from-spec-kit.md` (additive migration guide), and
`docs/adopting-governance.md` (optional Governance-after-init note). Cross-link
all three from `docs/index.md` and add the quickstart + migration links to the
`README.md` Workflow section.

**Rationale**: `docs/` is the established home for SDD documentation and the
roadmap explicitly lists quickstart and migration docs as Phase 9 deliverables.
Three focused docs keep each concern discoverable; the adoption note is its own
file because the optional-Governance boundary is a distinct audience concern.
Frontmatter consistency keeps the docs renderable by the same toolchain as the
existing docs without coupling generic SDD content to FS.GG.Rendering specifics
(CLAUDE.md boundary rule).

**Alternatives considered**: A single combined bootstrap doc — rejected: it
buries the migration and Governance-adoption audiences. Placing docs under
`docs/reference/` — rejected: that directory holds imported external reference
material, not first-party SDD guidance.

## D4 — Migration mapping (additive, non-destructive)

**Decision**: The migration guide describes additive steps only:
`fsgg-sdd init` creates the `.fsgg` configuration, `work/` root, and `readiness/`
root; the maintainer then maps each existing Spec Kit feature's `spec.md`,
`plan.md`, clarifications, checklist, tasks, and evidence onto the corresponding
`work/<id>/` authored sources, authoring them through the `fsgg-sdd` commands
rather than hand-copying. Existing `specs/` and `.specify/` content is left
unchanged, standard Spec Kit remains a valid workflow, and the steps are safe to
re-apply. Spec Kit artifacts with no direct SDD equivalent are represented in the
nearest SDD source or explicitly deferred, never deleted.

**Rationale**: Satisfies FR-007/FR-008/FR-009 and the Phase 9 exit criterion that
existing Spec Kit users have a documented migration path while the SDD repository
itself keeps using standard Spec Kit. Additivity removes adoption risk.

**Alternatives considered**: An automated migration command that rewrites
artifacts — rejected: out of scope (FR-012 forbids a new command here), and a
destructive transform would violate the non-destructive requirement. A
one-directional "abandon Spec Kit" cutover — rejected: contradicts preserving
Spec Kit as valid.

## D5 — Optional Governance adoption after init (FR-010/FR-011)

**Decision**: `docs/adopting-governance.md` documents that `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` are Governance-owned and added
after `fsgg-sdd init` as an additive layer; every SDD lifecycle command stays
usable whether those files are present, absent, or incomplete, and SDD never
evaluates or enforces Governance routing, freshness, profiles, gates, audit, or
release decisions. The smoke includes a variant that places present-but-
incomplete Governance files and asserts every command still succeeds.

**Rationale**: Encodes the product's central constraint (useful before
Governance, strict at protected boundaries once adopted) and verifies it. Matches
the CLAUDE.md ownership boundary and FR-016.

**Alternatives considered**: Documenting Governance setup in detail — rejected:
Governance owns its own schemas and setup; SDD only documents the boundary and
that adoption is additive and optional.

## D6 — Out-of-scope confirmations

**Decision**: Runtime product templates and FS.GG.Rendering template-provider
delegation for generating product runtime code are out of scope (the SDD/
Rendering ownership boundary; `init` already provides the lifecycle skeleton).
Governance-owned routing, effective-evidence freshness, profiles, gates, audit,
and release behavior are out of scope. The smoke asserts the lifecycle needs
nothing beyond the SDD projects (no Rendering package, no monorepo checkout).

**Rationale**: Keeps the feature within SDD ownership per CLAUDE.md and the
roadmap's "optional" framing of template providers, and keeps the bootstrap
promise (FR-013) verifiable.

**Alternatives considered**: Including a new-product runtime template now —
rejected: crosses into FS.GG.Rendering / runtime ownership and is marked optional
in the plan; deferring it keeps this feature cohesive and the ownership boundary
intact.

## D7 — No new public surface

**Decision**: The feature adds no new `fsgg-sdd` command, lifecycle stage, or
structured schema, and changes no `.fsi` signature or public API baseline. The
only F# artifact is the new smoke test exercising existing public surfaces.

**Rationale**: The bootstrap experience is documentation plus verification over a
complete surface (FR-012). Keeping the public contract unchanged means
`SurfaceBaselineTests` stay green unmodified and the change tier's only contracted
content is the user-facing docs and the verification harness.

**Alternatives considered**: Adding a `fsgg-sdd quickstart` helper command —
rejected: out of scope and unnecessary; the quickstart is documentation over the
existing commands, not a new command.

## T003 — Canonical stage map (working note, not a shipped artifact)

Derived read-only from `src/FS.GG.SDD.Commands/` (`CommandTypes.nextLifecycleCommand`,
the per-stage `run*` helpers in `tests/.../TestSupport.fs`, and the per-command
report tests). This is the single source of truth the quickstart (US1) and the
smoke (US2) both consume so the docs and the test cannot drift from command
behavior (FR-014). `<id>` is the work id; readiness views live under
`readiness/<id>/`.

| Stage | Authored source written | Generated readiness view refreshed/reported | Emitted next action (`NextAction.Command` / `nextLifecycleCommand`) |
|---|---|---|---|
| `init` | `.fsgg/` config, `work/`, `readiness/`, `CLAUDE.md`/`AGENTS.md`, agent targets | — | `charter` |
| `charter` | `work/<id>/charter.md` | `work-model.json` (refreshes when sources complete; else reports `missing`) | `specify` |
| `specify` | `work/<id>/spec.md` | `work-model.json` | `clarify` |
| `clarify` | `work/<id>/clarifications.md` | `work-model.json` | `checklist` |
| `checklist` | `work/<id>/checklist.md` | `work-model.json` | `plan` |
| `plan` | `work/<id>/plan.md` + `work/<id>/contracts/` | `work-model.json` | `tasks` |
| `tasks` | `work/<id>/tasks.yml` | `work-model.json` | `analyze` |
| `analyze` | — | `readiness/<id>/analysis.json` (+ `work-model.json`) | `evidence` (action id `analysis.next.implement`) |
| `evidence` | `work/<id>/evidence.yml` | `work-model.json` | `verify` (action id `evidence.next.verify`) |
| `verify` | — | `readiness/<id>/verify.json` (+ `work-model.json`) | `ship` (action id `verify.next.ship`) |
| `ship` | — | `readiness/<id>/ship.json` (+ `work-model.json`) | `None` (action id `ship.next.protectedBoundary`; points to the Governance-owned protected-boundary handoff) |
| `agents` (cross-cutting) | — | `readiness/<id>/agent-commands/<target>/` (`guidance.json`, `commands.md`, `skills.md`) for each configured target (`claude`, `codex`) | `None` (action id `agentsGenerated`; `nextLifecycleCommand = None`) |
| `refresh` (cross-cutting) | — | `readiness/<id>/summary.md` plus a current cross-view report over `work-model`/`analysis`/`verify`/`ship`/`agent-commands`/`summary` | `None` (`nextLifecycleCommand = None`) |

Generated views are outputs whose currency comes from running the generators
(`refresh`/`agents`), not from file presence alone (FR-015). Governance
(`.fsgg/policy.yml`, `.fsgg/capabilities.yml`, `.fsgg/tooling.yml`) is never
required or evaluated by any stage (FR-005, FR-016).
