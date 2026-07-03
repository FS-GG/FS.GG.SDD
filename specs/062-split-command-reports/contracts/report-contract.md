# Contract: report/exit-code invariants and the sanctioned typed surface

This is an internal refactor. The contract is mostly **what must NOT change**, plus
the one **minimal typed surface** deliberately added.

## A. Held invariant (must not change) — verified by existing suites

| # | Invariant | Verified by |
|---|-----------|-------------|
| A1 | Default/`--json` output bytes for every command × representative state | JSON golden/determinism suites (no baseline edits) — FR-007 |
| A2 | `--text` output bytes for every command | text projection goldens — FR-008 |
| A3 | `--rich` facts (adds/drops nothing) | rich projection tests — FR-008 |
| A4 | Exit code per outcome/diagnostics, and stdout/stderr routing | CLI exit-code / stream tests — FR-009 |
| A5 | `CommandReports` public module surface | `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` (unchanged) — FR-010/FR-011 |
| A6 | Persisted artifact schemas (`work-model.json`, provenance, readiness/*) | serialization goldens; schema versions unchanged — FR-012 |
| A7 | Governance-handoff compatibility + release-readiness catalog | `fsgg-sdd validate` handoff/baseline checks — SC-006 |

## B. Exit-code contract (behaviour preserved, mechanism retyped)

```
Blocked      ∧  (∃ d ∈ Diagnostics. d.IsToolDefect)   → 2
Blocked      ∧  ¬(∃ d. d.IsToolDefect)                → 1
Succeeded | SucceededWithWarnings | NoChange          → 0
```

- Mechanism today: `d.Id ∈ providerDefectIds` (a hand-maintained 7-id string set).
- Mechanism after: `d.IsToolDefect` (typed bit set at construction).
- **Conformance**: for every diagnostic the tool can emit, the after-mechanism MUST
  yield the same escalation as the before-mechanism. The seven ids that escalate
  today (`toolDefect`, `scaffold.providerFailed`, `scaffold.providerUnavailable`,
  `scaffold.providerWroteSddTree`, `scaffold.mirrorFailed`,
  `upgrade.selfUpdateFailed`, `upgrade.stepFailed`) and no others.

## C. Staleness classification (behaviour preserved, mechanism retyped)

- Today (in `HandlersAgents`): `diagnostic.Id.IndexOf("stale", OrdinalIgnoreCase) >= 0`.
- After: `Diagnostics.signalsStaleView diagnostic` — same boolean for every id,
  defined once. No `"stale"` literal remains in the `HandlersAgents` decision path
  (SC-004).

## D. Sanctioned new public surface (FR-011) — the ONLY additions

`FS.GG.SDD.Artifacts.Diagnostics`:

```fsharp
type Diagnostic = { … ; IsToolDefect: bool }     // record gains one field
val markToolDefect: Diagnostic -> Diagnostic
val signalsStaleView: Diagnostic -> bool
```

- Adds two lines to `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  (`markToolDefect`, `signalsStaleView`). The baseline tracks let-bound functions,
  not record fields, so the new field itself does not perturb it. This is the sole
  reviewed surface delta.
- `create`'s signature is unchanged.
- No new public surface in `FS.GG.SDD.Commands` (facade keeps `CommandReports`
  identical; `StagePlan` is internal).

## E. Negative contract (must be true after)

- `providerDefectIds` no longer exists anywhere in the source.
- No `Diagnostic.Id`-string membership test or `"stale"` substring test remains in
  the exit-code or refresh-staleness decision paths.
- The JSON writer (`Serialization.fs`) is not modified; `IsToolDefect` is never
  serialized.
