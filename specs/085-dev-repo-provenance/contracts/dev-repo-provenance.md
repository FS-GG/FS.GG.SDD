# Contract: `scaffold-provenance` schema v1 — the dev-repo document

Artifact: `.fsgg/scaffold-provenance.json` · Owner: `sdd` · Stability: `additiveOptional` ·
Schema version: **stays 1** (additive) · Serves FR-001…FR-007.

## Change

Introduce a third provenance state — a provider-less **dev-repo** document — written by
`fsgg-sdd init`. It is expressible in the existing `ScaffoldProvenanceRecord` with no
structural change: a new `outcome` value plus empty provider/template fields.

New public surface on `ScaffoldProvenance` (module `FS.GG.SDD.Artifacts`):

- `val devRepoOutcome: string` — `"devRepoInit"`, the discriminating `outcome`.
- `val isDevRepo: ScaffoldProvenanceRecord -> bool` — `Outcome = devRepoOutcome`.
- `val devRepoRecord: GeneratorVersion -> ScaffoldProducedPath list -> ScaffoldProvenanceRecord`
  — the canonical constructor (empty provider/contract/template, `None` minimum,
  `devRepoOutcome`, empty mirrored/effective).

## JSON shape (dev-repo document)

```jsonc
{
  "schemaVersion": 1,
  "generator": { "id": "FS.GG.SDD.Artifacts", "version": "0.8.0" }, // the CLI that init'd (FR-001)
  "requiredMinimumCliVersion": null,                                // dev-repo declares none (FR-005)
  "providerName": "",                                               // no provider (FR-001)
  "providerContractVersion": "",
  "templateRef": "",                                                // no template pin
  "outcome": "devRepoInit",                                         // the dev-repo marker (FR-004)
  "producedPaths": [ /* the seeded skeleton, owner "sdd", sorted (FR-002) */ ],
  "mirroredPaths": [],
  "effectiveParameters": []
}
```

## Rules

- **Discriminator**: `isDevRepo` ⇔ `outcome = "devRepoInit"`. A scaffold (provider) document is
  `isDevRepo = false`; its `outcome` is a `ScaffoldOutcome` value (`providerSucceeded`, …).
- **Producer**: written by the `init` **command** only, appended to `initEffects` at the dispatch
  site — never inside the shared `initEffects` seam that `scaffold` and `upgrade` reuse (FR-006).
- **Reconciliation** (`Drift.compute`): a dev-repo record flows through the existing `Some record`
  branch. The artifact axis is the seeded set (`Drift.expectedArtifactPaths`); the CLI axis is
  `coherentByAbsence` (no minimum); the template re-pin is `noTarget`. The reported
  `ProviderName` is `None` for a dev-repo (FR-005) — never the empty string.
- **Determinism**: byte-identical across identical runs (FR-007); no clock, no absolute path,
  `producedPaths` sorted by path.
- **Back-compat (parse)**: a document with empty provider fields and the `devRepoInit` outcome
  parses under the unchanged v1 grammar; records written before this feature still parse.
- **Versioning**: additive-optional ⇒ schema stays v1, **minor** package bump. No handoff
  `contractVersion`, so only the package/registry coherence checklist applies (FR-008).

## Anchoring note (why `producedPaths` ≠ reconciled set source)

`doctor`/`upgrade` derive the artifact axis from `Drift.expectedArtifactPaths` (the seeded
manifest), NOT from `record.ProducedPaths`. The dev-repo record's `producedPaths` therefore
*mirror* that set for a truthful self-describing manifest; they are consumed only by the provider
product-skill union, which excludes the `fs-gg-sdd-*` namespace — so listing the seeded process
skills there is inert for drift. The provenance file itself is the anchor and is deliberately
**absent** from `Drift.expectedArtifactPaths`.

## Fixtures / tests that must exist (golden/byte-stable)

- `tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs` — dev-repo `serialize`/`tryParse`
  round-trip, `isDevRepo` true/false, empty-pin + `devRepoInit` shape, byte-identity.
- `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs` — `init` writes a parseable dev-repo
  document over the seeded skeleton (owner `sdd`); byte-identical across runs.
- `tests/FS.GG.SDD.Commands.Tests/RemediationCommandTests.fs` (`DriftTests`) — a dev-repo record
  engages reconciliation with `ProviderName = None`, `coherentByAbsence`, `NoTarget` re-pin,
  coherent when fully seeded; re-seeds a missing seed without inventing a provider.
- `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` — the two init-vs-scaffold skeleton
  comparisons updated to exclude the intentionally-different provenance anchor.
- `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` — +`devRepoRecord`, +`isDevRepo`.
