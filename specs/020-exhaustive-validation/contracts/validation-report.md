# Contract: `validation-report` (deterministic JSON)

The single machine-readable artifact the harness emits (FR-006/FR-007). It is a
**new** public contract at `schemaVersion = 1`. It is the automation contract;
`--text` is a portable projection of the same facts; `--rich` is deferred (research
Decision 6).

## Shape (canonical JSON, stable key order)

```json
{
  "schemaVersion": 1,
  "generatorVersion": { "id": "FS.GG.SDD.Artifacts", "version": "0.2.0" },
  "matrices": [
    {
      "name": "lifecycle-output",
      "dimensions": ["command", "projection", "state"],
      "cells": [
        { "coordinates": [["command","ship"],["projection","json"],["state","fresh"]],
          "status": { "skippedWithReason": "ship is invalid for a fresh project" } },
        { "coordinates": [["command","verify"],["projection","text"],["state","verified"]],
          "status": { "pass": true } },
        { "coordinates": [["command","plan"],["projection","rich"],["state","planReady"]],
          "status": { "fail": { "id": "...", "message": "...", "path": "...", "correction": "..." } } }
      ]
    }
  ],
  "summary": {
    "passed": 0, "failed": 0, "skipped": 0, "coverageGaps": 0, "notValidated": 0,
    "overallPassed": true
  },
  "sensed": { "startedAtUtc": null, "durationMs": null, "host": null }
}
```

## C-1 — Determinism & the sensed fence (FR-007 / INV-2 / INV-5)

- Serializing the report twice over identical source inputs is **byte-identical**
  once the `sensed` object is excluded from the comparison.
- The serialized form carries **no** clock, duration, host path, ordering
  nondeterminism, or ANSI escape outside `sensed`.
- `sensed` is the **only** place a wall-clock/duration/host fact may appear. In the
  deterministic comparison and in golden fixtures, `sensed` fields are normalized to
  `null`. Sensed metadata never affects `overallPassed` or the exit code.

## C-2 — Status encoding

`status` is a tagged object with exactly one key:

| `CellStatus` | JSON |
|---|---|
| `Pass` | `{ "pass": true }` |
| `Fail d` | `{ "fail": <Diagnostic> }` |
| `SkippedWithReason r` | `{ "skippedWithReason": "<r>" }` |
| `CoverageGap s` | `{ "coverageGap": "<surface>" }` |
| `NotValidated r` | `{ "notValidated": "<r>" }` |

`<Diagnostic>` reuses the existing `FS.GG.SDD.Artifacts.Diagnostics` JSON shape and
**must** identify the matrix, the cell coordinates, and the affected
contract/artifact (FR-006).

## C-3 — Summary & exit code (FR-006 / INV-6)

- `summary` counts cells per status across all matrices.
- `overallPassed = false` iff any cell is `Fail`, `CoverageGap`, or `NotValidated`;
  `Skipped` and `Pass` do not fail the run.
- Exit code: `overallPassed` ⇒ `0`; otherwise non-zero.

## C-4 — Field stability

`schemaVersion` is **Stable**; all other fields are **AdditiveOptional** (consumers
MUST tolerate unknown fields), consistent with every other SDD JSON contract. The
report is **not** itself added to the `release-readiness.json` catalog: it carries
sensed metadata and is the harness *output*, not a lifecycle artifact (research
Decision 6).

**This exclusion MUST be explicit, not silent** — the same anti-omission principle
this feature enforces (FR-012 / 018's "every public output has exactly one catalog
entry"). The `validation-report` contract is documented as a **declared exception** in
`docs/release/schema-reference.md` (a short note: a public contract intentionally not
catalogued because it carries sensed metadata and is harness output rather than a
produced lifecycle artifact). The coverage-reconciliation surface (matrix-runner C-7)
therefore does not flag it as a `CoverageGap`, because the exclusion is recorded rather
than absent.

## C-5 — Completeness (INV-1)

Every declared cell of every matrix appears exactly once with exactly one status. An
interrupted/partial run still emits a well-formed report in which untouched cells are
`NotValidated` (never absent, never `Pass`).
