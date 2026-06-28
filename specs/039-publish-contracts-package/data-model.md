# Phase 1 Data Model: Publish FS.GG.Contracts on release

This feature adds no `.fsgg` schema and no F# type. The "data" here is the small set of
CI-level entities and the version-resolution state machine the workflow implements. It is
documented so the workflow's behavior is a contract, not folklore.

## Entities

### Release trigger
The event that starts the workflow and decides whether a real push may happen.

| Field | Source | Notes |
|-------|--------|-------|
| `event_name` | GitHub | `release` \| `push` (tag) \| `workflow_dispatch` |
| `tag` | `github.event.release.tag_name` / `github.ref_name` | version-bearing tag, may carry a leading `v`; absent on manual runs |
| `version_input` | `inputs.version` | manual override only; empty ⇒ dry run |
| `repository` | `github.repository` | must equal `FS-GG/FS.GG.SDD` to publish |

### Package source version
The authoritative version line for the package.

| Field | Source | Value |
|-------|--------|-------|
| `fsproj_version` | `dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version` | evaluated effective `<Version>` (currently `1.0.0`) |

### Resolved publish plan
The output of the version-resolution step (see state machine).

| Field | Type | Meaning |
|-------|------|---------|
| `version` | semver string | the version stamped via `-p:Version` |
| `push` | bool | `true` ⇒ push to feed; `false` ⇒ pack-only dry run |

### FS.GG.Contracts package
The packable artifact. Identity: PackageId `FS.GG.Contracts` + `version`. Produced into
`artifacts/packages/*.nupkg`; the unit pushed to the feed.

### Org package feed
`https://nuget.pkg.github.com/FS-GG/index.json`. Shared org-scoped feed consumers and
Renovate resolve against. Read side already provisioned (out of scope); this feature adds
the write side for `FS.GG.Contracts`.

### `fsgg-contracts` registry record (cross-repo, FS-GG/.github)
`registry/dependencies.yml` entry: `version`/`package-version` = `1.0.0`, plus a coherence
note. Updated after the first publish (FR-011, outside this repo). The org #18 gate asserts
`declared == fsproj_version == feed`.

## Version-resolution state machine

```text
                         ┌──────────────────────────────────────────────┐
   event_name?           │ repository != FS-GG/FS.GG.SDD                 │
        │                │   → job does not run (fork no-op, FR-006)     │
        │                └──────────────────────────────────────────────┘
        ├── workflow_dispatch ──┐
        │                       ├── version_input non-empty → version = normalize(input); push = true
        │                       └── version_input empty      → version = fsproj_version;   push = FALSE   (dry run, FR-003)
        │
        └── release | push(tag) ──┐
                                  │  version = fsproj_version          (Decision 1)
                                  │  push    = true
                                  │  GUARD: tag is version-bearing AND normalize(tag) != fsproj_version
                                  │           → FAIL LOUD (drift, FR-002/FR-008, edge: malformed/mismatched tag)
                                  │  GUARD: fsproj_version empty/unreadable
                                  │           → FAIL LOUD (defect; never a silent dry run, FR-009)
                                  └──────────────────────────────────────────────────────────────────────
```

`normalize(x)` = strip a single leading `v` (`v1.0.0` → `1.0.0`), consistent with the
rendering sibling.

## Pack/push transitions and safety invariants

| Step | Guard | On violation |
|------|-------|--------------|
| restore (locked) | lockfile matches resolved graph | fail loud (graph drift) |
| test | `FS.GG.Contracts.Tests` pass | publish job skipped (FR-005) |
| pack | ≥1 `.nupkg` produced | fail loud (FR-009 — packed nothing) |
| push | `push == true` | dry run skips push (FR-003) |
| push | feed/credentials available; non-duplicate or `--skip-duplicate` | duplicate ⇒ skipped OK (FR-004); other failure ⇒ fail run (FR-009) |

## Invariants (map to success criteria)

- **I1** `feed.version == fsproj_version == registry.declared` (SC-003, FR-008) — held by
  construction (version source) + the release-event guard + the org #18 backstop.
- **I2** A run that does not land the package on the feed never reports success (SC-001,
  FR-009).
- **I3** Re-publishing an existing version is a no-op success (SC-004, FR-004).
- **I4** No publish from a fork or on failing tests (SC-005, FR-005/FR-006).
- **I5** No `.fsgg` schema, contract surface, contract version, or CLI behavior changes
  (FR-010) — this model introduces none.
