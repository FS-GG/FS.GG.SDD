# Phase 1 Data Model: `refresh` Currency Classification

**Feature**: 095 · **Date**: 2026-07-08

No persisted schema changes (FR-014). The "data model" here is the **in-memory currency state
machine** in `HandlersRefresh.fs` and the report fields it projects onto.

## Entities

### `ViewCurrencyClass` (existing, unchanged)

A refresh-local DU (`HandlersRefresh.fs:36-43`), deliberately distinct from `GeneratedViewCurrency`
because the case names collide (`:33`).

| Case | Projects to `GeneratedViewCurrency` | Report word | Clean? | Bucket (`classifyToBucket :740`) |
|---|---|---|---|---|
| `Refreshed` | `Current` | `current` | yes | `refreshedViewIds` |
| `AlreadyCurrent` | `Current` | `current` | yes | `alreadyCurrentViewIds` |
| `Stale` | `Stale` | `stale` | no | `blockedViewIds` |
| `Malformed` | `Malformed` | `malformed` | no | `blockedViewIds` |
| `Missing` | `Missing` | `missing` | no | `blockedViewIds` |
| `Blocked` | `Blocked` | `blocked` | no | `blockedViewIds` |
| `NotApplicable` | — | — | yes | `notApplicableViewIds` |

**Invariant this feature establishes**: the case assigned to an artifact is a true statement *about
that artifact*, never about its source. No case is added or removed.

### `FileSnapshot` (existing, unchanged)

What `snapshot path model` returns. Carries `.Text`. Passed verbatim to `parseShipView`, which accepts
`FileSnapshot` directly (`Ship.fsi:55`) — so no re-read and no reconstruction.

### The validator (new, local)

```
isValid : FileSnapshot -> bool
```

Two instances, both file-local:

| Instance | Definition | Applied to |
|---|---|---|
| `parsesAsJsonSnap` | existing `parsesAsJson` (`:350`) lifted to `FileSnapshot` | `analysis.json`, `verify.json` |
| `parsesAsShipView` | `ShipModule.parseShipView >> Result.isOk` | `ship.json` |

`parsesAsShipView` is strictly stronger: a non-JSON body fails inside `parseJsonView` before any field
is read, so it subsumes `parsesAsJsonSnap` (research R1). This is what makes US1-AS4 hold without a
second check.

## State transitions

### `downstreamClass isValid path` (`:438-449`) — **changed**

```
  wmClass = Blocked                     ─→ Blocked
  snapshot path = None                  ─→ Missing
  snapshot path = Some snap
    │
    ├── not (isValid snap)              ─→ Malformed      ◀── FR-001: `isValid` was hardcoded
    │                                                          `parsesAsJson`; now a parameter
    └── isValid snap
          ├── wmChanged                 ─→ Stale
          └── otherwise                 ─→ AlreadyCurrent
```

Call sites (`:451-453`):

| View | Validator | FR |
|---|---|---|
| `analysis.json` | `parsesAsJsonSnap` | FR-002 (unchanged behavior) |
| `verify.json` | `parsesAsJsonSnap` | FR-002 (unchanged behavior) |
| `ship.json` | `parsesAsShipView` | FR-001 |

### `verdictClass` (`:514-539`) — **behavior unchanged; one arm becomes unreachable**

```
  match shClass, verdictOnDisk with
  │
  ├── AlreadyCurrent, _         ─→ project the verdict from ship.json
  │     ├── projection succeeds, bytes equal    ─→ AlreadyCurrent
  │     ├── projection succeeds, bytes differ   ─→ Refreshed  (+ WriteFile effect)
  │     ├── projection fails (`jsonOpt = None`) ─→ Malformed  ◀── :527 NOW UNREACHABLE (FR-004)
  │     └── textOf = None                       ─→ Missing    ◀── :528 dead branch (FR-013)
  │
  ├── Stale, Some _             ─→ Stale        (FR-012, unchanged)
  ├── _,     Some _             ─→ Blocked      ◀── the state a bad ship.json now lands in (FR-004)
  └── _,     None               ─→ Missing      (FR-010)
```

**Why `:527` becomes unreachable.** The `(AlreadyCurrent, _)` arm is entered only when
`shClass = AlreadyCurrent`, which after FR-001 implies `parsesAsShipView snap = true`, i.e.
`parseShipView` returned `Ok`. `shipVerdictEmission` projects from the same text, so `jsonOpt = None`
can no longer occur for a source that reached this arm. The arm stays for totality (same reasoning as
FR-013) — **not deleted**, because F# cannot prove the implication.

**Why `:528` is dead (FR-013).** `shClass = AlreadyCurrent` is reachable only through
`downstreamClass`'s `Some snap` branch (`:445`), so `snapshot (shipPath workId) model` returned `Some`.
`textOf` (`:347`) is `snapshot path model |> Option.map (fun s -> s.Text)`, over the same `model`, so it
returns `Some`. After FR-001 the arm is *doubly* unreachable. Retained for match totality; the comment
records both facts.

### `verdictDiags` (`:607-618`) — **changed**

```
  match verdictClass with
  │
  ├── Malformed  ─→ refreshMalformedGeneratedView   (warning)   ◀── now unreachable, retained
  ├── Blocked    ─→ refreshBlockedUpstreamView      (ERROR)
  ├── Stale      ─→ refreshStaleView                (warning)   (FR-012)
  └── Missing    ─→ ▼ split on the SOURCE's class               ◀── FR-009 / FR-011
                     ├── shClass = Stale ─→ refreshStaleView          (warning)   FR-009
                     └── otherwise       ─→ refreshBlockedUpstreamView (ERROR)    FR-011
```

The `Missing` arm is the only edit. It is the sole place where the verdict's diagnostic must consult
the *source's* class — because "the verdict is absent" is not, by itself, enough to choose a severity:
absent-because-the-source-moved (re-run `ship`, a warning) differs from
absent-and-the-source-is-unreadable (genuinely blocked, an error).

## Report projections affected

| Field | Path | Change |
|---|---|---|
| `generatedViews[].currency` for `ship` | `:753` | `current` → `malformed` in the valid-JSON/invalid-view state (FR-003) |
| `generatedViews[].currency` for `ship-verdict` | `:754` | `malformed` → `blocked` in that state (FR-004) |
| `refresh.alreadyCurrentViewIds` | `:780` | loses `"ship"` in that state (FR-003a, research R3a) |
| `refresh.blockedViewIds` | `:780` | gains `"ship"` in that state (FR-003a) |
| diagnostics | `:587-618` | `malformedGeneratedView` moves from the verdict's path to `ship.json`'s; the `(Stale, None)` state's diagnostic changes id and severity |
| **exit code** | `:631` via `structuredClasses` | **invariant** (FR-007, SC-004) — see research R3 |
| `ship-verdict.json` on disk | — | **never written** in any state this feature touches (FR-006) |

## Validation rules

- **Every currency word is true of its artifact.** (SC-001, the whole feature.)
- **`ship-verdict: malformed` ⟹ `ship-verdict.json` is itself unparseable.** After this change no
  input satisfies the antecedent, because the verdict is only ever *projected*, never *parsed*, by
  `refresh`. (SC-002.)
- **`ship: current` ⟹ `parseShipView (snapshot shipPath) = Ok`.** (SC-003.)
- **The validator never runs when `wmClass = Blocked`** — `downstreamClass` short-circuits at `:439`
  before touching the snapshot. (Spec §Edge Cases.)
- **Schema validity and currency are orthogonal.** `Stale` is decided by `wmChanged` *after* `isValid`
  passes, so a valid-but-stale ship view is `Stale`, never `Malformed`.
