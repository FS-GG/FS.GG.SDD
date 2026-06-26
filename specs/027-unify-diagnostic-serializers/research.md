# Phase 0 Research: Collapse Diagnostic Builder + Unify JSON Serializers

All decisions below are grounded in the current `main` source, not assumed.

---

## D1 — Where do the shared writer primitives live, and how do they cross the assembly boundary without changing the guarded surface?

**Decision**: Introduce **one new low-level writer module in the Artifacts
assembly under a dedicated sub-namespace** — `namespace FS.GG.SDD.Artifacts.Json`,
`module JsonWriters` — with its own `.fsi`. Both `Serialization`
(`FS.GG.SDD.Artifacts`) and `CommandSerialization` (`FS.GG.SDD.Commands`) consume
it. The three named entry-point `.fsi` files (`Serialization.fsi`,
`CommandSerialization.fsi`, `CommandReports.fsi`) are **not touched**.

**Rationale**:
- Layering: `Commands` already references `Artifacts`
  (`FS.GG.SDD.Commands.fsproj:38`); the one-way direction is preserved by placing
  shared code in the lower (`Artifacts`) layer. Nothing new flows
  `Artifacts → Commands`.
- The current writers are *private-by-`.fsi`-omission*: `Serialization.fsi` lists
  only 6 entry points and `CommandSerialization.fsi` only `serializeReport`, so
  `writeDiagnostic` et al. are not assembly-public today. To share them
  cross-assembly they must become public in *some* `.fsi`. FR-008 permits exactly
  this: "a new dedicated low-level shared writer module … is internal-style
  plumbing, not a new public serializer contract."
- **Surface-baseline byte-stability (SC-005) is the deciding constraint.**
  `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs` reflects over types
  whose `Namespace = "FS.GG.SDD.Artifacts"` **exactly** (line 16). A module
  declared under `FS.GG.SDD.Artifacts.Json` has namespace
  `"FS.GG.SDD.Artifacts.Json"` ≠ `"FS.GG.SDD.Artifacts"`, so it is **not** picked
  up by the reflection filter and the checked-in `PublicSurface.baseline`
  (139 lines, Serialization entries at 121–125) stays byte-identical. The same
  holds for the Commands baseline (the new module is not in `FS.GG.SDD.Commands`
  at all). Result: FR-007 / SC-005 satisfied to the letter, FR-008 satisfied,
  constitution III satisfied (visibility declared in a `.fsi`, not via `internal`
  modifiers).

**Alternatives considered**:
- *Put the module in the `FS.GG.SDD.Artifacts` namespace.* Rejected: its public
  functions would be captured by the reflection baseline and force new lines into
  `PublicSurface.baseline`, breaking the byte-identical baseline (SC-005). The
  sub-namespace is the minimal, honest reconciliation — low-level plumbing is
  deliberately kept out of the curated lifecycle-API surface that the baseline
  guards.
- *`[<assembly: InternalsVisibleTo("FS.GG.SDD.Commands")>]` + `internal` writers.*
  Rejected: constitution III forbids using top-level `internal`/`private`
  modifiers as visibility policy ("The `.fsi` is the sole declaration of public
  surface").
- *Duplicate-but-share via a shared `.fs` linked into both projects (no new
  assembly-public surface).* Rejected: produces two copies of the compiled
  functions (one per assembly), which does not satisfy SC-002's "exists in
  exactly one shared location" and reintroduces the drift hazard it's meant to
  remove.

---

## D2 — `writeDiagnostic` differs by `relatedIds` ordering: how is byte-identical output preserved?

**Decision**: The unified `writeDiagnostic'` takes the **string-list ordering as
a parameter** (or, equivalently, takes the `relatedIds`-writing function),
because the two current implementations are *not* behaviorally identical.

**Rationale — verified, not assumed**:
- `Diagnostics.create` (`Diagnostics.fs:34`) stores `RelatedIds = relatedIds`
  **verbatim** — it does **not** sort. So ordering is decided at serialization.
- Commands `writeDiagnostic` (`CommandSerialization.fs:61`) calls the Commands
  `writeStringList`, which **sorts** (`CommandSerialization.fs:15`,
  `List.sort`).
- Artifacts `writeDiagnostic` (`Serialization.fs:181`) calls the Artifacts
  `writeStringList`, which does **not** sort (`Serialization.fs:21`).
- Therefore the two `writeDiagnostic` twins can emit `relatedIds` in **different
  order** for the same input. A naive merge picking one default would silently
  reorder one serializer's arrays → byte regression (this is the spec's
  "Sort-behavior divergence" edge case, here confirmed concretely for diagnostics
  specifically). The shared writer must preserve each caller's ordering by
  parameter.

**Mechanism**: model ordering as an explicit value the caller passes — e.g.
`type StringListOrder = SourceOrder | Sorted`, and `writeStringList'` applies
`List.sort` only for `Sorted`. `writeDiagnostic'` threads the caller's chosen
order into the `relatedIds` write. Commands passes `Sorted`; Artifacts passes
`SourceOrder`. Same primitive, two call-site parameters, byte-identical output
for both.

**Alternatives considered**:
- *Sort `relatedIds` at construction in `Diagnostics.create` so ordering is moot.*
  Rejected: changes Artifacts work-model output ordering (currently source-order)
  → byte regression in `serializeWorkModel`; out of scope and contrary to FR-006.
- *Assume `relatedIds` are already sorted at every call site so the difference is
  inert.* Rejected: not guaranteed across ~113 Commands constructors and the
  work-model path; the safe, mechanical design parameterizes ordering and lets
  the byte-identical golden test confirm.

---

## D3 — `option` vs bare digest writers

**Decision**: The shared digest writer is the **`option`-aware** form
(`writeDigestObject : name -> SourceDigest option -> unit` and the `OutputDigest`
analogue), and the bare Artifacts callers pass `Some digest`.

**Rationale**:
- Artifacts has bare `writeDigest`/`writeOutputDigest` (`Serialization.fs:24,30`,
  always write an object) and the `None` case is handled *inline at each call site*
  (`writeNull`, e.g. `Serialization.fs:146-148`, `128-130`, `42-44`).
- Commands has `writeSourceDigest`/`writeOutputDigest`
  (`CommandSerialization.fs:18,27`) which are already `option`-wrapped
  (`Some → object`, `None → writeNull`).
- The `option`-aware primitive is the superset: it serves Commands directly, and
  serves the bare Artifacts object-writes via `Some`. The bytes are identical —
  in both the `Some` object shape (`algorithm`, `value`) and the `None`/absent
  rendering — because the inner field writes are already identical across both
  modules. This matches the "`option` vs non-`option` digest writers" edge case.

**Alternatives considered**:
- *Two primitives (bare + option).* Rejected: reintroduces the duplication SC-002
  targets; the `option` form with `Some` at bare call sites is strictly simpler.

---

## D4 — How is the diagnostic builder collapsed without touching `CommandReports.fsi`?

**Decision**: Keep `commandDiagnostic` (the existing public 6-arg helper,
`CommandReports.fsi:7-14`) byte-stable. Add **internal-only** convenience layers
*beneath* the named functions:
1. an **error-default** builder (severity fixed to `DiagnosticError`) so the ~99
   error call sites stop re-spelling the literal;
2. **family helpers** (`missing*`, `malformed*`, `duplicate*`, `unknown*`,
   `stale*`, `unsafe*`/`failed*`) that capture each family's shared
   id/path/message/correction shape.

The ~113 named functions remain as thin call sites with **unchanged signatures**;
the 14 warning constructors call a warning-default variant (or pass
`DiagnosticWarning` explicitly through the existing helper) so no warning is
promoted to error.

**Rationale**:
- The named functions and their `.fsi` signatures are the downstream contract
  (FR-003, FR-007, Assumption "named functions are the contract"). Only *how* they
  are built changes.
- The new convenience builders are not in `CommandReports.fsi`, so they are
  private-by-omission — they add no public surface and cannot trip the Commands
  baseline. `commandDiagnostic` stays public exactly as today.
- Severity correctness (the "severity is not uniform" edge case) is mechanical:
  the 14 warning functions are enumerated from the source
  (`grep DiagnosticWarning`) and each is verified to still resolve to
  `DiagnosticWarning` after the collapse; the byte-identical golden test catches
  any accidental flip.

**Alternatives considered**:
- *Replace the named functions with a generic table/dispatch keyed by id.*
  Rejected: would change call-site signatures and risks id/message drift;
  violates FR-003's "without merging or renaming the named functions."

---

## D5 — What is the binding verification, and is new test code needed?

**Decision**: Reuse the existing guards as the gate; capture a **pre-change JSON
baseline** as the fixture; add new assertions **only if** a coverage gap is found.

**Rationale**:
- Byte-identical output: the per-command golden assertions
  (`*CommandTests.fs`) and `ReleaseDeterminismTests.fs` already pin command
  `--json`; the Artifacts work-model serialization tests pin `serializeWorkModel`.
  Capture the current `--json` for the representative set (charter, analyze,
  refresh, and a diagnostic-emitting failure path — SC-004) and the work-model
  JSON *before* the change, then diff after each story.
- Byte-stable `.fsi` + surface baselines: the three `SurfaceBaselineTests` and a
  `git diff` over the named `.fsi` files are the guard (SC-005).
- Release gate: the R5 FS3261/FS0025 `WarningsAsErrors` build must stay green
  (SC-007).
- This is behavior-preserving, so the *primary* evidence is that nothing moves.
  New test code is justified only where an existing test does not already cover a
  representative diagnostic family or digest/null shape; that gap check is a
  Phase-1/tasks item, not assumed up front.

**Open items for `/speckit-tasks`**: (a) confirm the representative command set
already has golden coverage or add a thin golden for the missing one; (b) decide
whether to add an explicit "every CommandReports diagnostic routes through the
builder" structural assertion (SC-001) or verify it by inspection/grep.
