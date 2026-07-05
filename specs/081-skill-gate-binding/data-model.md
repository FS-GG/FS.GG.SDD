# Data Model: Bind SDD authoring skills to the CLI gate grammar

**Feature**: 081-skill-gate-binding · **Date**: 2026-07-05

This feature adds no persisted schema. The "entities" below are the small typed values and file-level structures the binding introduces or relies on.

## 1. RequiredKeys registry (new typed value)

The single authoritative source of "which keys the gate requires", so prose skills can be *checked against a contract* rather than against other prose (FR-009).

- **`requiredFrontMatterKeys : LifecycleStage -> string list`**
  - Returns the required front-matter keys for a stage's artifact.
  - Known values (from the per-parser tuple matches, to be centralized): e.g. `Clarify → [ "schemaVersion"; "workId"; "stage"; "sourceSpec" ]` (`Clarification.fs:121-122`). Other stages populated from their parsers.
  - "Required" = the key whose absence makes the parser/gate block; *defaulted* keys are excluded (they don't block).
- **`requiredDeferralKeys : string list`**
  - `[ "rationale"; "owner"; "scope"; "laterLifecycleVisibility" ]` — the keys an evidence declaration with `result: deferred` (`kind: deferral`) must carry, enforced today by `missingDeferralRationale` in `HandlersEvidence.fs`.
- **Location**: `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/RequiredKeys.fs` (+ `.fsi`), or folded into an existing artifacts module if a natural home exists.
- **Validation invariant**: for every stage, `requiredFrontMatterKeys stage` must equal the set the parser actually enforces (guarded by a behavioral parser test that omits each key and asserts a block).
- **Relationships**: consumed by `RequiredFieldContractTests` (checks skills + §5 table) and asserted by Artifacts parser tests.

## 2. Skill example marker (authoring-surface annotation)

A machine-readable binding annotation preceding a runnable fenced block in a stage `SKILL.md`. Adds **no content**; it names the corpus file the block must agree with.

- **Grammar**: `<!-- fsgg-sdd:example corpus=<file> [mode=contains|equals] -->` immediately before a fenced code block; a counter-example uses `<!-- fsgg-sdd:example counter -->`. Full grammar in `contracts/example-marker.md`.
- **Fields**:
  - `corpus` — the `docs/examples/lifecycle-artifacts/<file>` this block must match.
  - `mode` — `contains` (default; the block is a normalized substring of the corpus file, for fragments) or `equals` (the block is the whole file).
- **Lifecycle**: authored by hand in `SKILL.md`; survives the byte-identical `.claude`↔`.codex` mirror; read by the doctest extractor.
- **Relationships**: each marked block → exactly one corpus file. Unmarked fences are ignored.

## 3. Example corpus (existing files, brought to gate coherence)

The coherent, copyable artifact set under `docs/examples/lifecycle-artifacts/` that the doctest runs through the real gates.

- **Members**: `charter.md`, `spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, `tasks.yml`, `evidence.yml` (all for one work item id).
- **Invariants**:
  - The set is **internally coherent** — shared `workId`, cross-artifact ids resolve (FR-covers-AC, CHK back-refs, evidence obligation refs).
  - Running each artifact through its gate yields **zero blocking diagnostics** (the doctest asserts this).
  - `spec.md` uses the gate-accepted coverage form (`- FR-###: … (covers AC-###)`), non-bold, single physical line.
  - `evidence.yml` contains **at least one** `result: deferred` declaration carrying all `requiredDeferralKeys`, alongside satisfying `result: pass` entries.
- **Relationships**: target of every skill example marker; input to `SkillGateDoctestTests`.

## 4. `missingChecklistBackReference` diagnostic (new command diagnostic)

A distinct diagnostic naming the real cause of a checklist `CR-###` review line lacking its `[CHK:CHK-###]` back-reference.

- **Id**: `missingChecklistBackReference` (JSON `Diagnostic.Id`).
- **Severity**: DiagnosticError (blocking) — unchanged from the current behavior; only the *identity/message* changes.
- **Emission**: source at `Checklist.fs` `checklistReferenceDiagnostics` (was `workModelInconsistent`); surfaced by a `CommandReports.missingChecklistBackReference` constructor.
- **Message**: names the missing `[CHK:CHK-###]` back-reference and the offending `CR-###` result id.
- **Remediation pointer**: points at the checklist back-reference grammar (not front matter).
- **Relationships**: replaces `malformedChecklistFrontMatter` for this one case; the genuine front-matter/schema causes retain `malformedChecklistFrontMatter`. Full contract in `contracts/diagnostic-missing-back-reference.md`.

## 5. Doctest / field-check results (transient test outputs)

Not persisted artifacts — xUnit assertions. Included for completeness of the model:

- **Doctest outcome per skill example**: `(skill, corpusFile) → Pass | Rejected(diagnosticId) | ConsistencyMismatch`. A non-`Pass` fails the build naming the skill and cause (FR-003/004).
- **Field-check outcome per surface**: `(surface, stage) → Match | Missing(field) | Extra(field)`. A non-`Match` fails the build naming the field and surface (FR-009).
