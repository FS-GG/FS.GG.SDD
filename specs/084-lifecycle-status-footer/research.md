# Phase 0 Research: Lifecycle-Status Footer

All Technical-Context unknowns are resolved below. Each item: Decision / Rationale / Alternatives.

## 1. Versioning posture for the additive `lifecycleStatus` field

- **Decision**: Add `lifecycleStatus` as an **additive optional field** on the command report. Keep `schemaVersion = 1` (**Stable**). Record the field in `docs/release/schema-reference.md`'s command-report inventory and in `docs/release/release-readiness.json` `catalog[].inventory` (lockstep). Bump the semantic `reportVersion` from `"1.0.0"` to `"1.1.0"`.
- **Rationale**: The command-report is classed **AdditiveOptional** in `schema-reference.md`; `schemaVersion` is explicitly **Stable**, and the two prior additive-field features — 053 (`doctor`/`upgrade`) and 076 (`lint`) — added fields **without** bumping `schemaVersion`. Consumers "MUST tolerate unknown fields." Bumping `schemaVersion 1→2` for a purely additive field would break that established contract semantics (a `schemaVersion` change signals a consumer-visible envelope change). The spec's FR-006/SC-005 asked for "increment the schema version," but the intent behind that requirement — *the change is traceable and a version moves* — is honored by the `reportVersion` minor bump, which is the correct lever for an additive report change and does not violate the Stable-`schemaVersion` policy.
- **Reconciliation**: FR-006 and SC-005 are amended in the spec (and a Clarifications bullet added) to require: additive-only field + inventory record + `reportVersion` minor bump, with `schemaVersion` held Stable. **Flagged for user confirmation** in the plan completion report.
- **Alternatives considered**:
  - *Bump `schemaVersion 1→2`* — rejected: contradicts AdditiveOptional/Stable policy and 053/076 precedent; risks consumers treating an additive field as a breaking envelope change.
  - *Bump neither version, only inventory* — rejected: leaves FR-006's "a version moves" intent unmet and provides no coarse signal that the report gained a cross-command field.

## 2. Where filesystem sensing threads in (MVU seam)

- **Decision**: Sense each stage's artifact presence by emitting `ReadFile` `CommandEffect`s for the 10 stage artifact paths (for the resolved work id) during the **pure plan step**, then derive `LifecycleStatus` by a **pure fold over `model.InterpretedEffects`** inside `buildReport`: a stage is `done` when its `ReadFile` result has `Snapshot = Some`. Add a helper `Foundation.lifecycleSensingReadEffects : workId -> CommandEffect list` and append it (deduped) to every command's effect list.
- **Rationale**: `buildReport` (`ReportAssembly.fs:44`) is pure and forced-deterministic (it does not even echo the real project root). The interpreter (`CommandEffects.interpret`) already encodes existence in results (`Snapshot = Some` ⟺ file exists) and folds like `effectDiagnostics`/`changeFromEffectResult` already read `InterpretedEffects`. This keeps I/O at the edge (Principle V) and adds zero filesystem calls to pure code. Many stage paths are already read by existing effects (e.g. `preWorkModelReadEffects`), so the helper is deduped.
- **Alternatives considered**:
  - *`File.Exists` inside `buildReport`* — rejected: violates Principle V (I/O in pure assembly) and breaks determinism guarantees.
  - *A separate post-hoc sensing pass in the CLI edge* — rejected: would put the fact outside the report, breaking projection parity (the JSON contract would lack it) and Principle VII.

## 3. Per-stage artifact path map (sensing source of truth)

Central constants: `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`.

| Stage | Sensed artifact | Path builder |
|---|---|---|
| charter | `work/<id>/charter.md` | `charterPath` |
| specify | `work/<id>/spec.md` | `specPath` |
| clarify | `work/<id>/clarifications.md` | `clarificationPath` |
| checklist | `work/<id>/checklist.md` | `checklistPath` |
| plan | `work/<id>/plan.md` | `planPath` |
| tasks | `work/<id>/tasks.yml` | `tasksPath` |
| evidence | `work/<id>/evidence.yml` | `evidencePath` |
| analyze | `readiness/<id>/analysis.json` | `analysisPath` |
| verify | `readiness/<id>/verify.json` | `verifyPath` |
| ship | `readiness/<id>/ship.json` | `shipPath` |

- **Decision**: These 10 paths define "done" (presence). `stagePrimaryArtifactPath` (Foundation.fs:445) already maps the 7 authored stages; extend the sensing helper to also include the 3 `readiness/<id>/*.json` generated stages. `work-model.json` is **not** a stage; not sensed for stage state.
- **Rationale**: Matches the spec's stated locations and the independent catalog in `schema-reference.md:147-150`. Presence-only (Assumption in spec) — the footer does not re-parse or freshness-check these files.
- **Alternatives**: sensing "done" by validity/freshness — rejected (explicitly out of scope; overlaps the existing freshness concern; risks crashing on malformed artifacts, violating Principle VIII).

## 4. Stage-state derivation rules (deterministic)

- **Decision**: Per stage, apply this precedence for a **lifecycle-stage command** `S` with report outcome `O`:
  1. `O = Blocked` and stage = `S` → **Blocked**
  2. stage = `S` → **Current**
  3. artifact sensed present → **Done**
  4. stage = `nextLifecycleCommand(S)` → **Next**
  5. else → **Pending**

  For a **cross-cutting command** (`init`, `agents`, `refresh`, `scaffold`, `doctor`, `upgrade`, `lint`, `validate` — `nextLifecycleCommand = None`, or no work id):
  1. artifact sensed present → **Done**
  2. lowest-ordinal Pending stage → **Next**
  3. else → **Pending**

  and `isLifecycleStage = false`, no stage is `Current`.

  `currentOrdinal` = ordinal of the `Current` stage (lifecycle command) or the count of `Done` stages (cross-cutting); `totalStages = 10`; `nextCommand` = `nextLifecycleCommand(S)` for a lifecycle command, else the `Next` stage's command (or `None` when all done / terminal).
- **Rationale**: Marks each stage independently by its own artifact presence, so **non-contiguous** progress (a later artifact present while an earlier is absent) renders truthfully (FR-004, SC-006). Exactly one `Current` for lifecycle commands; none for cross-cutting (which are "not a lifecycle stage"). Deterministic for a given on-disk state (FR-015).
- **Alternatives**: "all stages before current = done" — rejected (would fabricate completion on non-contiguous state, failing SC-006).

## 5. Failure explanation + options (projection, not a new field)

- **Decision**: On `Blocked`/failed outcome, the footer's explanation + options are **computed at render time** from existing report fields, not stored on `lifecycleStatus`:
  - **explanation** = the blocking `Diagnostic.Message` (looked up via `NextAction.BlockingDiagnosticIds`), with `Diagnostic.Correction` (which already carries the remediation-pointer sentence from `RemediationPointers.suffixFor`) as the "how to fix."
  - **options** = the `NextAction.Command` (rendered `fsgg-sdd <command>`) plus `NextAction.RequiredArtifacts`; when no remediation pointer exists, at minimum the next-action command is shown.
- **Rationale**: Honors FR-017 (no new footer-specific field, no second source of truth) and parity (these facts already exist in every projection). `Diagnostic` (`Diagnostics.fs:14`: `Message`, `Correction`), `NextAction` (`CommandTypes.fs:450`: `Command`, `Reason`, `RequiredArtifacts`, `BlockingDiagnosticIds`), and `RemediationPointer` (`RemediationPointers.fs:22`) supply everything.
- **Alternatives**: a structured `failure { explanation, options[] }` on `lifecycleStatus` — rejected by FR-017 (duplicates/competes with `Diagnostics`/`NextAction`).

## 6. Color mapping for the rich footer (presentation only)

- **Decision**: Semantic state → Spectre style: `done` = green, `current` = bold cyan, `next` = yellow, `pending` = dim/grey, `blocked` = bold red. The blocked/failed stage and the failure explanation get the red emphasis. Colors are presentation-only, excluded from golden/deterministic contracts, and reuse the existing `outcomeStyle`/`esc` helpers in `Rendering.fs`.
- **Rationale**: FR-016 requires each state visually distinguishable and blocked emphasized; the exact palette is a presentation detail (spec Assumption). Reusing existing style helpers keeps it consistent with the current rich rendering.
- **Degradation**: FR-009 is satisfied **by construction** — `Rendering.resolve` only calls `renderRichTo` when interactive+color-enabled; otherwise it emits `renderText` (which now includes the plain footer). So the rich footer degrades to the byte-identical text footer with zero effort and zero ANSI.
- **Alternatives**: a fixed palette pinned in the spec/contract — rejected (color is non-contractual; pinning it would wrongly bind a presentation detail).

## 7. Footer placement (final element)

- **Decision**: Text — append after the `nextAction`/`help` block, before `builder.ToString()` (`CommandRendering.fs:545`). Rich — append after the Next-action callout (`Rendering.fs:195`), as the last `MarkupLine`/`Panel` calls.
- **Rationale**: FR-001 requires the footer to be the final element; both render functions receive the whole report and end at those points. "Any additional information comes before" maps to "report body first, footer last."
- **Alternatives**: a header instead of footer — rejected by FR-001 and the user's stated placement.

## 8. Report-serialization approach

- **Decision**: Serialize `lifecycleStatus` as a nested JSON object in `CommandSerialization.fs` (writer style already used for the report), inserted so field ordering stays deterministic/byte-stable. `null` is not required (the field is always present), but the object is always emitted for every command.
- **Rationale**: Consistent with existing writer-based serialization; additive; byte-stable.
- **Alternatives**: emit only when a work id exists — rejected: FR-001/FR-011 require the footer on **every** command (cross-cutting/no-work-id render a coherent all-pending status).

## Open items

None. The one decision requiring sign-off (versioning posture, §1) is surfaced in the plan and completion report; it does not block Phase 1 design.
