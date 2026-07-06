# Implementation Plan: Dev-Repo Provenance (085)

**Spec**: `spec.md` · **Contract**: `contracts/dev-repo-provenance.md` · **Status**: Implemented

## Approach

Introduce the dev-repo provenance shape as an **additive** third state in the existing
`ScaffoldProvenanceRecord` (schema stays v1). No structural/type change: a new `outcome`
value + empty provider/template fields express it. Have the `init` **command** write it; the
existing `Drift.compute` `Some record` branch already reconciles a provider-less record
correctly (the template re-pin is unconditionally `noTarget`, the CLI axis falls back to the
record's `None` minimum, the artifact axis comes from the seeded manifest), so the only
behavioral touch is reporting `ProviderName = None` for a dev-repo instead of the empty string.

## Layers touched

1. **`FS.GG.SDD.Artifacts/ScaffoldProvenance.fsi` + `.fs`** — add `devRepoOutcome`, `isDevRepo`,
   `devRepoRecord`. No serializer/parser change (empty strings + a new outcome round-trip under
   the unchanged v1 grammar).
2. **`FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`** — add `devRepoProvenance` (producedPaths
   = `Drift.expectedArtifactPaths`, owner `Sdd`) and `initProvenanceEffect`; append the write to
   the `Init` dispatch **only** (`| Init, _ -> [], initEffects request @ [ initProvenanceEffect request ]`),
   keeping it out of the shared `initEffects` seam that `scaffold`/`upgrade` reuse.
3. **`FS.GG.SDD.Commands/CommandWorkflow/Drift.fs`** — in the `Some record` branch, report
   `ProviderName = None` when `isDevRepo record`.

## Why no doctor/upgrade handler change

`HandlersDoctor`/`HandlersUpgrade` key off `Drift.compute` via `resolveProvenance`. With a
dev-repo record present, `HasProvenance = true`, so they leave the "no scaffold provenance —
nothing to reconcile" no-op automatically and report/reconcile through the shared path.
`resolveDriftDescriptor` finds no descriptor for the empty provider name (→ `None`), which is
the correct provider-less input.

## Determinism & isolation invariants

- `init` byte-identical for a fixed CLI version (the only variable is the recorded `generator`).
- The write is init-command-bound; `scaffold` still writes its provider provenance; `upgrade`
  re-seeds only the missing seeded set. The provenance file is excluded from
  `Drift.expectedArtifactPaths` (it is the anchor, not a reconciled artifact).

## Versioning

Additive-optional; schema stays v1; **minor** package bump. Public surface grows by exactly
three vals (recorded in `PublicSurface.baseline`). Rides the in-flight 0.8.0 release.
