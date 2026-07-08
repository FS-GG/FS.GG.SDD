# Phase 1 Data Model: Slim the Evidence Declaration Shape

**Feature**: `091-evidence-obligation-shape` | **Date**: 2026-07-08

## Scope

No type changes. `EvidenceDeclaration`, `SyntheticDisclosure`, and every other record in
`FS.GG.SDD.Artifacts.Evidence` are untouched, as is `Evidence.fsi`. This document describes the
**rendering rule** that maps the existing in-memory model to on-disk YAML.

## The in-memory model (unchanged)

The five fields this feature touches, as declared in `Evidence.fsi`:

```fsharp
type SyntheticDisclosure = { StandsInFor: string; Reason: string }

type EvidenceDeclaration =
    { // …
      SyntheticDisclosure: SyntheticDisclosure option
      Rationale: string option
      Owner: string option
      Scope: string option
      LaterLifecycleVisibility: string option
      // … }
```

Each is an `option`. The model already says "this may be absent." Only the writer disagreed.

## The rendering rule

| In-memory value | Before (current) | After (this feature) |
|---|---|---|
| `Rationale = None` | `    rationale: null` | *(no line)* |
| `Rationale = Some "because"` | `    rationale: "because"` | `    rationale: "because"` |
| `Rationale = Some "null"` | `    rationale: "null"` | `    rationale: "null"` |
| `Owner = None` | `    owner: null` | *(no line)* |
| `Scope = None` | `    scope: null` | *(no line)* |
| `LaterLifecycleVisibility = None` | `    laterLifecycleVisibility: null` | *(no line)* |
| `SyntheticDisclosure = None` | `    syntheticDisclosure: null` | *(no line)* |
| `SyntheticDisclosure = Some d` | 3-line nested mapping | 3-line nested mapping *(unchanged)* |

Ordering among **present** optionals is unchanged: `syntheticDisclosure`, `rationale`, `owner`,
`scope`, `laterLifecycleVisibility` — emitted after `synthetic:` and before `notes:`.

## The reading rule (unchanged, and why omission is safe)

| YAML | `tryScalarNonNullAt` | Model |
|---|---|---|
| key absent | `tryChild` → `None` | `None` |
| `rationale: null` | `isPlainNullScalar` → `true` | `None` |
| `rationale: Null` / `NULL` / `~` | `isPlainNullScalar` → `true` | `None` |
| `rationale:` *(empty)* | `isPlainNullScalar` → `true` | `None` |
| `rationale: "null"` *(quoted)* | `Style ≠ Plain` → not null | `Some "null"` |
| `rationale: because` | scalar | `Some "because"` |

The first four rows collapse to the same `None`. That equivalence — already relied on by feature
161 — is what makes omission a serialization change rather than a schema change.

`syntheticDisclosure` follows the same shape one level down: `parseSyntheticDisclosure` requires
both `standsInFor` and `reason` to resolve to non-whitespace scalars, so an absent mapping and a
`null` mapping both yield `None`.

## Round-trip invariants

For every declaration `d` and its rendering `r = render d`:

1. **Parse-render fixpoint (FR-007)**: `render (parse r) = r`. Re-running `evidence` on a slim file
   changes no byte.
2. **Model preservation (FR-005, SC-003)**: `parse (renderVerbose d) = parse (renderSlim d) = d` for
   the five fields. The verbose and slim renderings are two spellings of one value.
3. **Quoted-`"null"` fidelity (FR-006)**: if `d.Rationale = Some "null"` then `r` contains
   `rationale: "null"` (quoted), and `parse r` recovers `Some "null"`. This is *not* the `None` case
   and must never be omitted.
4. **Well-formedness (FR-004)**: `r` contains no blank line and no trailing whitespace regardless of
   how many optionals are `None`; `parseEvidenceArtifact` accepts it.

Invariants 1 and 3 are the feature-161 guarantees, carried forward. Invariant 2 is what this
feature adds. Invariant 4 is the implementation hazard called out in `research.md` R4.

## Worked example

**Before** — one declaration, all five optionals `None` (20 lines):

```yaml
  - id: EV-001
    kind: implementation
    subject:
      type: "task"
      id: "T001"
    taskRefs: ["T001"]
    requirementRefs: []
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    checklistResultRefs: []
    planDecisionRefs: []
    obligationRefs: []
    artifacts: []
    sourceRefs: []
    result: missing
    synthetic: false
    syntheticDisclosure: null
    rationale: null
    owner: null
    scope: null
    laterLifecycleVisibility: null
    notes: []
```

**After** — the same declaration (15 lines):

```yaml
  - id: EV-001
    kind: implementation
    subject:
      type: "task"
      id: "T001"
    taskRefs: ["T001"]
    requirementRefs: []
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    checklistResultRefs: []
    planDecisionRefs: []
    obligationRefs: []
    artifacts: []
    sourceRefs: []
    result: missing
    synthetic: false
    notes: []
```

Both parse to the same `EvidenceDeclaration`. At 16 obligations that is 80 lines removed (SC-002).

**After** — a declaration that *does* carry a disclosure and a rationale (nothing is omitted):

```yaml
  - id: EV-007
    kind: synthetic
    subject:
      type: "task"
      id: "T007"
    # … ref buckets …
    result: pass
    synthetic: true
    syntheticDisclosure:
      standsInFor: "a real headless render"
      reason: "no GPU on the CI runner"
    rationale: "accepted deferral, see DEC-004"
    notes: []
```
