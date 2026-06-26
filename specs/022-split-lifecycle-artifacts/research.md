# Phase 0 Research: splitting `LifecycleArtifacts` in F#

All findings below were validated empirically with real `dotnet build` spikes,
not from memory.

## Decision 1 — Split mechanism

**Decision**: Retire the single `module LifecycleArtifacts`. Make **each artifact
family its own `[<AutoOpen>]` top-level module under namespace
`FS.GG.SDD.Artifacts`**, fronted by a shared `Internal` helper module and a shared
`Core` types module, with a `WorkItem` aggregator module last. Update in-repo
consumers to `open FS.GG.SDD.Artifacts`.

**Rationale**: This is the only structure that delivers the per-family split
(FR-001/FR-009) while keeping every name reachable with idiomatic access. The
relaxations granted during planning (no external consumers; tests are the only
behavioral gate) make the consumer edits acceptable.

**Alternatives considered and why rejected** (each tested):

- **Re-export facade (type abbreviations + value re-binds), zero consumer edits.**
  Tested with a two-file project. Result:
  - Explicit `Facade.parseFoo ()` and `(x : Facade.Foo)` — **compile** ✓.
  - `open Facade` then `{ A = 2; B = "y" }` — **`error FS0039: The record label
    'A' is not defined`** ✗.
  - `open Facade` then DU case `Y 5` — **`error FS0039: The value or constructor
    'Y' is not defined`** ✗.
  - Conclusion: a facade preserves explicit-qualified access (all 26 explicit
    `LifecycleArtifacts.<member>` sites here are functions or the `FileSnapshot`
    type, so they would survive) **but cannot bring record labels or DU cases
    into scope on `open`**. Because every family type is a record/DU and the 18
    `open ...LifecycleArtifacts` files construct records / use DU cases, a facade
    breaks them. Rejected.

- **`[<AutoOpen>]` family modules under the namespace, keep a `LifecycleArtifacts`
  facade for the explicit sites, zero consumer edits.** The 18 `open`-site files
  open `FS.GG.SDD.Artifacts.LifecycleArtifacts`, **not** the parent namespace
  (verified: none of the 18 also `open FS.GG.SDD.Artifacts`). So auto-opening at
  the namespace level does not reach them without editing their `open`. Rejected
  as a zero-edit option; adopted *with* the consumer edits (this is the chosen
  mechanism).

- **Keep one `module LifecycleArtifacts`, split only low-level helpers into
  preceding internal modules (zero consumer edits).** Hits an unavoidable
  forward-reference wall: family parsers build the public family types, so the
  types must precede the parsers; but the public types must live *in*
  `LifecycleArtifacts` for `open` to expose labels/cases, and the public parse
  bindings must also live in `LifecycleArtifacts`. Types-first + bindings-last in
  the same module is impossible without a second `module LifecycleArtifacts`
  declaration, which F# forbids. Net effect: only the family-agnostic Yaml/Json/
  Markdown helpers can leave, leaving the bulk in one file — fails FR-001/FR-009.
  Rejected.

- **Re-declare (copy) types in the facade instead of abbreviating.** Creates two
  distinct nominal types (`Core.PlanFacts` vs `LifecycleArtifacts.PlanFacts`);
  parser return types and the facade signature disagree. Type-identity conflict.
  Rejected (also violates FR-006 no-duplication).

## Decision 2 — Shared helpers placement

**Decision**: One `module internal FS.GG.SDD.Artifacts.LifecycleArtifacts.Internal`
(or `...Parsing`) compiled first, holding the family-agnostic helpers currently at
`LifecycleArtifacts.fs:704-823` (YAML access: `parseYaml`, `tryMapping`,
`tryScalar`, `tryChild`, `scalarList`, `schemaVersion`, `requiredScalar`,
`combine`, `normalizePath`, `artifact`/`sourceArtifact`), the Markdown helpers
(`frontMatter`, `proseStatus`, `sourceLocation`, `hasHeading`, `sectionLines`,
scoped-ID helpers), and the JSON helpers at `2212-2308` (`tryJsonProperty`,
`jsonString`, `jsonInt`, `jsonArray`, `jsonStringList`, `parseJsonDigest`, …).

**Rationale**: These are used by multiple families. Relocating them once (not
copying) satisfies FR-006 and lets every family module `open` a single helper
module. `internal` keeps them off the public surface, so no `.fsi` is required for
this module (Principle III applies to *public* modules).

**Alternatives**: Splitting helpers into `Yaml` / `Json` / `Markdown` submodules —
viable and slightly cleaner, deferred as an implementation nicety; a single
`Internal` module is the minimum that satisfies the requirement. The split into
two/three helper files is acceptable if any single file would otherwise be large.

## Decision 3 — Compile order & cycle-breaking

**Decision**: Linear order (full list in [data-model.md](./data-model.md)):
`Internal → Core → Config → WorkItemMetadata → Specification → Clarification →
Checklist → Plan → RequirementModel → Task → Analysis → Evidence → Verify → Ship
→ Guidance → WorkItem`.

**Rationale**: Verified there are no true family cycles. The only cross-family
type dependencies are:
- `FileSnapshot` (every parser) → in `Core`, first.
- `AnalysisSourceRecord`, `AnalysisGeneratedViewRecord`,
  `AnalysisOptionalBoundaryFact` (used by Analysis, Verify, Ship) → in `Core`.
- `EvidenceDisposition` / `RequiredTestDisposition` / `SkillVisibilityFact`
  (defined near Evidence in today's file but referenced only by `VerificationView`)
  → relocate into **`Verify`**, which is ordered after Evidence regardless. This
  breaks the apparent Evidence↔Verify coupling cleanly.
- `ParsedWorkItem` aggregates all families → `WorkItem` last.

**Alternatives**: Putting disposition types in `Core` also works; placing them in
`Verify` keeps them next to their only consumer and is preferred. Final type homes
are compiler-confirmed during implementation (move-until-it-compiles).

## Decision 4 — `.fsi` posture

**Decision**: Author a `.fsi` for every new **public** family module (Config,
Core, WorkItemMetadata, Specification, Clarification, Checklist, Plan,
RequirementModel, Task, Analysis, Evidence, Verify, Ship, Guidance, WorkItem).
The `Internal` helper module is `module internal` with no `.fsi`. Delete the old
`LifecycleArtifacts.fsi`.

**Rationale**: Constitution Principle III requires `.fsi` for public modules and
forbids `.fs`-level visibility modifiers as policy. Each family `.fsi` is the
slice of the old 722-line signature that belongs to that family, verbatim, so the
public surface is preserved exactly (just redistributed across modules).

## Decision 5 — Determinism & warnings

**Decision**: Move record/DU definitions **verbatim** (no field reordering); move
parser bodies verbatim; do not touch warning-affected code. Do not change
`<NoWarn>`/warning settings.

**Rationale**: FR-005's byte-identical guarantee has been **relaxed** by the
stakeholder to "tests pass," but the cheapest way to keep tests green and honor
FR-008 (relocate, don't change warnings) is still a verbatim move. FS3261/FS0025
sites for the JSON view parsers will concentrate in `Analysis.fs` / `Verify.fs` /
`Ship.fs`, which is the R3 setup benefit for R4/R5.

**Verification of warning posture**: capture the pre-refactor warning baseline
(`dotnet build` warning inventory) and compare after; counts should be unchanged
modulo file relocation. (The spec's 290 FS3261 / 4 FS0025 are unique-site counts;
raw per-project counts differ and are not the comparison basis.)

## Open questions

None blocking. Final type-home placement (e.g. `EvidenceObligation` in Evidence
vs Verify) is resolved mechanically by the compiler during implementation and
does not affect the contract.
