# Phase 1 Data Model: Pre-flight authoring lint

Entities are lightweight F# records/DUs in `FS.GG.SDD.Commands`. Lint introduces **no persisted
artifact and no schema version** — every entity below is in-memory, surfaced only through the
`CommandReport` projection. The `Diagnostic` type is reused unchanged from `FS.GG.SDD.Artifacts`.

## ArtifactKind (DU) — new

The kind lint auto-detects (FR-002) and routes on.

| Case | Detected by (D5) | Routed parser(s) |
|---|---|---|
| `Charter` | front-matter `stage: charter` | `WorkItemMetadata` / charter parser |
| `Specification` | `stage: specify` | `Specification.parse*` |
| `Clarification` | `stage: clarify` / `clarifications.md` | `Clarification.parseClarificationFacts` |
| `Checklist` | `stage: checklist` / `checklist.md` | `Checklist.parseChecklistFacts` (+ coverage-line shape) |
| `Plan` | `stage: plan` | `Plan.parse*` |
| `Tasks` | `tasks.yml` | `Task.parseTaskFacts` |
| `Evidence` | `evidence.yml` | `Evidence.parseEvidence` |

- `Unrecognized` is **not** a case — an undetectable artifact yields the unusable-input outcome
  (exit 2), reported via a single `LintDefect` of class `Unresolvable`.

## LintDefectClass (DU) — new

The classification attached to each surfaced diagnostic, driving the grammar pointer (FR-007).

| Case | Source diagnostic id(s) | Grammar anchor (D7) |
|---|---|---|
| `CoverageLine` | `failedRequirementsQuality` | `authoring-contracts.md#acceptance-coverage-line` |
| `MissingDecisionTag` | `missingClarificationAnswer`, `unresolvedBlockingAmbiguity` | `#clarify-decision-tag-resolution` |
| `FrontMatter` | `malformed*FrontMatter` | `#per-stage-front-matter` |
| `DuplicateId` | `duplicate*Id` | `#per-stage-front-matter` (id-declaration rules) / relevant section |
| `Parse` | parser hard-fail (FR-015) | (none — parse-level) |
| `Unresolvable` | kind not detectable (FR-002) | (none — usage) |

- All classes except `Parse`/`Unresolvable` carry a resolvable pointer (SC-003: 100% of reportable
  grammar defects). `Parse`/`Unresolvable` are structural, not grammar defects.

## LintDefect (record) — new

One reported defect. Wraps the reused `Diagnostic` and adds the classification + pointer.

| Field | Type | Notes |
|---|---|---|
| `Class` | `LintDefectClass` | classification (above) |
| `Diagnostic` | `Diagnostic` | reused as-is — carries `Id`, `Severity=Error`, `Location {Line;Column}`, `Message`, `Correction` (fix hint, FR-007a) |
| `GrammarPointer` | `GrammarPointer option` | doc anchor + optional example tag (FR-007b); `None` only for `Parse`/`Unresolvable` |

**Validation / invariants**
- Every `LintDefect.Diagnostic.Severity = Error` (FR-017 — no warnings).
- Ordering is `(Location.Line, Location.Column, Diagnostic.Id)`, stable (FR-012/SC-005).
- `Class ∈ {CoverageLine, MissingDecisionTag, FrontMatter, DuplicateId}` ⇒ `GrammarPointer.IsSome`.

## GrammarPointer (record) — new

| Field | Type | Notes |
|---|---|---|
| `Doc` | `string` | always `docs/reference/authoring-contracts.md` (relative) |
| `Anchor` | `string` | a heading slug that MUST exist in the doc (drift-guarded) |
| `ExampleTag` | `string option` | a tagged fenced-block label (e.g. `coverage:accepted`), when a worked example exists |

## LintSummary (record) — new, on `CommandReport`

The report model for the lint projection (json/text/rich), analogous to `DoctorSummary`.

| Field | Type | Notes |
|---|---|---|
| `ArtifactPath` | `string` | the linted path (echoed) |
| `Kind` | `string` | detected `ArtifactKind` name, or `"unresolved"` |
| `Defects` | `LintDefect list` | ordered; empty ⇒ clean |
| `Outcome` | `LintOutcome` | `Clean` / `DefectsFound` / `UnusableInput` (drives exit 0/1/2) |

- `LintSummary.Defects = []` ⇔ `Outcome = Clean` ⇔ exit 0 (FR-013/SC-002).
- `Outcome = UnusableInput` ⇔ missing/unreadable/unrecognized (exit 2, FR-011/D5/D6).

## CommandRequest additions (additive) — `CommandTypes.fsi`

| Field | Type | Notes |
|---|---|---|
| `Artifact` | `string option` | the `lint <artifact>` positional path; ignored by other commands |
| `Explain` | `bool` | `<stage> --explain` dry-run flag; default `false` |

## Exit mapping (D6) — pure

```
Clean         -> 0
DefectsFound  -> 1
UnusableInput -> 2
```
Applied by `exitCodeForLint` for `Command = Lint || Request.Explain`; all other commands unchanged.

## Reused unchanged

- `Diagnostic` / `DiagnosticSeverity` / `SourceLocation` (`FS.GG.SDD.Artifacts/Diagnostics.fs`).
- All `parse*Facts` / `parseEvidence` parsers and `Internal.duplicateScopedDiagnostics`.
- `CommandReport`, `CommandOutcome`, `NextAction`, and the json/text/rich renderers.
