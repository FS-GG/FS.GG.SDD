# Contract: `missingChecklistBackReference` diagnostic

**Feature**: 081-skill-gate-binding · **Status**: proposed (Tier 1 command-output/JSON contract, additive)

Splits the missing-`[CHK:CHK-###]`-back-reference case out of `malformedChecklistFrontMatter` so the diagnostic names its real cause (#144, FR-010/FR-011).

## Identity

| Field | Value |
|---|---|
| `Id` | `missingChecklistBackReference` |
| Severity | `DiagnosticError` (blocking) — unchanged behavior; only identity/message differ |
| Stage classification | `Checklist` (added to the checklist stage-classifier lists in `DiagnosticConstructors.fs`) |
| Category (view) | its own case in `ViewGeneration.fs` (not `malformedSource`/front-matter) |

## When it fires

A checklist review result line (`CR-###`) in **Review Results** or **Accepted Deferrals** carries no `[CHK:CHK-###]` item reference — detected in `Checklist.checklistReferenceDiagnostics` (the `| None ->` branch that today emits `workModelInconsistent`).

## When it does NOT fire (retained `malformedChecklistFrontMatter`)

- malformed/parse-failed front matter;
- `malformedSchemaVersion` / `unsupportedSchemaVersion` / `futureSchemaVersion`;
- `sourceSpec` / `sourceClarifications` front-matter mismatch;
- genuine `workModelInconsistent` front-matter/identity conflicts.

These continue to emit `malformedChecklistFrontMatter`, which continues to name *front matter*.

## Message & remediation

- **Message**: identifies the offending `CR-###` result id and states the missing `[CHK:CHK-###]` back-reference is the cause.
- **Remediation pointer** (`RemediationPointers.fs`): points at the checklist back-reference grammar / example line (`- CR-### [CHK:CHK-###] …`), **not** the front-matter section. `RemediationPointersTests` requires every id to have a pointer — this one included.

## JSON contract impact

- **Additive**: consumers see a new `Id` value; the back-ref case's emitted `Id` moves from `malformedChecklistFrontMatter` to `missingChecklistBackReference`. Exit codes and blocking behavior unchanged.
- No persisted-schema version bump. `PublicSurface.baseline` (Commands **and** Cli) gains the sorted `FS.GG.SDD.Commands.CommandReports.missingChecklistBackReference` symbol.
- No deprecated alias — the old id is SDD-internal (no `.github`/Governance references).

## Test obligations

- `ChecklistCommandTests`: a missing-back-ref fixture now asserts `report.Diagnostics` contains `missingChecklistBackReference` (and *not* `malformedChecklistFrontMatter`).
- An Artifacts-layer parser test asserts `checklistReferenceDiagnostics` emits the new id for a `None` item ref.
- A retained-behavior test: a genuinely malformed front-matter checklist still emits `malformedChecklistFrontMatter`.
- `RemediationPointersTests` / `SurfaceBaselineTests` green with the new id.
