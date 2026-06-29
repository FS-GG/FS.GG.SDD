# Phase 1 Data Model: Accept 4-Segment Versions in the Registry Validator

This feature adds no new type. It widens the **accepted vocabulary** of an existing field and changes a
**version-coherence state**. Both are modeled below; the F# types in `Registry.fsi` are unchanged.

## Entity: Version string (the accepted vocabulary)

A contract's `version` (and optional `package-version`) value in the registry, validated by
`isValidVersion` in `Fsgg.Registry`. The accepted language is the **union** of two regexes
(unchanged: a value matching *either* is accepted):

| Class | Grammar (regex) | Examples | Status |
|---|---|---|---|
| Bare integer schema version | `^\d+$` | `1`, `2` | accepted (unchanged) |
| 3-segment SemVer (± pre/build) | `^\d+\.\d+\.\d+(-…)?(\+…)?$` | `1.0.0`, `0.1.52-preview.1` | accepted (unchanged) |
| **4-segment numeric (± pre/build)** | `^\d+\.\d+\.\d+(\.\d+)?(-…)?(\+…)?$` | `1.2.1.1`, `1.2.1.1-preview.1` | **newly accepted** |

The change is the single optional `(\.\d+)?` group folded into the SemVer regex (the two rows for
SemVer are one regex; the 4th segment is optional). The accepted set strictly **grows** — every value
accepted before is still accepted.

**Boundary (still rejected — the widening admits one numeric segment and nothing else):**

| Input | Why rejected |
|---|---|
| `1.2.x.4` | 4th-ish segment is non-numeric (`x`) |
| `abc` | not a version |
| `1.2.3.4.5` | five numeric segments (the optional group admits one extra, not unbounded) |
| `??` (range) | the `range` field uses the separate, unchanged `rangeRegex` |

The `range` field grammar (`1.x`, comparator sets) is **out of scope and unchanged**.

### Validation rules (where the vocabulary is enforced)

- `validateDocument` → `version` (`Registry.fs:311`): blank ⇒ `MissingField "version"`; else
  `not (isValidVersion …)` ⇒ `MalformedVersion`.
- `validateDocument` → `package-version` (`Registry.fs:346`): present and `not (isValidVersion …)` ⇒
  `MalformedVersion`.
- A diagnostic-free run over the whole document ⇒ `Valid`. Diagnostics are emitted deterministically in
  document order (root → repos → contracts → dependencies → coherence).

No state transitions: the validator is a pure total function `RegistryDocument -> ValidationResult`.

## Entity: `governance-reference-gate-set` contract (the concrete case)

The registry contract legitimately versioned `1.2.1.1` (ADR-0007: `{gov}.{caps}.{policy}.{tooling}`
schema versions; the real `FS.GG.Governance.ReferenceGateSet` package version). Before this change it
produces two `MalformedVersion` diagnostics (on `version` and `package-version`); after, none. The local
fixture `tests/fixtures/registry/dependencies.yml` is refreshed to mirror the canonical `FS-GG/.github`
registry and carry this contract so the end-to-end check is real (research Decision 4).

## State: Version-bump coherence (the publish side)

The publish obligation is a coherence state across version authorities. The same-change invariant from
`docs/release/contracts-version-bump-checklist.md`:

```text
FS.GG.Contracts:  source(fsproj <Version> == ContractVersion.value) == feed(newest) == registry.version == registry.package-version
```

This feature transitions the Contracts authorities `1.1.0 → 1.1.1` and the SDD product line `0.2.0 → 0.2.1`:

| Authority | Location | Before | After |
|---|---|---|---|
| Contracts source `<Version>` | `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` | `1.1.0` | `1.1.1` |
| Contracts source constant | `src/FS.GG.Contracts/ContractVersion.fs` (`value`, `patch`) | `1.1.0` / `0` | `1.1.1` / `1` |
| Contracts feed (newest) | org GitHub Packages | `1.1.0` | `1.1.1` (publish) |
| Contracts registry `version`/`package-version` | `FS-GG/.github` `registry/dependencies.yml` `fsgg-contracts` | `1.1.0` | `1.1.1` (cross-repo, after feed) |
| SDD product line `<Version>` | `Directory.Build.local.props` | `0.2.0` | `0.2.1` |
| SDD line projections | `docs/release/release-readiness.json` (`identity.version`, `generatorVersion.version`), `docs/release/versioning-policy.md` | `0.2.0` | `0.2.1` |
| `fsgg-sdd` tool feed | org GitHub Packages | `0.2.0` | `0.2.1` (publish) |

**Ordering invariant** (bump checklist step 3): the `.github` registry `package-version` advance for
`fsgg-contracts` must follow feed confirmation that `1.1.1` is live — `package-version` must never run
ahead of the feed.

## Coherence-id outcome (cross-repo, downstream)

`registry-validator-typed` coherence id flips from `coherent: false` toward `coherent: true` once
FS-GG/.github#49 pins the published `0.2.1` tool in the `contract-coherence` gate and confirms the typed
CLI and `validate-registry.py` agree on the canonical file. That flip is the downstream consumer step,
tracked separately; this feature's contribution is the published, parity-restored validator.
