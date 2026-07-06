# Phase 1 Data Model: Lifecycle-Status Footer

New types live in `src/FS.GG.SDD.Commands/CommandTypes.fs` (+ `.fsi`). One new additive field is added to `CommandReport`. No persisted-artifact schema changes.

## `StageState` (discriminated union)

The sensed state of a single lifecycle stage.

| Case | Meaning | JSON token |
|---|---|---|
| `Done` | The stage's artifact is present on disk. | `"done"` |
| `Current` | The stage of the command just run (lifecycle-stage commands only). | `"current"` |
| `Next` | The successor stage to run next. | `"next"` |
| `Pending` | Not yet produced and not current/next. | `"pending"` |
| `Blocked` | The current stage, when the run outcome is blocked/failed. | `"blocked"` |

Rules: exactly one `Current` for a lifecycle-stage command; zero `Current` for cross-cutting commands. `Blocked` replaces `Current` on a blocked outcome. Each stage's `Done` is decided independently by artifact presence (non-contiguous progress is represented faithfully).

## `StageEntry` (record)

One stage's position and state.

| Field | Type | Notes |
|---|---|---|
| `Command` | `SddCommand` | One of the 10 lifecycle-stage cases (`Charter … Ship`). Rendered via `commandName`. |
| `Ordinal` | `int` | 1-based position in canonical order (1 = charter … 10 = ship). |
| `State` | `StageState` | Sensed state (above). |

## `LifecycleStatus` (record)

The new structured fact carried on every command report.

| Field | Type | Notes / JSON |
|---|---|---|
| `WorkId` | `string option` | Resolved work id in scope; `None` → JSON `null` (no work item, e.g. `init`/cross-cutting/unresolved). |
| `Stages` | `StageEntry list` | The 10 stage entries in canonical order (`charter → ship`). Always length 10. |
| `CurrentOrdinal` | `int option` | "N" in "N of M". For a lifecycle-stage command: the `Current` stage ordinal. For a cross-cutting command: the count of `Done` stages. `None` when no meaningful position (e.g. no work id). |
| `TotalStages` | `int` | "M" = 10 (constant; carried for consumer convenience/forward-safety). |
| `Outcome` | `CommandOutcome` | Echo of `report.Outcome` (the command's outcome) — convenience for the footer; same value already on the report. |
| `NextCommand` | `SddCommand option` | Next lifecycle command (`nextLifecycleCommand` for a lifecycle stage; the lowest pending stage's command for cross-cutting; `None` at terminal/all-done). |
| `IsLifecycleStage` | `bool` | `false` for the cross-cutting verbs (`init`, `agents`, `refresh`, `scaffold`, `doctor`, `upgrade`, `lint`, `validate`); `true` for the 10 stages. Drives the "not a lifecycle stage" flag. |

**Not on this entity (by design)**: the failure explanation and options. On a blocked/failed outcome the footer computes those at render time from the report's existing `Diagnostics` (message + correction) and `NextAction` (command + required artifacts), per FR-017 — no duplication, no second source of truth.

### JSON shape (illustrative)

```json
"lifecycleStatus": {
  "workId": "084-lifecycle-status-footer",
  "isLifecycleStage": true,
  "currentOrdinal": 3,
  "totalStages": 10,
  "outcome": "succeeded",
  "nextCommand": "checklist",
  "stages": [
    { "command": "charter",   "ordinal": 1,  "state": "done" },
    { "command": "specify",   "ordinal": 2,  "state": "done" },
    { "command": "clarify",   "ordinal": 3,  "state": "current" },
    { "command": "checklist", "ordinal": 4,  "state": "next" },
    { "command": "plan",      "ordinal": 5,  "state": "pending" },
    { "command": "tasks",     "ordinal": 6,  "state": "pending" },
    { "command": "analyze",   "ordinal": 7,  "state": "pending" },
    { "command": "evidence",  "ordinal": 8,  "state": "pending" },
    { "command": "verify",    "ordinal": 9,  "state": "pending" },
    { "command": "ship",      "ordinal": 10, "state": "pending" }
  ]
}
```

## `CommandReport` change (additive)

Add exactly one field to the record in `CommandTypes.fs`:

```fsharp
      // … existing fields …
      NextAction: NextAction option
      Help: HelpSummary option
      LifecycleStatus: LifecycleStatus            // NEW — always present
```

- Additive-only: no existing field removed, renamed, or repurposed.
- `SchemaVersion` stays `1` (Stable). `ReportVersion` moves `"1.0.0"` → `"1.1.0"`.
- Populated in one place: `ReportAssembly.buildReport` (folding `model.InterpretedEffects` + `model.Request.Command`/`WorkId` + `report.Outcome`/`NextAction`).

## Derivation inputs (all already in the model)

| Input | Source |
|---|---|
| Which stages are done | `model.InterpretedEffects` — `ReadFile <stagePath>` results with `Snapshot = Some` |
| Current stage / is-lifecycle-stage | `model.Request.Command` |
| Work id | `model.Request.WorkId` |
| Outcome | computed `report.Outcome` |
| Next command | `nextLifecycleCommand` (existing) |
| Blocking diagnostic (for the render-time failure projection) | `report.Diagnostics` + `report.NextAction.BlockingDiagnosticIds` |

## Validation / invariants

- `Stages` is always exactly the 10 canonical stages, ordinals 1..10, in order.
- At most one `Current`; `Current` present ⇔ `IsLifecycleStage` and outcome not blocked.
- `Blocked` present ⇒ outcome is blocked/failed and marks the current stage.
- `CurrentOrdinal`, when `Some`, is in `1..10`.
- Deterministic: identical on-disk state + identical command ⇒ identical `LifecycleStatus`.
