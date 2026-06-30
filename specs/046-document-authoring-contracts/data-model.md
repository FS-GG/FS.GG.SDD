# Phase 1 Data Model: Authoring Contracts

This feature adds no new structured artifact and no schema version. The "model" here is the set
of **authoring entities** the documentation and the drift guard must describe accurately. Each
maps to existing types/parsers in `FS.GG.SDD.Artifacts`.

## Coverage line

The sole input recognized by the coverage (strict-scan) parser, linking a functional requirement
to its acceptance scenario(s).

| Field | Meaning | Rule (verified) |
|---|---|---|
| Requirement id | `FR-###` (≥3 digits) | Must lead the list item as `- FR-###:` (literal `- `, id, literal `:`); case-insensitive. |
| Acceptance reference(s) | `AC-###` (and/or `US-###`) | Must appear on the **same line** as the `- FR-###:`; collected as the requirement's coverage. |
| Text | Free prose after the colon | Required by the regex (`:\s*(.+)$`) — the line must have content after the colon. |

- **Establishes coverage**: `- FR-001: W/S move the left paddle. (covers AC-002)`
- **Does NOT establish coverage** (counted by the loose scan, invisible to the strict scan):
  - `**FR-001** W/S move the left paddle. (AC-002)` — bold id, no leading `- FR-001:`
  - `- FR-001 — moves paddle (AC-002)` — no colon after the id
  - A separate line `(covers AC-002)` not on the `- FR-001:` line
- **Source**: `LifecycleArtifacts/Specification.fs` `requirementReferences`; consumed by
  `CommandWorkflow/ParsingMid.fs` `requirementCoverage`.

## Evidence declaration

An authored `evidence.yml` entry that may satisfy an obligation emitted by `evidence`/`verify`.

| Field | Values (verified) | Rule |
|---|---|---|
| `kind` | `implementation` · `verification` · `review` · `generated-view` · `synthetic` · `deferral` · `note` · `missing` | Unrecognized value **silently falls back to `verification`** (`parseEvidenceKind`). |
| `result` | `pass` · `fail` · `deferred` · `missing` · `stale` · `advisory` · `blocked` | Normalized by trim + lowercase. |
| `synthetic` | boolean | A `synthetic: true` declaration does **not** satisfy an obligation even with `result: pass`. |
| obligation linkage | obligation id / task / requirement refs | A declaration matches an obligation by id. |

- **Satisfaction rule (verified)**: an obligation is **satisfied** iff a matching declaration has
  `result` normalizing to `pass` **and** `synthetic = false`.
  - `synthetic: true` + `pass` → disposition `synthetic` (discloses a stand-in; not satisfied).
  - `result: deferred` or `kind: deferral` → disposition `deferred`.
  - The same non-synthetic-`pass` rule is applied by **two** disposition ladders: `verify`
    (`HandlersVerify.fs:155-158`, non-synthetic pass ⇒ `"satisfied"`) and `evidence`
    (`HandlersEvidence.fs:396-398`, non-synthetic pass ⇒ `"supported"`). There is no single
    shared public satisfaction predicate; the SC-005 drift guard re-expresses the one-line rule
    over `parseEvidence` output (see contracts/authoring-reference.md) and T002 keeps it in sync.
- **Source**: `LifecycleArtifacts/Evidence.fs` (`EvidenceKind`, `parseEvidenceKind`),
  `CommandWorkflow/HandlersEvidence.fs` (`normalizedEvidenceResult`, `evidence` disposition ladder),
  `CommandWorkflow/HandlersVerify.fs` (`verify` disposition ladder).

## Specification intent (`--input`)

The facts the `specify --input` parser extracts to decide whether a new spec can be drafted.

| Fact | Detected when | Diagnostic when missing |
|---|---|---|
| user value | a `value:` label, or any unlabeled line | named in `missingSpecificationIntent` message |
| scope | a `scope:` label | named when absent |
| measurable requirement | a `requirement:` label | named when absent |

- **Rule (verified, `ParsingEarly.fs`)**: the parser reads `label: value` lines; an unlabeled
  line becomes `value`. The `missingSpecificationIntent` **message already names** the specific
  missing facts; only the **correction** (expected labeled form) and the documentation are
  missing.

## Authoring diagnostic (the mutable surface)

`Diagnostic` (`FS.GG.SDD.Artifacts/Diagnostics.fs`) and the CHK review record carry:

| Field | This feature |
|---|---|
| `Id` (code) | **unchanged** |
| `Severity`, blocking | **unchanged** |
| `Location`, `RelatedIds` | unchanged (may already carry the offending id) |
| `Message` | unchanged except where it already names facts (intent) |
| `Correction` | **enriched** for the three sites — shows the exact expected form |

`Correction`/`Message` are serialized into the default JSON (`Json/JsonWriters.fs`),
`work-model.json` and readiness views (`ViewGeneration.fs`), and `checklist.md`. Changing them is
a deliberate, golden-covered, Tier-1 string-value change; the JSON **field set** and all
**codes/outcomes** are invariant.

## Authoring reference (new doc)

`docs/reference/authoring-contracts.md` — the durable publication of the three entities above,
plus tagged, machine-checkable example blocks the drift guard reads. Mirrored in
`docs/quickstart.md` at the `checklist` and `evidence` stages, and summarized in both agent
`SKILL.md` files. Names SDD's `evidence.yml` distinctly from any unrelated "evidence" doc a
scaffolded product may ship (FR-011).
