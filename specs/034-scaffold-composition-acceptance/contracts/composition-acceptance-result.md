# Contract: `composition-acceptance-result` (schema v1)

The single deterministic, diffable per-run record of the composition acceptance (FR-011). It is
**verification harness output**, not a produced lifecycle artifact — a **declared exception** in
`docs/release/schema-reference.md` (same class as the `validate` `validation-report`), and is NOT
added to `release-readiness.json` (D5).

## Shape

```json
{
  "schemaVersion": 1,
  "generator": { "id": "fsgg-sdd-composition-acceptance", "version": "1.0.0" },
  "verdict": "pass",
  "inputs": {
    "provider": "rendering",
    "params": { "lifecycle": "sdd" }
  },
  "scaffoldOutcome": "providerSucceeded",
  "scaffoldDiagnostic": null,
  "facts": {
    "skeletonPresent": true,
    "constitutionPresent": true,
    "appBuilds": true,
    "appRuns": true,
    "gitInitialized": true,
    "scriptsExecutable": true,
    "provenancePartitioned": true,
    "refreshExcludes": true,
    "reportedComplete": true
  },
  "failure": null,
  "sensed": {
    "resolvedTemplateVersion": null,
    "providerAvailable": null,
    "host": null,
    "timestamp": null
  }
}
```

## Field rules

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | int | `1`. |
| `generator.id` / `.version` | string | pinned generator identity; a **hard-coded constant** (`version` is `"1.0.0"`, never derived from build/date/random) so the body is deterministic. |
| `verdict` | enum | `"pass" \| "fail" \| "skip-unavailable"`. |
| `inputs.provider` | string | always `"rendering"` (generic name, not an identifier). |
| `inputs.params` | object | always `{ "lifecycle": "sdd" }`. |
| `scaffoldOutcome` | string | the scaffold `--json` `outcome` value, verbatim — one of exactly four: `providerSucceeded` \| `providerSucceededEmpty` \| `providerNotRun` \| `providerFailed`. |
| `scaffoldDiagnostic` | null \| string | the scaffold diagnostic **code** that drove the verdict when `outcome` alone is ambiguous (`null` on a clean `providerSucceeded` pass). For `providerFailed` it is the discriminator between SKIP and FAIL: `scaffold.providerUnavailable` ⇒ `skip-unavailable`; `scaffold.providerWroteSddTree` / `scaffold.providerFailed` ⇒ `fail`. Recorded so the SKIP-vs-FAIL decision is diffable, not hidden. |
| `facts.*` | bool | each asserted fact (see [data-model.md](../data-model.md)); all `true` ⇔ `verdict="pass"`. |
| `failure` | null \| object | `null` on pass/skip; on fail, `{ "fact": <first failing fact>, "diagnostic": <surfaced scaffold/build/run message> }`. |
| `sensed.*` | nullable | legitimately variable metadata; carries real values at runtime, **null-normalized** for golden/diff comparison (D8). |

## Determinism contract (FR-011/SC-005)

- The **deterministic body** = the whole document except the `sensed` block. Two runs with the
  same inputs and an available provider produce a **byte-identical** body.
- The **`sensed`** block is the only region allowed to vary; it is set to `null` before any
  byte-comparison (golden test / two-run diff), mirroring `ValidationContracts` INV-5.
- Serialization is deterministic: stable key order, no timestamps/randomness in the body, UTF-8,
  the same `Utf8JsonWriter`-style emission the repo already uses for `scaffold-provenance.json`.

## Relationship to existing contracts

- Reads `scaffold-provenance.json` (`scaffold-provenance` v1) and the scaffold `--json` report;
  changes neither.
- Introduces no new outcome/exit code; `scaffoldOutcome` is a value from the existing vocabulary.
- Not refreshed by `refresh`; not catalogued in `release-readiness.json`.
