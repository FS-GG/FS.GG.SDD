# Implementation Plan: Early-Stage Agent Guidance Bootstrap

**Branch**: `049-early-stage-agent-guidance` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/049-early-stage-agent-guidance/spec.md`

## Summary

A consumer agent driving the TestSpec tutorial found the one piece of SDD-generated
authoring guidance (`commands.md` / `skills.md` from `fsgg-sdd agents` / `refresh`)
**unavailable at exactly the hardest-to-author stages** — `charter`, `specify`,
`clarify`, `checklist` — because both generators derive from
`readiness/<id>/work-model.json`, which only exists once `verify`/`ship` has run. So
an early-stage author hits a bare hard block (`agents.missingWorkModel` /
`refresh.blockedUpstreamView`) and gets nothing: a chicken-and-egg gap
(FS-GG/FS.GG.SDD#40, epic FS-GG/.github#74 §2.2).

This feature closes the gap with **two channels** (FR-010), each landing at a seam
already located in the code:

1. **Static channel (FR-010a).** `fsgg-sdd init` seeds one new authored skeleton
   file, **`.fsgg/early-stage-guidance.md`**, alongside `.fsgg/constitution.md`. It
   is a generic, deterministic, no-clobber-on-re-run body covering the four
   pre-work-model stages: the `fsgg-sdd` command per stage, the required section
   headings each stage's artifact must contain, the stable-id formats those
   artifacts use, and the two load-bearing authoring contracts that previously
   forced decompilation — the **§1.1 acceptance coverage line** and the **§1.2
   `evidence.yml` rule** (`docs/reference/authoring-contracts.md`, published under
   FS-GG/FS.GG.SDD#38). It reuses the exact `init` mechanism that already makes
   `constitutionText` byte-identical and no-clobber: an embedded F# string literal
   written with `ArtifactWriteKind.AgentGuidanceTarget` (`Foundation.fs:208`,
   `canOverwrite` `CommandEffects.fs:42-48`). A single generic file is inherently
   Claude/Codex-parity-safe (FR-009).

2. **Generated channel (FR-010b).** `fsgg-sdd agents` and `fsgg-sdd refresh` stop
   dead-ending when `work-model.json` is absent. At that one branch
   (`HandlersAgents.fs:211-212`; `HandlersRefresh.fs:126-148` / `:322-330`) the
   **missing** work model is no longer a blocking error — it is reclassified as a
   recognized, navigable **early-stage** state. The command emits clearly-labeled
   best-effort guidance derived **only from the early artifacts that actually
   exist** (which of charter/spec/clarifications/checklist are present, and the next
   lifecycle command), plus a `NextAction` routing the author to
   `.fsgg/early-stage-guidance.md`. Outcome becomes non-blocking (exit 0) so it is
   never a dead end (SC-002).

**Invariant preserved (FR-008 / FR-011 / SC-006).** The early-stage path triggers
**only when `work-model.json` is absent**. It writes **no** digest-stamped
`guidance.json` / `commands.md` / `skills.md`; the best-effort guidance lives in the
`CommandReport` and is explicitly early-stage-labeled, never marked or digest-stamped
as the full projection. The moment the work model is buildable, the existing
generators run unchanged and their views are byte-identical to their pre-feature
output. Genuine defects (`malformedWorkModel`, `staleWorkModel`, a malformed existing
view) keep blocking exactly as today — only the *missing* case is reclassified.

**Change tier**: **Tier 1 (contracted)** — adds one authored skeleton artifact
(`.fsgg/early-stage-guidance.md`), changes the `agents`/`refresh` command-output
semantics for the missing-work-model case (severity → advisory, exit 1 → 0, new
`NextAction`), adds the early-stage advisory diagnostics, updates both agent surfaces
(CLAUDE.md / AGENTS.md), and updates the release schema-reference. No Governance
contract, no rendering/provider identity, no parser-grammar relaxation.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: Existing in-repo only — `FS.GG.SDD.Artifacts`
(`Identifiers`, lifecycle artifacts, standard-section lists, currency engine),
`FS.GG.SDD.Commands` (MVU workflow, `Foundation.initEffects`, `HandlersAgents`,
`HandlersRefresh`, `CommandReports`, serialization/rendering), `FS.GG.SDD.Cli`
(`Program.fs` dispatch / exit codes). No new packages.

**Storage**: Files only — the new authored `.fsgg/early-stage-guidance.md`, the
already-seeded `.fsgg/*`, the (absent, in this scenario) generated
`readiness/<id>/work-model.json`, and command-report stdout/stderr.

**Testing**: xUnit with hand-written `Assert.*`; inline triple-quoted golden strings
plus on-disk fixture trees under `tests/fixtures/**`. Determinism convention is
**generate-twice byte-comparison** (`AgentsCommandTests.fs:186-197`), not checked-in
file snapshots. Drift-guard convention is the live-parser cross-check
(`AuthoringDocsContractTests`) from feature 046.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`).

**Project Type**: Single project — CLI lifecycle product (libraries + CLI).

**Performance Goals**: N/A (deterministic, offline; not a hot path).

**Constraints**: The seeded guidance MUST be byte-deterministic (no date / timestamp
/ random / repo / provider token, mirroring `constitutionText` `Foundation.fs:81-85`)
and contain **zero dangling references** (FR-007, SC-003); the early-stage
`agents`/`refresh` report MUST be deterministic (SC-004); `--rich` stays
presentation-only and degrades to zero ANSI; no Governance contract or dependency
change; no FS.GG.Rendering/provider identity in generic SDD; the full
work-model-derived guidance contract is unchanged (SC-006).

**Scale/Scope**: One new ~120-line embedded literal + one `init` effect; one
reclassified branch each in `agents` and `refresh`; two new advisory diagnostics + a
`NextAction`; one drift-guard test that pins the guidance against the live contract.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Public-surface deltas are the
  two early-stage advisory diagnostic constructors + the `NextAction` ActionId in
  `CommandReports.fsi`, and (if introduced) early-stage disposition string constants.
  Each behavior gets a failing-first test before implementation; the two tests that
  encode today's blocking behavior (`AgentsCommandTests`, `RefreshCommandTests`) are
  rewritten. **PASS**.
- **II. Structured Artifacts Are the Machine Contract**: The seeded
  `.fsgg/early-stage-guidance.md` is an **authoring surface** (Markdown), explicitly
  *not* a machine contract and *not* a second source of truth — its facts are a
  read-only restatement of the authoritative `Identifiers`, standard-section lists,
  `nextLifecycleCommand`, and `docs/reference/authoring-contracts.md`, pinned by a
  drift-guard test so prose can never diverge from the structured contract. The
  best-effort `agents`/`refresh` guidance is report prose, never digest-stamped as
  the structured work-model projection (FR-008/FR-011). **PASS**.
- **III. Visibility Lives in `.fsi`**: `CommandReports.fsi` gains the new diagnostic
  constructors + ActionId; new embedded literal and path constant in `Foundation.fs`
  are internal workflow surface (no `.fsi`). `PublicSurface.baseline` for
  `FS.GG.SDD.Commands` updated accordingly. `serializeReport`/`renderText`/`resolve`
  signatures unchanged. **PASS**.
- **IV. Idiomatic Simplicity**: A static string literal and pure transitions over
  already-loaded snapshots; no clever abstractions, operators, or reflection.
  **PASS**.
- **V. Elmish/MVU Boundary**: No new I/O edge. The seed reuses `init`'s existing
  `WriteFile` effect; the `agents`/`refresh` changes are pure transitions over
  snapshots already loaded by the upstream planning phase. **PASS**.
- **VI. Test Evidence Mandatory**: Failing-first tests over real fixture trees —
  init seeds the file / re-run no-clobbers / byte-identical twice; the drift-guard
  (every heading/id/command/path/contract the guidance names resolves); `agents` and
  `refresh` over an early-only fixture yield an actionable, early-stage-labeled,
  exit-0 result with the pointer and no digest-stamped view; genuine
  malformed/stale work model still blocks; SC-006 buildable-work-model regression
  (existing goldens unchanged); determinism of the early-stage report. **PASS**.
- **VII. Agent + Human Share One Contract**: One generic guidance file (no
  Claude/Codex divergence, FR-009); the best-effort guidance flows through the same
  `CommandReport` consumed by CLI/agents/CI; CLAUDE.md and AGENTS.md updated in
  lockstep. **PASS**.
- **VIII. Observability And Safe Failure**: The early-stage state is a *recognized,
  navigable* disposition with an actionable `NextAction`, not a silent success and
  not a non-actionable block; malformed input (bad work model) still fails fast; an
  incomplete result is explicitly labeled early-stage and never reported complete
  (FR-006). **PASS**.

Engineering-constraint checks: stays `net10.0`; package namespaces unchanged; no
FS.GG.Rendering/provider identity introduced; SDD remains useful without Governance
and adds no Governance dependency. **No violations → Complexity Tracking omitted.**

## Project Structure

### Documentation (this feature)

```text
specs/049-early-stage-agent-guidance/
├── plan.md              # This file
├── research.md          # Phase 0 — the delivery + representation decisions
├── data-model.md        # Phase 1 — entities, dispositions, state transitions
├── quickstart.md        # Phase 1 — US1–US3 validation scenarios
├── contracts/
│   ├── early-stage-guidance-file.md      # the seeded static guidance contract
│   ├── agents-refresh-early-stage.md     # the navigable early-stage command result
│   └── guidance-drift-guard.md           # the no-dangling-reference cross-check
├── checklists/          # (pre-existing) requirements.md
├── spec.md
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandWorkflow/
│   ├── Foundation.fs                # NEW earlyStageGuidanceText literal + path
│   │                                #   constant; one WriteFile in initEffects
│   │                                #   (AgentGuidanceTarget → deterministic, no-clobber)
│   ├── HandlersAgents.fs            # missing-work-model branch (:211-212) →
│   │                                #   early-stage advisory + pointer; write no views
│   └── HandlersRefresh.fs           # early all-blocked path (:126-148) and the
│                                    #   blocked/missing downstream arms (:322-330) →
│                                    #   navigable early-stage state + pointer
├── CommandReports.fs / .fsi         # agentsEarlyStageGuidance / refreshEarlyStageGuidance
│                                    #   advisory diagnostics; early-stage NextAction
│                                    #   ActionId; missing-work-model no longer blocks
└── CommandTypes.fs                  # (if needed) early-stage disposition string consts

tests/FS.GG.SDD.Commands.Tests/
├── InitCommandTests.fs              # seeds .fsgg/early-stage-guidance.md; re-run
│                                    #   no-clobbers; byte-identical twice
├── AgentsCommandTests.fs            # rewrite missing-work-model: actionable early-stage
│                                    #   result, exit 0, pointer, no guidance.json written;
│                                    #   malformed-work-model still blocks; SC-006 unchanged
├── RefreshCommandTests.fs           # rewrite all-blocked early path → navigable result
└── EarlyStageGuidanceContractTests.fs # NEW drift-guard: every heading/id/command/
                                      #   path/contract the guidance names resolves
tests/FS.GG.SDD.Cli.Tests/
├── (help/report rendering)          # early-stage report json/text/rich projection
└── PublicSurface.baseline           # FS.GG.SDD.Commands baseline updated

docs/
├── reference/authoring-contracts.md # cross-link the early-stage guidance (optional)
└── release/schema-reference.md      # note the early-stage advisory dispositions and
                                     #   that .fsgg/early-stage-guidance.md is an
                                     #   authored skeleton seed (like .fsgg/constitution.md),
                                     #   not a catalog entry

CLAUDE.md / AGENTS.md                # mention .fsgg/early-stage-guidance.md + the
                                     #   navigable early-stage agents/refresh behavior
```

**Structure Decision**: Single-project layout (existing). The feature touches the
`init` seed (Channel A), the two early-stage command branches (Channel B), the report
contract, and the agent surfaces. No new project, and — matching the
`.fsgg/constitution.md` precedent (which carries no release-catalog entry) — **no new
release-catalog `catalog[]` entry**: the seeded file is an authored skeleton artifact,
not a produced lifecycle view.

## Phase 0 — Research

See [research.md](./research.md). All NEEDS CLARIFICATION resolved:

1. **Static-guidance delivery** → a **seeded skeleton file** (`init`/
   `AgentGuidanceTarget`), not a new subcommand and not a `docs/` page — because the
   guidance must reach a *scaffolded product's* skeleton from stage zero (SC-001), and
   the `.fsgg/constitution.md` seed is an exact, no-clobber, deterministic precedent.
2. **Best-effort representation** → **in-report prose + pointer**, writing **no**
   new on-disk view — the only representation that satisfies FR-008/FR-011 (never a
   second digest-stamped source of truth) while still being "emitted by
   agents/refresh."
3. **Missing vs malformed disposition** → only the **missing** work model is
   reclassified to a non-blocking early-stage advisory; malformed/stale/blocked stay
   blocking errors (Observability VIII, FR-008).
4. **Single vs per-target file** → **one generic file**; behavior is aligned across
   Claude/Codex so per-target duplication would only add drift risk (FR-009).
5. **Catalog treatment** → **uncatalogued authored seed**, mirroring
   `.fsgg/constitution.md`; the schema-reference notes the new advisory dispositions.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the early-stage guidance body, the early-stage
  command disposition, and the missing-work-model state transition.
- [contracts/early-stage-guidance-file.md](./contracts/early-stage-guidance-file.md)
- [contracts/agents-refresh-early-stage.md](./contracts/agents-refresh-early-stage.md)
- [contracts/guidance-drift-guard.md](./contracts/guidance-drift-guard.md)
- [quickstart.md](./quickstart.md) — runnable validation scenarios proving US1–US3.
- Agent context: `CLAUDE.md` SPECKIT marker repointed to this plan.

**Post-design Constitution re-check**: still **PASS** — the design adds one authored
Markdown seed (pinned against the structured contract by a drift-guard), reclassifies
exactly one missing-state branch in two commands from blocking to navigable, and adds
two advisory diagnostics + one `NextAction`. No new I/O edge, no Governance contract,
no provider/rendering identity, determinism and the full-guidance contract preserved.
