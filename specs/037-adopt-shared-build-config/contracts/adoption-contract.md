# Contract: Shared-Build-Config Adoption

The machine contract this feature consumes. SDD does **not** define a new contract;
it adopts the org `shared-build-config` + `lockfile-restore-enforcement` contracts
(ADR-0006, `.github#19`). The "interfaces" below are the sync-tool CLI, the MSBuild
import seam, and the drift-gate exit-code contract.

## 1. Sync tool CLI (`FS-GG/.github` `scripts/sync-build-config.sh`)

Manages exactly these files, relative to the consumer repo root:

```
Directory.Build.props
Directory.Packages.props
.config/dotnet-tools.json
```

| Invocation | Behavior | Exit |
|---|---|---|
| `sync-build-config.sh --adopt <repo>` | For each managed `*.props` that exists and lacks the marker `Source of truth: FS-GG/.github`, move it to `*.local.props`; then copy all canonical files in. Idempotent (skips the move if `*.local.props` already exists). | 0 ok / 1 on error |
| `sync-build-config.sh <repo>` | Re-sync: copy canonical files in. Refuses to overwrite a hand-authored marker-less `*.props` (tells you to run `--adopt`). | 0 ok / 1 refusal |
| `sync-build-config.sh --check <repo>` | **Drift gate.** Diff each managed file against canonical. Print `ok:`/`DRIFT (missing\|differs):` per file. | **0 in sync / 1 on any drift** |

The marker string `Source of truth: FS-GG/.github` is how the tool distinguishes a
canonical synced file from a hand-authored one — every canonical file carries it;
`*.local.props` files do not.

## 2. MSBuild import seam (last-write-wins)

- `Directory.Build.props` ends with
  `<Import Project="Directory.Build.local.props" Condition="Exists(...)" />`.
- `Directory.Packages.props` ends with
  `<Import Project="Directory.Packages.local.props" Condition="Exists(...)" />`.
- Import is **last**, so a `local.props` property of the same name **overrides** the
  org default. Additive properties (`WarningsAsErrors`) must be **appended** via
  `$(WarningsAsErrors)` in `local.props`, not assigned.
- `*.local.props` are optional (`Condition="Exists(...)"`) — a repo with no
  overrides still builds.

## 3. Drift-gate exit-code contract (FR-007 / SC-001 / SC-006)

| State | `--check` output | Exit |
|---|---|---|
| All three managed files byte-identical to canonical | `ok: <file>` ×3, `Done (check).` | **0** |
| A managed file edited locally | `DRIFT (differs): <file>` + remediation line | **non-zero** |
| A managed file missing | `DRIFT (missing): <file>` | **non-zero** |

CI consumes this exit code directly. The job checks out `FS-GG/.github` and runs
`--check` against the SDD workspace (no org reusable workflow exists yet).

## 4. Central Package Management duplicate rule (FR-004 / SC-003)

A package pinned in the canonical org baseline (`Directory.Packages.props`,
currently `FSharp.Core 10.1.301`) MUST NOT be re-declared in
`Directory.Packages.local.props`. Violation → CPM `NU1504`/`NU1011` at restore.

## 5. Locked-restore gate (FR-005 — unchanged behavior)

```xml
<RestoreLockedMode Condition="'$(GITHUB_ACTIONS)' == 'true' And Exists('$(MSBuildProjectDirectory)/packages.lock.json')">true</RestoreLockedMode>
```

Owned by the canonical `Directory.Build.props`. The condition is **identical** to
SDD's pre-adoption gate, so CI restores in locked mode and a fresh local clone (no
lockfile yet, or `GITHUB_ACTIONS` unset) is never blocked.

## 6. Non-goals / prohibitions (FR-008)

- No new `fsgg-sdd` lifecycle artifact, CLI behavior, or SDD-owned schema.
- No rendering-, template-, or Governance-specific package ID, template, path, or
  docs URL added to SDD build config.
- The canonical files are never edited locally; all SDD specifics live in
  `*.local.props`.
