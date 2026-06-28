# Quickstart / Verification: Publish FS.GG.Contracts on release

Prove the producer path end-to-end. There is no in-repo unit test for the YAML workflow
(Principle VI note in plan.md); verification is the workflow's own dry run plus a real feed
query, mirroring the merged rendering sibling (FS-GG/FS.GG.Rendering#15).

## Prerequisites

- `.github/workflows/release.yml` present on the canonical repo `FS-GG/FS.GG.SDD`.
- Read access to the org feed for the final query (a `read:packages` token; provisioned
  per epic #16 / #21 / #22).
- `dotnet` SDK `10.0.x` locally for the offline pack/version checks.

## C0 — Local: confirm the effective package version (no push)

```bash
dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version
# expect: 1.0.0  (the project override of the 0.2.0 SDD product line)

dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release -o /tmp/fsgg-pack
ls -1 /tmp/fsgg-pack/*.nupkg
# expect: FS.GG.Contracts.1.0.0.nupkg  (single package, single-package scope)
```

## C1 — Manual dry run (pack-only, pushes nothing) — FR-003 / SC contract C1

Trigger `release` workflow via `workflow_dispatch` with **no** `version` input
(GitHub UI → Actions → release → Run workflow, leave version blank), or:

```bash
gh workflow run release.yml --repo FS-GG/FS.GG.SDD
```

Expected: the `publish` job logs a dry-run notice, lists `Packed:` with one `.nupkg`, and
**skips** the push step. A subsequent feed query is unchanged.

## C2 — Real publish from a release — FR-001/FR-002 / SC-001

1. Ensure the fsproj `<Version>` is the version to ship (bump it to release a new version).
2. Cut a release tagged to match, e.g. `v1.0.0`:

```bash
gh release create v1.0.0 --repo FS-GG/FS.GG.SDD --title v1.0.0 --notes "FS.GG.Contracts 1.0.0"
```

Expected: `contracts-tests` passes, then `publish` resolves `version=1.0.0`, packs, and
pushes to `https://nuget.pkg.github.com/FS-GG/index.json`.

**Mismatch guard (C2 negative)**: a release tagged `v2.0.0` while the fsproj is `1.0.0`
must **fail loudly** in the version-resolution step (drift), pushing nothing.

## C3 — Idempotent re-run — FR-004 / SC-004

Re-run the same release (or `workflow_dispatch` with `version: 1.0.0`) after `1.0.0` is on
the feed. Expected: the run **succeeds**; `dotnet nuget push --skip-duplicate` reports the
duplicate skipped; no error, no second copy.

## C4 — Fork / red-build no-op — FR-005/FR-006 / SC-005

- On any fork, a release/tag/dispatch event must **not** run the jobs (`if:
  github.repository == 'FS-GG/FS.GG.SDD'`). Confirm the run is skipped on a fork.
- If `FS.GG.Contracts.Tests` fail, `publish` (which `needs` them) never runs — confirm the
  push step is never reached.

## C5 — Consumer restore from the org feed alone — SC-002 / SC-006

```bash
# Feed query: the published version is listed, not 404.
curl -s -u USER:TOKEN \
  https://nuget.pkg.github.com/FS-GG/download/fs.gg.contracts/index.json
# expect JSON listing "1.0.0"

# Clean-environment restore against the org feed only (no local/dev feed):
mkdir /tmp/consume && cd /tmp/consume
dotnet new classlib -lang F# -n Consume && cd Consume
dotnet nuget add source https://nuget.pkg.github.com/FS-GG/index.json -n fsgg \
  -u USER -p TOKEN --store-password-in-clear-text
dotnet add package FS.GG.Contracts --version 1.0.0
dotnet restore
# expect: restore succeeds from the org feed alone
```

## C6 — Registry coherence (cross-repo, FR-011 / SC-006)

After C2/C5 land `1.0.0` on the feed, the org contract-coherence gate (FS-GG/.github#18)
asserting `declared fsgg-contracts == actual FS.GG.Contracts version == feed` passes, and
the `fsgg-contracts` registry note is updated to describe a real published package (handled
in FS-GG/.github via the cross-repo coordination protocol — not in this repo).
