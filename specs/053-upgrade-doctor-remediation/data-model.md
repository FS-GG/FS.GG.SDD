# Phase 1 Data Model: Diff-Driven Remediation Verbs (`doctor` / `upgrade`)

Feature: `053-upgrade-doctor-remediation` · Date: 2026-07-01

All changes are **additive** to existing types. No entity is removed or reshaped; the persisted
schema (`scaffold-provenance.json` v1, provider registry) is **read-only** for this feature and
unchanged. New machine contract = two additive `CommandReport` blocks + one additive effect.

---

## E1 — `SddCommand` (extended)

`src/FS.GG.SDD.Commands/CommandTypes.fs(.fsi)`

| Case | Change | Notes |
|------|--------|-------|
| `Doctor` | **NEW** | `parseCommand "doctor"`; `commandName = "doctor"`; `commandStage = "doctor"`; `nextLifecycleCommand Doctor = None`. |
| `Upgrade` | **NEW** | `parseCommand "upgrade"`; `commandName = "upgrade"`; `commandStage = "upgrade"`; `nextLifecycleCommand Upgrade = None`. |

Both are cross-cutting (not lifecycle stages), reachable only via the CLI (FR-001/FR-006).

## E2 — `CommandRequest` (extended, additive inputs)

`src/FS.GG.SDD.Commands/CommandTypes.fs(.fsi)`

| Field | Type | Change | Notes |
|-------|------|--------|-------|
| `AssumeYes` | `bool` | **NEW** | `--yes`; when true, `upgrade` confirms all steps without prompting (FR-011). Default `false`. Ignored by non-`upgrade` commands. |
| `IsInteractive` | `bool` | **NEW** | Computed at the edge from `Console.IsInputRedirected` (R7). Lets the pure core refuse a non-interactive `upgrade` without `--yes` up front (FR-012), rather than emitting a blocking `Confirm`. Default follows the edge; other commands ignore it. |

Existing scaffold inputs (`Provider`, `Parameters`, `Force`, `TemplateUpdate`) are unchanged and
ignored by `doctor`/`upgrade`.

## E3 — `CommandEffect` (extended) + `CommandEffectResult` (extended)

`src/FS.GG.SDD.Commands/CommandTypes.fs(.fsi)`

| Addition | Type | Notes |
|----------|------|-------|
| `Confirm` | `Confirm of stepId: string * prompt: string` | **NEW** effect. Requests per-step confirmation for one reconciliation step (R7). Interpreted at the edge by a stdin read when `IsInteractive`; the pure `update` re-derives the next step from the confirmed results in the log. |
| `CommandEffectResult.Confirmed` | `bool option` | **NEW** additive field on the result record. `Some true` = confirmed/applied, `Some false` = declined/skipped, `None` = not a `Confirm` result (all existing results). Threaded back like `Process`/`Snapshot`. |

Edge rules for `Confirm` (see `contracts/confirm-effect.md`): `DryRun` → `Confirmed = Some false`
(never mutate); `AssumeYes` short-circuits at the pure layer (no `Confirm` emitted — steps applied
directly); non-interactive without `AssumeYes` → the pure core never reaches the confirm loop (it
emits the refusal path in E5/FR-012).

## E4 — `DoctorSummary` (new report block)

`src/FS.GG.SDD.Commands/CommandTypes.fs(.fsi)` — additive optional field on `CommandReport` and
`CommandModel` (like `ScaffoldSummary`). Read-only picture `doctor` emits (spec Key Entity "Drift
report"). Projected as a json block, a text projection, and derived rich.

| Field | Type | Notes |
|-------|------|-------|
| `HasProvenance` | `bool` | `false` → "no scaffold provenance — nothing to reconcile" (FR-015). |
| `ProviderName` | `string option` | From provenance; `None` when no provenance. |
| `InstalledCliVersion` | `string` | `request.GeneratorVersion.Version`. |
| `RequiredMinimumCliVersion` | `string option` | Live descriptor / provenance-recorded minimum (R2); `None` when the provider declares none (FR-016 coherent-by-absence). |
| `CliAxis` | `string` | `"behind"` / `"atOrAbove"` / `"coherentByAbsence"` / `"undeterminable"` (installed unparseable, R12). |
| `CliBehindBy` | `string option` | Behind-by delta (e.g. `"1.0.0 → 1.2.0"`); `None` unless `behind`. |
| `ExpectedArtifactCount` | `int` | Size of the seeded skeleton set (R3). |
| `MissingArtifactPaths` | `string list` | Sorted; the named missing seeded artifacts (FR-004). |
| `PreviewSteps` | `ReconciliationStep list` | Dry-run preview of what `upgrade` would change (FR-005). |
| `IsCoherent` | `bool` | `true` when CLI at/above-or-absent **and** no missing artifacts **and** no re-pin target → "coherent — nothing to reconcile" (US1-AC3). |

`doctor` always yields `Outcome = NoChange` or `SucceededWithWarnings` (drift is a warning, not a
block) → **exit 0** (FR-002).

## E5 — `UpgradeSummary` (new report block)

`src/FS.GG.SDD.Commands/CommandTypes.fs(.fsi)` — additive optional field on `CommandReport` and
`CommandModel`. The outcome of the reconciliation (spec Key Entity "Reconciliation step").

| Field | Type | Notes |
|-------|------|-------|
| `HasProvenance` | `bool` | `false` → no-op "nothing to reconcile", exit 0 (FR-015). |
| `Mode` | `string` | `"interactive"` / `"assumeYes"` / `"refusedNonInteractive"` (records the path used, US3-AC1). |
| `AlreadyCoherent` | `bool` | `true` → clean no-op "already coherent", exit 0 (US2-AC3/FR-013). |
| `Steps` | `ReconciliationStep list` | Each step with its `Outcome` (applied/skipped/failed/noTarget). |
| `AppliedStepIds` | `string list` | Steps actually applied (US2-AC4 distinguishes applied from skipped). |
| `SkippedStepIds` | `string list` | Declined steps (no write occurred). |
| `FailedStepIds` | `string list` | Confirmed steps that failed to apply (→ exit 2, SC-006). |
| `ResidualDrift` | `bool` | `true` when any axis remains out of coherence after the run (FR-013). |
| `NextActionHint` | `string` | E.g. "re-run `doctor` to confirm" or the refusal pointer to `--yes` (FR-012). |

Outcome mapping: all-applied + no residual drift → `Succeeded`; some skipped w/ residual drift →
`SucceededWithWarnings`; a **failed** step → `Blocked` with an `upgrade.*` defect id → **exit 2**;
non-interactive refusal → `Blocked` with the refusal diagnostic → **exit 1** (R10).

## E6 — `ReconciliationStep` (new value type)

Shared by `DoctorSummary.PreviewSteps` and `UpgradeSummary.Steps`.

| Field | Type | Notes |
|-------|------|-------|
| `StepId` | `string` | `"cliSelfUpdate"` / `"templateRePin"` / `"artifactReSeed"`. |
| `Kind` | `string` | Same domain as `StepId` (stable projection key). |
| `DiffPreview` | `string` | Compact before/after preview per step-kind (R5): version delta / created-path list / changed-line pin preview. |
| `Outcome` | `string` | Preview context: `"wouldApply"` / `"noTarget"`. Apply context: `"applied"` / `"skipped"` / `"failed"` / `"noTarget"`. |
| `TargetPaths` | `string list` | The consumer paths the step would write (re-seed: missing artifacts; re-pin: `.fsgg/providers.yml`; self-update: `[]`). |

## E7 — `Drift` (new pure module)

`src/FS.GG.SDD.Commands/CommandWorkflow/Drift.fs` — the pure computation shared by both handlers
(no I/O; consumes snapshots already read via effects). Produces the CLI axis (reusing 052's
`Fsgg.Version` + minimum reading, R2), the artifact axis (expected = `SeededSkills.skillNames` ×
`.claude`/`.codex` + `.fsgg/early-stage-guidance.md`, R3), and the previewed `ReconciliationStep`
list (R5/R6). See `contracts/drift-model.md`.

## E8 — Diagnostics (extended)

`src/FS.GG.SDD.Artifacts/Diagnostics.fs(.fsi)` — additive `doctor.*`/`upgrade.*` codes:

| Id | Severity | Exit class | Notes |
|----|----------|-----------|-------|
| `doctor.driftDetected` | Info/Warning | 0 | Non-blocking drift advisory (doctor never blocks). |
| `upgrade.nonInteractiveNoYes` | Blocking | 1 | Non-interactive without `--yes`; refuse, pointer to `--yes` (FR-012). |
| `upgrade.selfUpdateFailed` | Blocking | **2** | Confirmed self-update process errored (added to `providerDefectIds`, R10). |
| `upgrade.stepFailed` | Blocking | **2** | A confirmed re-pin/re-seed write failed (added to `providerDefectIds`). |
| `upgrade.residualDrift` | Warning | 0 | Partial apply with drift remaining (declined step, US2-AC4). |

The exit-2 additions extend the existing `providerDefectIds` set consumed by `exitCodeForReport`.

---

## Determinism & backward-compatibility notes

- **No persisted-schema change.** `scaffold-provenance.json` stays v1 and is read-only here; the two
  new report blocks are additive to the in-memory/emitted `CommandReport` json only.
- **json is byte-stable across projections** (FR-014/SC-007): both blocks emit hand-ordered fields;
  `MissingArtifactPaths`/`TargetPaths` sorted; no clock/absolute-path/ANSI.
- **Additive `CommandEffectResult.Confirmed`** defaults `None` for every existing result, so all
  current handlers and goldens are unaffected.
