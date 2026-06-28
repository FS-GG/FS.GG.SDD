# Contract: `release.yml` publish workflow

The external interface this feature exposes is a **CI/release-engineering contract**, not an
F# API or `.fsgg` schema. It is the trigger + version-resolution + gating protocol that
maintainers, the org feed, Renovate, and the org coherence gate (#18) rely on. This document
is the authoritative description; the YAML is its implementation.

File: `.github/workflows/release.yml` (net-new).

## Triggers

```yaml
on:
  release:
    types: [published]
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      version:
        description: "Explicit version to pack+publish. Omit for a pack-only dry run."
        type: string
        required: false
```

**Dual-trigger note**: a single tagged release fires **both** `release: published` and
`push: tags v*`. This is intentional and benign: the second run is a no-op via
`--skip-duplicate` (FR-004), and the `concurrency` group **serializes** the paired runs
(`cancel-in-progress: false`, keyed on the version/tag) so they never push simultaneously and
no push is cancelled mid-flight. Either trigger alone (a release without a pushed tag, or a
tag pushed without a GitHub release) still publishes exactly once.

## Jobs and gating contract

| Job | Runs when | Contract |
|-----|-----------|----------|
| `contracts-tests` | `github.repository == 'FS-GG/FS.GG.SDD'` | restore (locked) + `dotnet test tests/FS.GG.Contracts.Tests/FS.GG.Contracts.Tests.fsproj -c Release`. Gate for the publish (FR-005). |
| `publish` | same repo guard **and** `needs: [contracts-tests]` succeeded | resolve version → pack → (push if `push==true`). |

- Top-level `permissions: { contents: read }`.
- `publish` job adds `permissions: { contents: read, packages: write }`.
- Fork events never satisfy the repo guard ⇒ no publish (FR-006).

## Version-resolution contract (`publish` step `id: ver`, outputs `version`, `push`)

`version` source is the **effective Contracts fsproj `<Version>`**:
`dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version`.

| Event | `version` | `push` | Failure mode |
|-------|-----------|--------|--------------|
| `workflow_dispatch`, `inputs.version` non-empty | `strip-v(inputs.version)` | `true` | — |
| `workflow_dispatch`, `inputs.version` empty | `fsproj_version` | **`false`** | — (intentional dry run, FR-003) |
| `release: published` | `fsproj_version` | `true` | tag version-bearing & ≠ `fsproj_version` ⇒ **fail** (FR-002/FR-008); `fsproj_version` empty ⇒ **fail** (FR-009) |
| `push: tags v*` | `fsproj_version` | `true` | same guards as `release` |

`strip-v(x)` removes one leading `v`. The release-event tag is a **coherence check**
against `fsproj_version`, never the version source (see research Decisions 1–2).

## Pack + push contract

```
restore (locked, once) ─► dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj \
                            -c Release -p:Version=$VER --no-restore -o artifacts/packages
                       ─► assert ls artifacts/packages/*.nupkg is non-empty   (FR-009)
   if push == true     ─► dotnet nuget push "artifacts/packages/*.nupkg" \
                            --source https://nuget.pkg.github.com/FS-GG/index.json \
                            --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate
```

- Single explicit project (single-package scope, FR-001 / research Decision 4).
- `--skip-duplicate` ⇒ idempotent re-publish (FR-004).
- `${{ secrets.GITHUB_TOKEN }}` + `packages: write` ⇒ least-privilege, no PAT (FR-007).
- Any push failure (other than a skipped duplicate) fails the run (FR-009).
- `push == false` skips the push step entirely (dry run, FR-003).

## Out of scope / unchanged (FR-010)

No `.fsgg` schema, no contract surface, no contract version, no CLI behavior, no
offline/golden/deterministic inner-loop contract is touched. The cross-repo
`fsgg-contracts` registry coherence record (FR-011) is updated in **FS-GG/.github** after
the first publish, not here.

## Conformance checks (verification anchors)

- C1 — manual dry run (`workflow_dispatch`, no `version`) packs and pushes nothing.
- C2 — a `release: published` at a `v<fsproj_version>` tag publishes `fsproj_version`;
  a mismatched version-bearing tag fails loudly.
- C3 — re-running a published version completes and pushes no duplicate.
- C4 — fork event / failing `contracts-tests` ⇒ no push.
- C5 — feed query for `fs.gg.contracts` returns the published version (not 404).

See `quickstart.md` for the runnable form of each.
