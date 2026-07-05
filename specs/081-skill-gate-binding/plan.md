# Implementation Plan: Bind SDD authoring skills to the CLI gate grammar

**Branch**: `081-skill-gate-binding` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/081-skill-gate-binding/spec.md`

## Summary

Following an `fs-gg-sdd-*` stage skill verbatim currently produces artifacts the `fsgg-sdd` gate rejects, because the authored skills (what an author writes *from*) drift from the compiled gate grammar (what accepts/rejects). This feature binds the two surfaces durably: (1) a **skill↔gate doctest** runs a complete, gate-passing **example corpus** through the *real* gate commands and asserts each stage skill's marked example matches the corpus; (2) a **field-list check** binds each skill's required-field statements to a new typed required-keys registry; (3) the specify/evidence skills are corrected and a deferral-bearing evidence example is added; (4) the mislabeled checklist back-reference case is split into its own `missingChecklistBackReference` diagnostic. Approach per [research.md](./research.md): corpus-anchored, hand-authored skills, check-over-codegen, fix the diagnostic at its emission site.

**Change tier**: **Tier 1** (agent-skill contract + a command-output diagnostic identifier + public surface). Delivers spec, plan, tasks, `.fsi` updates, tests, docs, and migration notes.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (per constitution).

**Primary Dependencies**: existing repo only — `FS.GG.SDD.Artifacts` (parsers/diagnostics), `FS.GG.SDD.Commands` (gates, diagnostic constructors, remediation pointers), the `TestSupport` harness in `FS.GG.SDD.Commands.Tests`, xUnit. No new external dependencies.

**Storage**: files only — authored `SKILL.md` bodies, `docs/examples/lifecycle-artifacts/` corpus, `.agents/skills/skill-manifest.json`. No database, no persisted-schema change.

**Testing**: xUnit via `TestSupport.runRequest` (in-process MVU loop) and its per-stage wrappers (`runSpecify`/`runClarify`/`runChecklist`/`runEvidence`). Offline, no network, no Governance runtime (FR-013).

**Target Platform**: cross-platform CLI/library + CI (GitHub Actions), same as the rest of the repo.

**Project Type**: single project — F# CLI/library with a test suite (no frontend/mobile split).

**Performance Goals**: not a hot path; the doctest is a CI test over ~10 skills + one corpus. Whole-suite runtime impact must stay negligible (in-process, no `dotnet` sub-spawn).

**Constraints**: no second source of truth for skill content (Principle VII); `.claude`≡`.codex` byte-identical (mirror guard); seeded set + `skill-manifest` guards stay green; JSON diagnostic contract change is additive only; `PublicSurface.baseline` updated in lockstep.

**Scale/Scope**: 10 stage skills, ~7 corpus artifact files, 1 new diagnostic id, 1 new required-keys registry, 2 new test suites (doctest + field-list check).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec→FSI→Tests→Impl | PASS | Diagnostic split touches public surface; `CommandReports.fsi` `val` added before `.fs` body; corpus/skill work is spec-first via this feature. |
| II. Structured artifacts are the machine contract | PASS | The typed required-keys registry becomes the authoritative contract the prose skills are checked against — directly *reduces* prose-vs-contract drift, the principle's core aim. |
| III. Visibility in `.fsi` | PASS | New `missingChecklistBackReference` constructor gets a `CommandReports.fsi` signature; `PublicSurface.baseline` (Commands + Cli) updated. No `.fs` visibility modifiers. |
| IV. Idiomatic simplicity | PASS | Registry is a plain `LifecycleStage -> string list`; doctest is straight-line test code reusing `TestSupport`. No new abstractions, operators, or CE machinery. |
| V. Elmish/MVU boundary | PASS | No new stateful/I-O workflow — the doctest drives the *existing* MVU command loop through `TestSupport`; no new `Model`/`Msg`/`Effect`. |
| VI. Test evidence mandatory | PASS | The feature *is* test evidence: real-fixture corpus through real gates, plus red-branch demonstration (SC-003). No mocks. |
| VII. Agent+human share one contract | PASS | Skills stay the single authoring surface; the marker is a binding annotation, not content. `.claude`→`.codex` mirror + seeded set + manifest kept coherent. |
| VIII. Observability & safe failure | PASS | The diagnostic split makes a failure name its real cause; the doctest fails fast and names the offending skill/diagnostic. |

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/081-skill-gate-binding/
├── plan.md              # This file
├── research.md          # Phase 0 — the 6 design decisions
├── data-model.md        # Phase 1 — entities: RequiredKeys registry, example marker, corpus
├── quickstart.md        # Phase 1 — how to run/validate the doctest + checks
├── contracts/           # Phase 1 — the example-marker grammar + diagnostic contract
│   ├── example-marker.md
│   └── diagnostic-missing-back-reference.md
├── checklists/
│   └── requirements.md  # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
.claude/skills/fs-gg-sdd-*/SKILL.md      # canonical authoring surface (edited: specify, evidence, clarify?, checklist, authoring-contracts, troubleshooting) + example markers
.codex/skills/fs-gg-sdd-*/SKILL.md       # byte-identical mirror (SkillMirrorTests guard)
.agents/skills/skill-manifest.json       # regenerated (sha256 of edited bodies)
.fsgg/early-stage-guidance.md            # verified/updated for currency (drift guard)

docs/examples/lifecycle-artifacts/       # the gate-passing example corpus (the doctest's input)
├── spec.md  clarifications.md  checklist.md  evidence.yml  ...   # repaired to a coherent gate-passing set; evidence.yml gains a deferral

src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
├── Checklist.fs                         # emit missingChecklistBackReference at the source site (was workModelInconsistent)
├── Diagnostics.(fs/fsi)                 # new diagnostic id/helper if needed
└── (new) RequiredKeys.(fs/fsi)          # requiredFrontMatterKeys / requiredDeferralKeys registry

src/FS.GG.SDD.Commands/
├── CommandReports.fs / .fsi             # missingChecklistBackReference constructor + val
├── CommandReports/DiagnosticConstructors.fs   # constructor + stage-classifier list entries
├── CommandReports/RemediationPointers.fs       # back-reference remediation pointer
└── CommandWorkflow/ChecklistPlanAuthoring.fs   # remap: new id surfaces as itself; ViewGeneration.fs category

tests/
├── FS.GG.SDD.Commands.Tests/
│   ├── SkillGateDoctestTests.fs         # NEW — corpus through real gates + skill↔corpus consistency
│   ├── RequiredFieldContractTests.fs    # NEW — skills/§5-table ↔ RequiredKeys registry
│   ├── ChecklistCommandTests.fs         # + missing-back-ref now asserts missingChecklistBackReference
│   ├── RemediationPointersTests.fs      # new id has a pointer
│   └── PublicSurface.baseline           # + new symbol (sorted)
├── FS.GG.SDD.Cli.Tests/PublicSurface.baseline   # + new symbol
├── FS.GG.SDD.Artifacts.Tests/           # back-ref parser test asserts new id; RequiredKeys unit tests
└── FS.GG.Contracts.Tests/SkillMirrorTests.fs    # stays green (mirror maintained)
```

**Structure Decision**: Single-project layout, unchanged. New code is one small typed registry in `FS.GG.SDD.Artifacts`, one new diagnostic threaded through the existing `Commands` diagnostic plumbing, and two new test suites in `FS.GG.SDD.Commands.Tests`. Authored surfaces (skills, corpus) live where they already live; the doctest reads them from the repo tree.

## Phased delivery (maps to user stories)

The plan is sliced so each user story is an independently shippable increment (per spec priorities):

- **Slice A (US1+US2, P1) — the durable binding**: the example corpus (repaired coherent + deferral-bearing), the `SkillGateDoctestTests` (corpus through real gates + marked-example ↔ corpus consistency), and the example markers in stage skills. This alone delivers the MVP: drift can no longer land. Includes the specify bold→plain FR correction and the evidence deferral documentation because the corpus/doctest force them.
- **Slice B (US3, P2) — field-list truth**: the `RequiredKeys` registry, `RequiredFieldContractTests`, and reconciling the evidence/clarify skills + authoring-contracts §5 table to it.
- **Slice C (US4, P2) — honest diagnostic**: the `missingChecklistBackReference` split across the Artifacts + Commands layers, its remediation pointer, and the baseline/test updates.

Slices B and C are independent of each other and of A's doctest mechanics; A should land first so B/C corrections are caught by the doctest.

## Migration / compatibility notes

- **JSON diagnostic contract**: additive. A new `missingChecklistBackReference` identifier appears; the missing-back-ref case stops emitting `malformedChecklistFrontMatter`. Any consumer keying on the old id for the back-ref case must move — none exist outside SDD (verified: no `.github`/Governance references; only SDD baselines/tests, all updated here).
- **No persisted-schema bump**: `scaffold-provenance` and all `v1` schemas untouched; `skill-manifest` stays schema v1 (only its content sha256 changes).
- **Seeded workspaces**: scaffolded consumers receive the corrected skills on their next `init`/`scaffold`/`refresh` re-mirror; no consumer migration action required (no-clobber policy unchanged).
- **Agent surfaces**: Claude and Codex stay aligned via the byte-identical mirror; `.agents` seed + `skill-manifest` regenerated.

## Risks

- **Corpus not currently gate-passing**: the existing `docs/examples` set passes the *parsers* but has never been run through the *gate commands*; repairing it to full gate coherence (esp. checklist coverage + FR form) may surface latent issues. Mitigation: Slice A lands the doctest and the repair together; the doctest is the acceptance.
- **Marked-example vs corpus divergence for fragments**: some skills show fragments, not whole files. Mitigation: consistency check uses normalized containment (fragment ⊆ corpus file), specified in `contracts/example-marker.md`.
- **Registry vs scattered parsers**: the required-keys registry is new and initially *asserted against* (not consumed by) the parsers. Mitigation: behavioral parser tests prove the gate actually requires each key, so the registry can't lie without a red test.
