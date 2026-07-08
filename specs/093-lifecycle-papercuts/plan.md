# Implementation Plan: Lifecycle Authoring Papercuts (Counters, Task Refs, Atomic Write, Decision Refs, Clarify Title)

**Branch**: `item/164-sdd-lifecycle-papercuts-game-audio-feedb` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/093-lifecycle-papercuts/spec.md`

## Summary

Five defects from the 2026-07-07 FS.GG.Game (§WD3, §WD4) and FS.GG.Audio (§3.7, §3.9) workflow-feedback
reports, resolving FS-GG/FS.GG.SDD#164. Each is independently testable; none depends on another.

1. **Clarify title** — `clarificationTemplate` resolves its title from `--title` or the humanized work
   id, never from the `spec.md` it points at. One expression. (US1, FR-001)
2. **Atomic write** — the single `WriteFile` interpreter site truncates-then-writes, so a torn artifact
   is observable. Temp sibling + atomic rename. Protects every authored artifact at once. (US2, FR-005..010)
3. **Ambiguity counters** — `unresolvedAmbiguityCount` is a `spec.md`-body regex that never reads
   `clarifications.md`, so it cannot be zeroed by resolving anything. Removed from the model and the
   report. (US3, FR-002..004)
4. **Decision refs** — a `DEC-###`'s `FR`/`US`/`AC` refs are dropped on four independent paths, one of
   which is that the three `Related*Ids` fields have **zero read sites** in `src/`. Threaded through to
   `work-model.json` and the task graph; the unread copies deleted. (US4, FR-011..015)
5. **Task refs** — `sourceIds` and `decisions` are a generated bucket and an authored typed field, and
   four consumers disagree about which is canonical. Typed fields win; `SourceIds` becomes a derived
   union; the emitter writes only the residual. (US5, FR-016..022)

**Change tier: Tier 1.** Artifact-layout change to the authored `tasks.yml` surface, additive keys on
`work-model.json`'s `DecisionEntry`, and a `CommandReport` counter removal. Six `.fsi` files change, so
Constitution §I/§III put the signatures first.

**Not in this feature.** `outcome: noChange` → FS-GG/FS.GG.SDD#183 (split out; a new `CommandOutcome`
case cascades through `Cli/Rendering.fs`, the `ReleaseContract.fs` inventory, and ~20 test files, and
collides with FS-GG/FS.GG.SDD#177's touch-set). The three non-interpreter `File.WriteAllText` sites
(`Cli/Program.fs:138`, `Cli/RegistrySkillManifest.fs:75`, `Validation/ValidationRunner.fs:103`) — same
bug class, different touch-set. The `requestTitle` shape at `ChecklistPlanAuthoring.fs:203`/`:917` and
`TaskGraphAuthoring.fs:511` — noted for follow-up, not swept in.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution default).

**Primary Dependencies**: None new. YamlDotNet (reader, already present); `System.Text.Json`
(`Utf8JsonWriter`, already present); `System.IO.File.Move(overwrite)` (BCL).

**Storage**: `work/<id>/tasks.yml` (authored YAML, layout changes), `work/<id>/clarifications.md`
(authored Markdown, front-matter title changes), `readiness/<id>/work-model.json` +
`agent-commands/**` (generated views, bytes move).

**Testing**: xUnit. New `tests/FS.GG.SDD.Commands.Tests/CommandEffectsTests.fs` — the first direct test
of the effect interpreter. Extensions to `ClarifyCommandTests`, `SpecifyCommandTests`, `TasksCommandTests`,
`ClarificationArtifactTests`, `TasksArtifactTests`, `WorkModelTests`, `ExampleArtifactsContractTests`,
`TextProjectionTests`, `CommandReportJsonTests`, `RichRenderingTests`.

**Target Platform**: Cross-platform CLI (Linux, macOS, Windows).

**Project Type**: Single project. `FS.GG.SDD.Artifacts` (model + parsers + serialization) under
`FS.GG.SDD.Commands` (workflow, effects, report projections) under `FS.GG.SDD.Cli` (rendering).

**Performance Goals**: N/A. The derived `SourceIds` adds one `List.concat` per task. The atomic write
adds one file create + one rename per artifact write.

**Constraints**:
- The temp file must be a **sibling** of the destination (same volume ⇒ `rename` is atomic) and must
  not survive a failed write.
- `allTaskDispositionIds` must produce the identical set for every existing `tasks.yml` (it already
  computes the same union — this is satisfied by construction, and tested as a guard).
- Emitter idempotence: `emit → parse → emit` must be a fixpoint after one step.
- `remainingAmbiguityCount` / `blockingAmbiguityCount` count *lines*, not ids. Widening
  `RemainingAmbiguity.AmbiguityId` to a list must not move either count.

**Scale/Scope**: ~14 source files (6 with `.fsi` pairs), ~12 test files, 1 new test file, 1 `.fsproj`
compile entry, 2 docs, plus golden/fixture regeneration.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Followed. Six `.fsi` files change
  (`Specification.fsi`, `Clarification.fsi`, `RequirementModel.fsi`, `Task.fsi`, `WorkModel.fsi`,
  `CommandTypes.fsi`). Each signature lands before its `.fs` body; semantic tests through the public
  surface (`parseTasks`, `parseClarificationArtifact`, the `clarify`/`tasks` commands) precede the
  implementation. ✅
- **II. Structured Artifacts Are the Machine Contract**: Strengthened. Three of the five defects are
  precisely "the Markdown/YAML says something the model does not mean." `tasks.yml`'s two-fields-one-fact
  ambiguity is resolved in favor of one canonical authored surface; `work-model.json` gains the decision
  refs the artifact always carried. Prose and structured data are brought back into agreement. ✅
- **III. Visibility Lives in `.fsi`, Not in `.fs`**: Six signature files change, all narrowing or
  additive. Two `PublicSurface.baseline` files may move (`Commands`, `Cli`) — re-captured deliberately,
  and `fsgg-sdd surface --check` must exit 0. ✅
- **IV. Idiomatic Simplicity**: `List.concat |> List.distinct |> List.sort`; `Option.orElseWith`;
  `Set.contains` for the residual filter; one `try/finally`. No new abstraction, no operators, no
  reflection. `deriveGuidanceModel` gets *shorter* (`relatedIds = task.SourceIds`). ✅
- **V. Elmish/MVU Is the Boundary**: Respected exactly. The atomic write changes only how the
  effect-interpreting edge *performs* an existing `WriteFile` effect; the effect type, the pure
  `update`, and the effect log are untouched. No new effect, no new state. The other four fixes are pure
  functions over parsed facts. ✅
- **VI. Test Evidence Is Mandatory**: Every FR gets a test that fails before and passes after. The
  atomic write gets the interpreter's **first direct test** (fault injection over a real filesystem, no
  mocks). Golden regeneration is a reviewed deliverable (FR-024), not a `--update` waved through. ✅
- **VII. Agent And Human Workflows Share One Contract**: `fs-gg-sdd-clarify` and `fs-gg-sdd-tasks` skill
  bodies do not state where the front-matter `title:` comes from, nor that `sourceIds:` must be emitted.
  No skill body changes ⇒ no `skill-manifest.json` / `registry/skills.yml` sha reconcile.
  `docs/reference/authoring-contracts.md` and `docs/examples/lifecycle-artifacts/tasks.yml` are updated
  and stay pinned by `AuthoringDocsContractTests` / `ExampleArtifactsContractTests`. ✅
- **VIII. Observability And Safe Failure**: No gate weakens. `unknownClarificationReference` still blocks
  (FR-013 — and a regression test proves FR-011's threading does not route around it).
  `unresolvedBlockingAmbiguity` gets *more* information (every AMB per line, not the first).
  `unsafeOverwrite` and `ArtifactOperation.NoChange` are evaluated before the write and are untouched. A
  failed atomic commit surfaces on the existing `DiagnosticError` path and leaves the prior bytes intact
  — strictly safer failure than today. ✅

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/093-lifecycle-papercuts/
├── plan.md              # This file
├── research.md          # Phase 0: R1 counter, R2 refs, R3 sourceIds, R4 atomic write, R5 title
├── data-model.md        # Phase 1: D1–D7, the seven deltas
├── quickstart.md        # Phase 1: before/after for each fix, how to verify
├── tasks.md             # Phase 2: dependency-ordered task list
└── spec.md              # Feature specification
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
├── Specification.fs / .fsi      # D1: drop UnresolvedAmbiguityCount (field + computation)
├── Clarification.fs / .fsi      # D2: RemainingAmbiguity.AmbiguityIds (list)
│                                # D4: drop the unread Related*Ids copies
├── RequirementModel.fs / .fsi   # D3: Decision gains RequirementRefs/StoryRefs/AcceptanceRefs
└── Task.fs / .fsi               # D5: SourceIds derived on parse

src/FS.GG.SDD.Artifacts/
├── WorkModel.fs / .fsi          # D3: DecisionEntry refs (+ JSON re-parse)
│                                # D5: deriveGuidanceModel relatedIds = task.SourceIds
└── Serialization.fs             # D3: writeDecision emits the three ref arrays

src/FS.GG.SDD.Commands/
├── CommandEffects.fs            # D6: writeFileAtomic — the ONLY source change for US2
├── CommandTypes.fs / .fsi       # D1: SpecificationSummary drops the counter
├── CommandSerialization.fs      # D1: drop the json key
├── CommandRendering.fs          # D1: drop the text key
└── CommandWorkflow/
    ├── EarlyStageAuthoring.fs   # D7: clarificationTemplate title precedence
    │                            # D1: :581 summary construction
    │                            # D2: :1543,:1674 unresolvedBlockingAmbiguity id list
    └── TaskGraphAuthoring.fs    # D5: residual emitter, clarificationDecisionTasks refs

tests/FS.GG.SDD.Commands.Tests/
├── CommandEffectsTests.fs       # NEW — first direct test of the interpreter (fault injection)
└── FS.GG.SDD.Commands.Tests.fsproj   # compile entry for the above

docs/
├── examples/lifecycle-artifacts/tasks.yml   # reconciled field semantics
└── reference/authoring-contracts.md         # ditto
```

**Structure Decision**: Single project, existing layers respected. Artifacts owns *what a task / decision
/ ambiguity is*; Commands owns *how one is authored, rendered, and committed*. The atomic write lands in
`CommandEffects.fs` — the effect-interpreting edge — and nowhere else, because that is the one place the
constitution allows I/O mechanics to live.

**Declared touch-set (ADR-0021)**, matching `Paths:` on FS.GG.SDD#164. `fsgg-coord overlap
FS.GG.SDD#164 FS.GG.SDD#177` → **DISJOINT** (re-verified after both declarations were corrected; #177's
original `src/**, tests/**` made every item overlap).

Known shared file *outside* both declarations: `.specify/feature.json`, which every feature branch
rewrites (`092` vs `093`). `overlap` cannot see it. The second PR to merge takes a one-line conflict,
resolved by taking the merging branch's value. Flagged, not silently absorbed.

## Design Detail

See [data-model.md](./data-model.md) for the seven deltas (D1–D7) and
[research.md](./research.md) for why each is shaped the way it is. The three decisions worth restating:

### Why `unresolvedAmbiguityCount` is deleted, not recomputed

It could be recomputed from `clarifications.md`. But `remainingAmbiguityCount` already carries exactly
that meaning and already gates. A third counter that agrees adds no information; one that disagrees is
the bug being fixed. And the field is deleted from `SpecificationFacts` as well as from the report — a
computed-but-unread field is the very defect FR-015 removes elsewhere in this same change, and leaving
one behind while deleting five others would be incoherent.

Verified safe to remove (research R1a): `ReleaseContract.fs` freezes only *top-level* report keys, and
neither `docs/release/` nor `GovernanceHandoff.fs` mentions ambiguity.

### Why the typed fields win over `sourceIds`

The shipped example — validated against the live parser on every build — authors `requirements:` and
`decisions:` and **no `sourceIds:`**. Making `sourceIds` authoritative would invalidate the documentation
the product ships. Deriving it instead makes every consumer agree with the documented shape, and
`allTaskDispositionIds` (the one consumer that already read the union) keeps producing an identical set
by construction.

### Why fault injection, not a concurrency test

"No observer sees a prefix" needs a reader racing the writer: flaky, and it would be testing `rename(2)`
rather than our code. The property that actually protects the author is *a failed write leaves the prior
bytes intact and no residue* — directly assertable, deterministic. Plus a structural assertion that no
`File.WriteAllText` targets a destination path.

## Verification Plan

| FR | Test | Location |
|---|---|---|
| FR-001 | title precedence: `--title` > spec front matter > work id; blank front matter falls back; 089 blocked-seed path | `ClarifyCommandTests` |
| FR-002 | `unresolvedAmbiguities` absent from text; `unresolvedAmbiguityCount` absent from json | `TextProjectionTests`, `CommandReportJsonTests` |
| FR-003 | remaining/blocking counts and the gate they drive are byte-identical | `ClarifyCommandTests`, `LintTests` |
| FR-004 | three projections agree | `TextProjectionTests`, `RichRenderingTests` |
| FR-005/006 | no `File.WriteAllText` on a destination path | `CommandEffectsTests` (structural) |
| FR-007 | fault-injected commit: prior bytes intact, no `.tmp` residue, `DiagnosticError` surfaces | `CommandEffectsTests` |
| FR-008 | temp is a sibling, dot-prefixed, matches no lifecycle glob | `CommandEffectsTests` |
| FR-009 | identical content ⇒ `NoChange`, no write; `unsafeOverwrite` unchanged | `CommandEffectsTests`, `SurfaceCommandTests` |
| FR-010 | `dryRun` writes nothing, temp included | `CommandEffectsTests` |
| FR-011 | `DEC-003` naming `FR-007`,`FR-001`,`AC-005` → all three on the work model, sorted | `WorkModelTests`, `ClarificationArtifactTests` |
| FR-012 | a line naming two AMB ids records both | `ClarificationArtifactTests` |
| FR-013 | multi-ref line containing `FR-999` still blocks with `unknownClarificationReference` | `ClarifyCommandTests` |
| FR-014 | derived task's `requirements:` is `[FR-001, FR-007]`, not `[]` | `TasksCommandTests` |
| FR-015 | no field is parsed-stored-exposed-unread | compile: the fields are gone or read |
| FR-016/017 | typed-refs-only task derives `SourceIds`; explicit `sourceIds:` retained in the union; `schemaVersion` = 1 | `TasksArtifactTests` |
| FR-018 | residual-only emission; two runs byte-identical | `TasksCommandTests` |
| FR-019 | a `DEC-###` task does not carry the id in both fields | `TasksCommandTests` |
| FR-020 | `relatedIds` includes a `sourceIds`-only id | `WorkModelTests` |
| FR-021 | shipped example's tasks resolve in `evidence` + `verify` | `ExampleArtifactsContractTests` |
| FR-022 | `allTaskDispositionIds` set identical for every fixture | `TasksArtifactTests` |
| FR-024 | golden diff reviewed: digest-only, except `relatedIds` + `tasks.yml` normalization | PR review |
| FR-025 | docs stay pinned | `AuthoringDocsContractTests`, `ExampleArtifactsContractTests` |

Plus: `dotnet test` green, `fsgg-sdd surface --check` exit 0, `PublicSurface.baseline` moves only where
the six declared `.fsi` changes require.

## Agent-facing behavior

- `clarify` skeletons an agent generates now carry the spec's title — the agent no longer has to know to
  pass `--title` on the 089 blocked-seed path.
- Generated `agent-commands/**` guidance gains `relatedIds` entries reachable only via `sourceIds:`
  (e.g. scope boundaries). Bytes move; semantics improve.
- `work-model.json` decision entries gain three additive ref arrays. Consumers that ignore unknown keys
  are unaffected.
- One report key disappears (`specification.unresolvedAmbiguityCount`). No agent skill references it.

## Governance integration

None. No Governance runtime, no `contractVersion`, no `governance-handoff.json` field change (that
artifact never carried an ambiguity counter). `docs/release/release-readiness.json` is untouched — this
feature produces no new lifecycle artifact.

## Complexity Tracking

Not required — Constitution Check passed with no violations.

The one item worth naming: this feature bundles five unrelated defects because they arrived as one board
item with one declared touch-set. Splitting them into five PRs would serialize five overlapping
touch-sets against each other (they share `EarlyStageAuthoring.fs`, `CommandTypes.fs`, and
`TaskGraphAuthoring.fs`), which ADR-0021 forbids parallelizing. The sixth bullet *was* split out —
precisely because its touch-set reached outside this set and into FS.GG.SDD#177's.
