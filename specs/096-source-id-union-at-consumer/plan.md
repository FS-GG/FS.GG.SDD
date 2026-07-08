# Implementation Plan: The Source-Id Union Belongs at the Consumer

**Spec**: [spec.md](./spec.md) · **Issue**: FS-GG/FS.GG.SDD#189 · **Tier**: 1

## Summary

Two consumers of a task's reference fields answer *"which ids does this task touch?"* with the
wrong answer. `WorkModel.deriveGuidanceModel` answers `Requirements @ Decisions`, dropping every
`sourceIds`-only id. `HandlersVerify.verifyEvidenceDispositionViews` answers with the literal `[]`.
Both are read-only outputs behind no validation gate, so both can be widened without moving any
workspace from green to blocked.

The parser stays untouched. That is the whole point: `taskValidationDiagnostics.unknownSources`
gates on `task.SourceIds`, so unioning at `Task.fs` would retroactively subject the typed
`requirements:`/`decisions:` fields to a validation they have never faced — turning a green
`tasks.yml` red with no `schemaVersion` signal. `analyze` and `evidence` already union at the
consumer; this makes the remaining two agree with them.

Three comment/doc corrections keep the next reader from re-deriving the withdrawn parser fix, which
has now been proposed twice.

## Technical Context

**Language/stack**: F# on .NET `net10.0`. No new dependencies.

**Touched modules** (all internal — no `.fsi` exists for the two handler modules; `WorkModel.fsi`'s
`deriveGuidanceModel` signature is unchanged):

| File | Change | Kind |
|---|---|---|
| `src/FS.GG.SDD.Artifacts/WorkModel.fs:888` | `relatedIds` = three-way union | behavior |
| `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs:144-154` | take `taskFacts`; derive `SourceIds` | behavior |
| `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs:494` | pass `taskFacts` at call site | mechanical |
| `src/FS.GG.SDD.Commands/CommandWorkflow/TaskGraphAuthoring.fs:275` | correct stale union claim | comment |
| `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs:212` | record why `:276` already unions | comment |
| `docs/reference/authoring-contracts.md` | verbatim-parse / union-at-consumer model | docs |

**Explicitly not touched**: `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Task.fs` (FR-004).

**Schema posture**: no persisted schema version changes. `guidance.json` and `verify.json` both stay
at their current `schemaVersion`. Both affected fields already exist and are already emitted; they
move from under-populated to populated.

**No migration note** (FR-009). `versioning-policy.md:44` classes a change Breaking only on
remove/rename/retype of a field, an output-shape change, a command/flag removal, or an exit-code
change. Filling an existing `string[]` is none of these, and `migrations/README.md` *forbids* a note
on an additive-only release. This was checked, not assumed — the spec's first draft asserted the
opposite obligation and was corrected. Consequence: `docs/release/` is untouched, which also keeps
this item disjoint from FS-GG/FS.GG.SDD#190's in-flight touch-set.

**Public API surface**: unchanged. `deriveGuidanceModel: model: WorkModel -> NormalizedGuidanceModel`
keeps its signature (`WorkModel.fsi:123`). The internal `PublicSurface.baseline` reflection test
should not move; if it does, the change was larger than intended and the plan is wrong.

## Constitution Check

- **I. Spec → FSI → semantic tests → implementation.** Spec authored first. No `.fsi` sketch needed —
  no public signature changes; the surface is already stable. Semantic tests (T003, T005) are written
  and observed **failing** before the implementation tasks (T004, T006) run.
- **II. Structured artifacts are the machine contract.** `guidance.json` and `verify.json` are the
  contracts; the prose in `authoring-contracts.md` is the authoring surface. Where they disagree
  today, the structured artifact is the defect (the doc never claimed `affectedSourceIds` was
  meaningful) — so the artifact moves, not the doc.
- **III. Visibility lives in `.fsi`.** No public module gains or loses surface.
- **IV. Idiomatic simplicity.** Three list unions with `List.distinct |> List.sort`. No new
  abstraction, no shared helper extracted across the two sites — they operate on different types
  (`WorkTask` vs `EvidenceDispositionDraft` + `TaskFacts`) and a premature shared function would
  couple two independently-evolving views. Duplication of a four-line union is the cheaper cost.
- **V. Elmish/MVU boundary.** Untouched. Both changes are pure derivations inside existing pure
  functions. No new effect, no I/O.
- **VI. Test evidence is mandatory.** Every behavior change gets a test that fails before and passes
  after (T003, T005). Golden fixtures updated as evidence, not as expectation-fitting: the golden
  currently records `affectedSourceIds: []` everywhere and must be regenerated *after* the
  behavior test proves the new value correct — never the reverse.
- **VII. One contract for agents and humans.** `relatedIds` feeds generated agent guidance; the
  authored `tasks.yml` remains the sole source of truth. No second authority.
- **VIII. Observability and safe failure.** No new diagnostic. A `draft.TaskIds` entry naming an
  absent task degrades to skipping (`List.choose`), never throws — pinned by T003's edge case.

No principle requires a Complexity Tracking entry.

## Project Structure

### Documentation (this feature)

```
specs/096-source-id-union-at-consumer/
├── spec.md
├── plan.md          <- this file
└── tasks.md
```

No `research.md` (no unknowns — every claim was grounded against the running CLI before authoring).
No `data-model.md` (no new entity; two existing fields change population).
No `quickstart.md` (no new user-facing command).

### Source Code (repository root)

```
src/FS.GG.SDD.Artifacts/
└── WorkModel.fs                            # FR-001
src/FS.GG.SDD.Commands/CommandWorkflow/
├── HandlersVerify.fs                       # FR-002, FR-003
├── HandlersEvidence.fs                     # FR-005 (comment only)
└── TaskGraphAuthoring.fs                   # FR-006 (comment only)
docs/reference/
└── authoring-contracts.md                  # FR-007
tests/FS.GG.SDD.Artifacts.Tests/
└── AgentGuidanceViewTests.fs               # AC-001..003 (it already parses a work-model JSON)
tests/FS.GG.SDD.Commands.Tests/
├── VerifyCommandTests.fs                   # AC-004..007, FR-008
└── goldens/readiness/{verify,ship,ship-verdict}.json, summary.md   # regenerated evidence
```

Two files the first draft named are **not** touched. `WorkModelTests.fs` tests the *parsed* model,
not the guidance derivation; `AgentGuidanceViewTests.fs` already owns `deriveGuidanceModel` and
already parses a work-model JSON with `sourceIds` on every task, so the tests belong there.
`VerificationViewTests.fs` only *parses* `verify.json` — the producer (`HandlersVerify`) is internal
to `FS.GG.SDD.Commands`, so its behavior is testable only through `VerifyCommandTests`.

`ship.json`, `ship-verdict.json`, and `summary.md` goldens move too: each records a sha256 of the
upstream `verify.json` / `guidance.json`. Their diffs are digest-only — no structural change. The
`guidance.json` digest moves identically for the `claude` and `codex` targets, so Principle VII's
agent equivalence is preserved by construction.

## Design Detail

### Why the union is duplicated rather than extracted

The two sites union over different shapes. `deriveGuidanceModel` has a `WorkTask` in hand and unions
three of its own fields. `verifyEvidenceDispositionViews` has an `EvidenceDispositionDraft` (which
carries only `TaskIds`) and must first resolve those ids against `TaskFacts`, then union each
resolved task's three fields. A shared `taskLineage : WorkTask -> string list` helper is *possible*
and is the one extraction worth making — both sites end with the same three-field union on a
`WorkTask`. It goes in `WorkModel.fs` beside `deriveGuidanceModel`… except `HandlersVerify` would
then depend on `WorkModel` for a four-line function, coupling a command handler to the normalized-model
module for no reuse beyond this pair.

**Decision**: duplicate the four-line union at both sites, with a comment at each naming the other.
Revisit only if a third consumer appears. Principle IV: "the standard library over clever
abstractions"; two `List.distinct |> List.sort` calls are not an abstraction debt.

### Why `verify`'s field is derived from linked tasks, not from the obligation

`EvidenceDispositionDraft` carries `TaskIds` and nothing else id-shaped. The obligation *is* a task
obligation — `evidenceObligations taskFacts` builds one per task per `requiredEvidence` entry. So the
lineage of an obligation is exactly the lineage of the tasks that link it. Resolving through
`draft.TaskIds` also handles the multi-task case (AC-005) for free, and mirrors
`verifyTestDispositionViews` (`:164`), which already receives `taskFacts` and derives `RequirementIds`
the same way. `taskFacts` is in scope at the `:494` call site.

### Why the golden is regenerated last

`tests/FS.GG.SDD.Commands.Tests/goldens/readiness/verify.json` currently asserts
`"affectedSourceIds": []` at every occurrence — the golden *encodes the defect*. Regenerating it
first would make the behavior tests pass vacuously. Order is therefore: write the semantic test
(T005) → watch it fail → implement (T006) → watch it pass → regenerate the golden (T007) → inspect
the golden diff by eye and confirm every changed array is a lineage the fixture's tasks actually
declare. The golden is evidence, not the specification.

### Why FR-001 is verified at the model seam, not through `agents`

`verify` and `ship` never write `readiness/<id>/work-model.json` (FS-GG/FS.GG.SDD#191, found while
grounding this item), so `agents` cannot be driven to emit `guidance.json` end-to-end on a fresh
workspace. `deriveGuidanceModel` is a pure function over a `WorkModel`; its contract lives at its own
seam and `AgentGuidanceViewTests` exercises it directly with a parsed work model. This is a real test through
the public surface (Principle VI), not a workaround — but the limitation is recorded here so that when
#191 lands, an end-to-end `agents` assertion is added rather than assumed to exist.

## Verification Plan

| Requirement | Evidence | Fails before? |
|---|---|---|
| FR-001 | `AgentGuidanceViewTests`: task with `sourceIds: [SB-002]` ⇒ `relatedIds` contains `SB-002` | yes — currently absent |
| FR-001 | `AgentGuidanceViewTests`: typed-only task ⇒ `relatedIds` unchanged | no — regression guard |
| FR-001 | `AgentGuidanceViewTests`: id in both `decisions:` and `sourceIds:` ⇒ appears once, sorted | yes — dedup/sort pinned |
| FR-002 | `VerifyCommandTests`: at least one obligation has non-empty `affectedSourceIds` (SC-002) | yes — currently `[]` |
| FR-002 | `VerifyCommandTests`: every disposition's `affectedSourceIds` == its linked tasks' lineage, cross-checked against the parsed `tasks.yml` | yes |
| FR-002 | edge: obligation with no linked tasks ⇒ `[]`, no throw | no — safety guard |
| FR-003 | full `VerifyCommandTests` suite green; `outcome`/exit/diagnostics unchanged | no — regression guard |
| FR-004 | `Task.fs` unchanged (`git diff --exit-code`); `TasksArtifactTests` untouched and green | no — regression guard |
| FR-005/006 | comments present; `grep -rn` finds no present-tense parser-union claim in `src/` (SC-006) | yes |
| FR-007 | `docs/reference/authoring-contracts.md` documents the model | yes |
| FR-008 | run `verify` twice ⇒ byte-identical `verify.json`; same for guidance derivation | no — determinism guard |
| FR-009 | `docs/release/migrations/` gains no file; `release-readiness.json` `migrations[]` stays empty | no — policy guard |
| FR-010 | `authoring-contracts.md` names the consumers that union the three fields | yes |

**Baseline** (measured on `bd64f02`, not inherited): 1236 passing, 4 skipped (network-gated acceptance). Any deviation is investigated before
the change is blamed or exonerated. This sandbox requires
`dotnet restore -p:RestoreForceEvaluate=true` then discarding the lock churn; CI does not.

**Determinism**: both outputs are `List.distinct |> List.sort`, so ordering is total and
input-independent. FR-008 is checked by a second invocation, not asserted by construction.

## Agent-facing behavior

`relatedIds` widens, so generated `commands.md` / `skills.md` projections for a task with
`sourceIds`-only references gain those ids. Claude and Codex targets are generated from the same
`NormalizedGuidanceModel` and move together — Principle VII's equivalence is preserved by
construction, since the change is upstream of target selection.

No agent command, skill, or prompt changes.

## Governance integration

`verify.json` is the SDD→Governance handoff artifact. `schema-reference.md:183` names
`evidenceDispositions` but not its sub-fields; `affectedSourceIds` appears nowhere under
`docs/release/`, is absent from the release baseline's report inventory, and is read by no
Governance-handoff fixture — so no compatibility obligation is broken. The change is
additive-in-effect: a consumer that tolerated a constant `[]` still parses a populated array of the
same type. `schemaVersion` stays `1`.

Per `versioning-policy.md:42-46` this is not a Breaking change, so no migration note ships (FR-009).
A consumer that *depended on the array being empty* would break — but that is depending on a defect,
and there is no such consumer in-tree or in the handoff fixtures. Recorded here, in the code
comments, and in `authoring-contracts.md` rather than in a note the policy forbids.

## Complexity Tracking

None. No constitutional principle is bent; no complex F# feature is introduced.
