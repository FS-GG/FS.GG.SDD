# Phase 1 Data Model: Adopt Shared Build Config

This feature has no runtime data model. The "entities" are build files and the
ownership / import-order relationships between them. This document is the
authoritative inventory for the SC-002 "nothing lost" check.

## Entities & ownership

| Entity | Files | Owner | Edited locally? | Synced / overwritten by `--check`/sync? |
|---|---|---|---|---|
| **Canonical build files** | `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json` | `FS-GG/.github` (org) | **No** (drift = build failure) | Yes — byte-identical to `dist/dotnet/` |
| **Local override files** | `Directory.Build.local.props`, `Directory.Packages.local.props` | SDD (repo) | **Yes** — this is where SDD settings live | **No** — sync never touches them |
| **Org baseline package set** | (inside canonical `Directory.Packages.props`) | org | No | Yes — currently only `FSharp.Core 10.1.301` |
| **Drift check** | `scripts/sync-build-config.sh --check` (lives upstream) | org | n/a | The enforcement gate (exit 0 = in sync, 1 = drift) |
| **Lockfiles** | `**/packages.lock.json` (per project) | SDD | Regenerated, then committed | Restored in locked mode under `GITHUB_ACTIONS` + exists |

## Import-order semantics (the override seam)

```text
Directory.Build.props      (canonical) ──Import (LAST)──▶ Directory.Build.local.props      (repo)
Directory.Packages.props   (canonical) ──Import (LAST)──▶ Directory.Packages.local.props   (repo)
```

- MSBuild is **last-write-wins**: because the canonical file imports `local.props`
  last, a repo property overrides an org default of the same name.
- **Exception — additive properties.** `WarningsAsErrors` is *appended* by the
  canonical file (`$(WarningsAsErrors);NU1603;NU1608`) before the import, so
  `local.props` must also **append** (`$(WarningsAsErrors);FS3261;FS0025`) rather than
  assign, or the canonical promotions are lost. (See research Decision 2.)
- **CPM duplicate rule.** A `PackageVersion` pinned in the canonical org baseline
  (`FSharp.Core`) MUST NOT be re-declared in `Directory.Packages.local.props` — CPM
  raises `NU1504`/`NU1011`.

## Property inventory — `Directory.Build.local.props` (SC-002 checklist)

| Property | Pre-adoption value | Disposition |
|---|---|---|
| `Version` | `0.2.0` | **MOVE** to local (single source of truth; FR-002) |
| `TargetFramework` | `net10.0` | **MOVE** to local |
| `LangVersion` | `preview` | **MOVE** to local |
| `ContinuousIntegrationBuild` | `true` | **MOVE** to local (not in canonical; pairs with the `GITHUB_ACTIONS` gate) |
| `Nullable` | `enable` | **MOVE** to local |
| `TreatWarningsAsErrors` | `false` | **MOVE** to local |
| `WarningsAsErrors` (FS) | `FS3261;FS0025` | **MOVE & APPEND**: `$(WarningsAsErrors);FS3261;FS0025` |
| `Company` | `FS.GG` | **MOVE** to local |
| `Authors` | `FS.GG` | **MOVE** to local |
| `Product` | `FS.GG.SDD` | **MOVE** to local |
| `RepositoryUrl` | `https://github.com/FS-GG/FS.GG.SDD` | **MOVE** to local |
| `PackageLicenseExpression` | `MIT` | **MOVE** to local |
| `PackageRequireLicenseAcceptance` | `false` | **MOVE** to local |
| `Deterministic` | `true` | **DROP** — owned by canonical |
| `ManagePackageVersionsCentrally` | `true` | **DROP** — owned by canonical (both files) |
| `RestorePackagesWithLockFile` | `true` | **DROP** — owned by canonical |
| `RestoreLockedMode` (gated) | `GITHUB_ACTIONS` + lockfile-exists | **DROP** — owned by canonical (identical condition) |
| `WarningsAsErrors` (NU) | `;NU1603;NU1608` | **DROP** — owned by canonical (re-applied there) |

## Package inventory — `Directory.Packages.local.props` (SC-002 checklist)

| Package | Pre-adoption version | Disposition |
|---|---|---|
| `FSharp.Core` | `10.1.301` | **DROP** — org baseline; resolves upstream (FR-004/SC-003) |
| `YamlDotNet` | `16.3.0` | **MOVE** to local |
| `System.Text.Json` | `10.0.0` | **MOVE** to local |
| `Spectre.Console` | `0.57.0` | **MOVE** to local |
| `xunit` | `2.9.3` | **MOVE** to local |
| `xunit.runner.visualstudio` | `3.1.5` | **MOVE** to local |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | **MOVE** to local |

`Directory.Packages.local.props` declares **only** the `<ItemGroup>` of the six
moved `PackageVersion` items — it must NOT re-set `ManagePackageVersionsCentrally`
(canonical owns it).

## Effective-equivalence invariant (FR-006 / SC-005)

For every project, the evaluated values of `{TargetFramework, Version, LangVersion,
Deterministic, ContinuousIntegrationBuild, Nullable, TreatWarningsAsErrors,
WarningsAsErrors (as a set), ManagePackageVersionsCentrally,
RestorePackagesWithLockFile, RestoreLockedMode (under the same conditions),
resolved package versions}` MUST equal the pre-adoption values. The only intended
*structural* additions are `CentralPackageTransitivePinningEnabled=true` (org) and
the unused `fake-cli` tool manifest — neither may change the resolved graph
(verified per research Decision 3).
