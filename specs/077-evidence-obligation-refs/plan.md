# Implementation Plan: Preserve refs on auto-generated evidence obligations

**Branch**: `077-evidence-obligation-refs` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/077-evidence-obligation-refs/spec.md`

## Summary

`fsgg-sdd evidence` scaffolds one skeleton `EvidenceDeclaration` per task. Today
`skeletonEvidenceDeclaration` sets `PlanDecisionRefs = []` unconditionally and derives
`RequirementRefs` only from `task.Requirements`. For a plan-decision task
(`Implement plan decision PD-001`) the task graph puts the `PD-###` id — and the plan decision's
own source ids (often the `FR-###` it traces to) — into `task.SourceIds`, while `task.Requirements`
and `task.Decisions` are both empty. So the scaffolded obligation loses **all** origin lineage and
the author must join `evidence.yml` back to `tasks.yml` by title to classify honestly.

**Approach**: thread the originating task's `SourceIds` onto the in-memory `EvidenceObligation`
(new additive `LinkedSourceIds` field), then in `skeletonEvidenceDeclaration` classify that
lineage by the existing id grammar and route `FR-` → `RequirementRefs` and `PD-` →
`PlanDecisionRefs` (unioned with the refs already carried, sorted and de-duplicated). Routing is
deliberately limited to those two buckets — the origin issue #124 asks scaffolding to preserve;
other lineage ids stay unrouted so the acceptance/clarification/checklist buckets remain `[]` on
scaffolds and the evidence stage's unknown-reference validation surface is not widened (see
research Decision 3). Add an additive `fsgg-sdd evidence --from-tests <path>` flag that seeds each
newly scaffolded obligation with a verification-kind source pointing at `<path>`. No persisted
schema version bump: the declaration ref fields already exist and are merely populated where they
were empty; the change is purely in what values scaffolding writes.

## Technical Context

**Language/Version**: F# on .NET (`net10.0`), per constitution engineering constraints.

**Primary Dependencies**: existing `FS.GG.SDD.Artifacts` (lifecycle artifact types, `Identifiers`
grammar) and `FS.GG.SDD.Commands` (`HandlersEvidence`, MVU command workflow). No new packages.

**Storage**: authored `work/<id>/evidence.yml` (YAML, machine contract) scaffolded by the evidence
stage; task graph `work/<id>/tasks.yml` is the lineage source. Files only; no DB.

**Testing**: existing xUnit test projects with real filesystem fixtures + golden/snapshot fixtures
for command output (per constitution Principle VI). New tests exercise the public evidence handler
over fixtures with plan-decision, requirement, mixed, and clarification-decision tasks.

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`).

**Project Type**: single-project CLI + libraries (`src/FS.GG.SDD.*`).

**Performance Goals**: N/A — scaffolding a bounded per-work-item obligation set; the TD1 scale
point (85 obligations) is trivial. No hot path.

**Constraints**: deterministic, idempotent output (sorted/de-duplicated refs); `--from-tests`
absent ⇒ byte-identical to prior output aside from the now-populated refs; degrade-to-plain for
`--rich` unchanged; no Governance runtime.

**Scale/Scope**: small, contained change in one handler module + one artifact record; two new
tests-worthy behaviors (ref routing, `--from-tests`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Followed. `.fsi` change (add
  `LinkedSourceIds` to `EvidenceObligation` in `Evidence.fsi`; if `--from-tests` needs a new
  request field, update the relevant command-request `.fsi`) precedes `.fs`. Semantic tests
  written against the public evidence handler before the body hardens.
- **II. Structured Artifacts Are the Machine Contract**: The machine contract is the scaffolded
  `evidence.yml` (`EvidenceDeclaration`). Authoritative source of an obligation's origin = the
  task graph's `task.SourceIds` (routed by id grammar). Conflict rule: an **author-authored**
  evidence entry always wins over scaffolding (no-clobber, FR-006); scaffolding only fills
  skeleton entries. Diagnostic surface: the evidence stage's existing report/readiness views; no
  new diagnostic class needed (routing is total — unrecognized id shapes are left unrouted, not
  fatal). **No schema version bump** (fields pre-exist); migration posture documented in
  `data-model.md`.
- **III. Visibility in `.fsi`**: `EvidenceObligation` has an `.fsi`; the added field updates
  `Evidence.fsi` and the public API baseline for `FS.GG.SDD.Artifacts`. Any new command-request
  field updates its `.fsi` + baseline.
- **IV. Idiomatic Simplicity**: plain F# — a grammar-classify `List.fold`/`List.choose` over
  strings using existing `Identifiers.create*` validators; records over classes. No custom
  operators, SRTP, reflection, or CE machinery.
- **V. Elmish/MVU boundary**: both stories are pure transforms in the existing evidence `update` —
  no new `Effect`. `--from-tests` enters through the command-request Model and records a *declared*
  source pointer; it performs **no filesystem I/O** (path existence is a verify-stage concern), so
  the pure core stays I/O-free. Ref routing is likewise pure.
- **VI. Test Evidence Mandatory**: new tests fail before / pass after; real fixtures (plan-decision
  task graph → scaffolded evidence.yml) via the existing harness plus a real CLI subprocess smoke.
  `--from-tests` present/absent/blank paths all covered.
- **VII. One Contract for Agents & Humans**: the change is in the shared CLI artifact; the
  `fs-gg-sdd-evidence` skill doc and `docs/examples` lifecycle artifacts are refreshed so agents
  and humans see the same populated refs.
- **VIII. Observability & Safe Failure**: ref routing never crashes on an unrecognized id shape
  (degrades by leaving it unrouted); a blank `--from-tests` value is treated as absent rather than
  seeding an empty source (FR-009). A bad-but-non-blank path is a declared source that the verify
  stage flags — evidence declares, verify validates.

**Change tier**: **Tier 1** — command output contract / generated-view + public API (`.fsi`) +
new CLI flag change. Requires spec, plan, tasks, `.fsi`, tests, docs, and (here) *no* migration
notes because no persisted schema version changes.

**Gate result**: PASS. No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/077-evidence-obligation-refs/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (evidence.yml scaffolding contract delta)
├── checklists/
│   └── requirements.md  # /speckit-specify output
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/
│   └── LifecycleArtifacts/
│       ├── Evidence.fs          # EvidenceObligation: add LinkedSourceIds (additive)
│       └── Evidence.fsi         # signature for the added field
├── FS.GG.SDD.Commands/
│   └── CommandWorkflow/
│       └── HandlersEvidence.fs  # evidenceObligations: carry task.SourceIds;
│                                # skeletonEvidenceDeclaration: grammar-route refs;
│                                # --from-tests: seed verification source on skeletons
│   └── CommandTypes.fs(.fsi)    # request field for --from-tests (if threaded via request)
└── FS.GG.SDD.Cli/
    ├── Program.fs               # parse `--from-tests <path>` into the request
    └── CommandHelp.fs           # document the flag in `evidence` help

tests/
└── FS.GG.SDD.*.Tests/           # ref-routing + --from-tests fixtures & goldens

docs/
├── examples/lifecycle-artifacts/  # refresh scaffolded evidence.yml example refs
└── (evidence skill / reference)   # fs-gg-sdd-evidence skill note on populated refs
```

**Structure Decision**: Single-project layout (constitution default). The change is localized to
the evidence artifact record (`FS.GG.SDD.Artifacts`) and the evidence handler + CLI parsing
(`FS.GG.SDD.Commands` / `FS.GG.SDD.Cli`). No new project or package.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
