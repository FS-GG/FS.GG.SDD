# Phase 1 Data Model: Scaffold Composition Acceptance

This feature introduces **one** new structured entity — the per-run **result document**. It
*reads* existing entities (`scaffold-provenance.json`, the scaffold `--json` report) without
changing them. No lifecycle artifact, provider schema, or provenance schema is modified.

## Entity: Composition acceptance run

One execution of the real-provider scaffold acceptance.

**Inputs** (drive the run; recorded in the result body):

| Field | Type | Source | Notes |
|---|---|---|---|
| `provider` | string | fixed `"rendering"` | the author-supplied provider *name*; generic token, not an identifier |
| `params` | map | fixed `{ "lifecycle": "sdd" }` | forwarded verbatim by scaffold |
| `registryRef` | string | `FSGG_SDD_ACCEPTANCE_REGISTRY` | path to the **external** author-supplied `.fsgg/providers.yml`; copied into the product root before the run |
| `productRoot` | string (sensed) | temp dir | empty directory the run targets |

**Verdict** (the single headline result):

```text
Verdict = Pass | Fail of FailReason | SkipUnavailable
```

- `Pass` — every asserted fact (below) is true and the scaffold reported complete.
- `SkipUnavailable` — the provider could not be resolved (network/feed/version); the run made no
  claim about SDD correctness. Never emitted as Pass or Fail.
- `Fail of FailReason` — at least one asserted fact failed, or the scaffold was a provider defect
  / user-input/config error / incomplete. `FailReason` carries the first failing fact and the
  surfaced scaffold/build/run diagnostic.

**Asserted facts** (each a boolean in the result; all must hold for `Pass`):

| Fact | Assertion | Requirement |
|---|---|---|
| `skeletonPresent` | the SDD skeleton (reused `init` effects) exists in the product | FR-002 |
| `constitutionPresent` | `.fsgg/constitution.md` exists | FR-002 |
| `appBuilds` | `dotnet build` over the product succeeds (300 s timeout) | FR-003 |
| `appRuns` | a **headless** run smoke (no display server: a `--help`/`--version`-style probe, else a headless launch that survives a 10 s grace window without a non-zero exit; 60 s overall timeout) starts the app without crashing | FR-003 |
| `gitInitialized` | a git repo exists at the product root **or** init was explicitly skipped-non-fatal | FR-004 |
| `scriptsExecutable` | every produced `.sh` has the executable bit (or none were produced) | FR-004 |
| `provenancePartitioned` | every provider-produced path is `generatedProduct`; no skeleton/constitution path is `generatedProduct` | FR-005 |
| `refreshExcludes` | after `refresh`, provider/app paths are byte-unchanged; only SDD-owned views regenerate | FR-006 |
| `reportedComplete` | the scaffold `--json` `outcome` is the success outcome with the scaffold complete | FR-007 |

## Entity: Result document (`composition-acceptance-result` v1) — NEW

The diffable per-run record (FR-011). Schema fixed in
[contracts/composition-acceptance-result.md](./contracts/composition-acceptance-result.md).
Structure (two regions: a **deterministic body** and a normalized **`sensed`** block):

```text
{
  "schemaVersion": 1,
  "generator": { "id": "...", "version": "..." },   // deterministic (pinned generator)
  "verdict": "pass" | "fail" | "skip-unavailable",
  "inputs": { "provider": "rendering", "params": { "lifecycle": "sdd" } },
  "scaffoldOutcome": "providerSucceeded" | "providerSucceededEmpty" | "providerNotRun" | "providerFailed",
  "scaffoldDiagnostic": null | "scaffold.providerUnavailable" | "scaffold.providerWroteSddTree" | ...,
  "facts": {                                         // each fact above as a bool
    "skeletonPresent": true, "constitutionPresent": true, "appBuilds": true,
    "appRuns": true, "gitInitialized": true, "scriptsExecutable": true,
    "provenancePartitioned": true, "refreshExcludes": true, "reportedComplete": true
  },
  "failure": null | { "fact": "...", "diagnostic": "..." },
  "sensed": {                                        // NORMALIZED TO null for golden/diff (D8)
    "resolvedTemplateVersion": "...", "providerAvailable": true,
    "host": "...", "timestamp": "..."
  }
}
```

- **Deterministic body** = everything except `sensed`. Two same-input, provider-available runs
  produce a byte-identical body (SC-005).
- **`sensed`** = legitimately variable metadata; null-normalized before golden comparison, exactly
  like the `validate` report's sensed block (INV-5, `ValidationContracts.fs`).

## State transitions (verdict resolution)

```text
resolve registry ──unset/missing──▶ (test SKIP via Assert.Skip; no document, inner loop green)
        │ present
        ▼
run scaffold(provider=rendering, lifecycle=sdd) over external registry
        │  read (outcome, diagnostic code) — both, because `providerFailed` is overloaded
        │
        ├─ providerFailed + diag scaffold.providerUnavailable ─▶ Verdict = SkipUnavailable
        ├─ providerFailed + diag scaffold.providerWroteSddTree ─▶ Verdict = Fail(providerDefect)
        ├─ providerFailed + diag scaffold.providerFailed ─────▶ Verdict = Fail(providerDefect)
        ├─ providerNotRun (providerMissing/Unknown/VersionUnsupported/ParamMissing/targetCollision) ─▶ Verdict = Fail(configError)
        ├─ providerSucceededEmpty (diag scaffold.providerEmpty) ─▶ Verdict = Fail(incomplete)
        └─ providerSucceeded
                 │
                 ▼ assert facts (build, run, provenance partition, refresh, complete, git, chmod)
                 ├─ any fact false ─────────────────▶ Verdict = Fail(<first failing fact>)
                 └─ all facts true ─────────────────▶ Verdict = Pass
```

Three notes: (1) the env-unset path is an xUnit **skip** (no document written); the
`SkipUnavailable` **verdict** is a *run that happened* but the provider was unreachable — both
are honest non-PASS results but distinct (FR-008, SC-004). (2) `ScaffoldOutcome` has exactly four
values; provider-**unavailable** and provider-**wrote-SDD-tree** both surface as `providerFailed`
and are separated **only** by the diagnostic code — so the verdict is keyed on the
`(outcome, diagnostic)` pair, never the outcome alone (otherwise SKIP collapses into FAIL). (3) The
driving diagnostic code is recorded in the result's `scaffoldDiagnostic` field so the decision is
diffable.

## Read-only entities (unchanged)

- **`scaffold-provenance.json`** (`ScaffoldProvenance.ScaffoldProvenanceRecord`, schema v1) —
  read via `ScaffoldProvenance.tryParse`; the acceptance inspects `ProducedPaths[].Owner` for the
  partition (`GeneratedProduct` for provider paths; skeleton/constitution paths absent from the
  produced set). Byte-unchanged by this feature.
- **Scaffold `--json` report** — read for `outcome`/completeness. Byte-unchanged.
- **Author-supplied registry** (`ProviderDescriptor` via `parseProviderRegistry`) — external,
  read-only, supplied through `FSGG_SDD_ACCEPTANCE_REGISTRY`. Never committed to this repo.
