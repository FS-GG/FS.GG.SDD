# Contract: FS.GG.Contracts version coherence & bump protocol

**Status**: process contract (release-engineering). No `.fsgg` schema, no F# surface, no
contract-version change. This documents the invariant the new checklist projects and the
protocol the publish + registry-sync steps obey. It composes with — and does not replace —
feature 039's `release-workflow.md` (the trigger/version-resolution contract) and the
`contract-coherence` gate owned by `FS-GG/.github`.

## The three-authority coherence invariant

For `FS.GG.Contracts`, four values must agree when the system is coherent:

```
fsproj <Version>  ==  Fsgg.ContractVersion.value          (source: enforced by 042/036)
registry.version  ==  fsproj <Version>                     (enforced by contract-coherence gate)
registry.package-version  ==  newest version on the org feed
COHERENT  ⇔  source == feed(newest) == registry.version == registry.package-version
```

- **source** — `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` `<Version>` (authority).
- **feed** — newest `FS.GG.Contracts` on `nuget.pkg.github.com/FS-GG`.
- **registry.version / registry.package-version** — `FS-GG/.github`
  `registry/dependencies.yml` under `fsgg-contracts` (cross-repo owned).

## Current vs target (this feature)

| Value | Current | Target |
|---|---|---|
| source (`<Version>` / `ContractVersion.value`) | `1.1.0` | `1.1.0` (unchanged) |
| feed (newest) | `1.0.1` | `1.1.0` |
| registry.version | `1.1.0` | `1.1.0` (unchanged) |
| registry.package-version | `1.0.1` | `1.1.0` |

## Publish protocol (this feature, US1 → US2)

1. **Pre-flight** (optional): `workflow_dispatch` `release.yml` with **no** version input →
   dry run, packs `FS.GG.Contracts.1.1.0.nupkg`, pushes nothing.
2. **Publish**: `workflow_dispatch` `release.yml` with `version=1.1.0` → contracts tests gate,
   pack, `nuget push --skip-duplicate` to the org feed. Idempotent if already present.
3. **Verify feed**: query the org packages API; `1.1.0` is listed (not 404).
4. **Advance registry** (cross-repo, ordered strictly after step 3): notify FS-GG/.github#42 (or
   successor) to set `fsgg-contracts.package-version: 1.1.0` and refresh the compatibility
   projection. **Never** advance `package-version` before step 3 confirms the feed (FR-007).

## Durable bump protocol (FR-005 — what the checklist encodes)

Any **future** `FS.GG.Contracts` source `<Version>` bump MUST, in the same change set:

1. **Bump source** — fsproj `<Version>` and `Fsgg.ContractVersion.value` together.
2. **Publish to feed** — land the new version on the org feed (via `release.yml`).
3. **Update the `.github` registry** — advance `fsgg-contracts.version` (so the
   `contract-coherence` gate stays green) and, once the feed confirms, `package-version`.

Per **ADR-0001**, a `FS.GG.Contracts` version bump must update the `.github` registry in the
same coordinated change; the `contract-coherence` gate enforces `registry.version == source`.
The 042 gap (source bumped, feed + registry not) is exactly the failure this protocol prevents.

## Conformance

- **C-coherent**: after this feature, all four values equal `1.1.0` (data-model "Target" row).
- **C-gate-green**: the `contract-coherence` gate passes on `.github` PRs and `main`.
- **C-checklist-present**: `docs/release/contracts-version-bump-checklist.md` exists and names
  the three same-change actions, citing the gate and ADR-0001.
- **C-no-surface-change**: no `.fsgg` schema, F# surface, contract version, CLI byte, or 042
  golden/fixture changes (SC-005).
