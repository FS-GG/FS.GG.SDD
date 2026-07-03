# Contract: Build/CI hygiene surfaces (feature 064)

The externally-observable contracts this feature establishes. Each is verifiable
without reading implementation. "CI" = GitHub Actions on `ubuntu-latest`.

## C1 — Hermetic restore (FR-001..003)

- `nuget.config` MUST contain a `<clear/>` under `<packageSources>` and an explicit
  `<add>` for every restore-time source. (Amended: NO `<packageSourceMapping>` — it
  breaks `dotnet tool install --add-source`; see FR-001 amendment.)
- On any machine (including one with unrelated inherited NuGet sources),
  `dotnet restore FS.GG.SDD.sln` against a clean checkout MUST leave every
  `packages.lock.json` unmodified (`git diff --exit-code '**/packages.lock.json'`).
- `dotnet restore FS.GG.SDD.sln --locked-mode` MUST exit 0.
- All 11 lockfiles MUST list one resolved `FSharp.Core` version and content hash.

## C2 — Lockfile-keyed CI caching (FR-004/005)

- Every `actions/setup-dotnet@v4` step in `gate.yml`, `release.yml`, and
  `composition-acceptance.yml` MUST set `cache: true` and
  `cache-dependency-path: '**/packages.lock.json'`.
- The locked-mode restore MUST still fail on graph drift / NU1603 (caching is
  speed-only). A second run of an unchanged ref MUST report a cache hit.

## C3 — Format gate (FR-006..008)

- A repo-root `.editorconfig` MUST exist with `[*.fs]`/`[*.fsi]` Fantomas settings.
- CI MUST run `fantomas --check` over the tracked tree (pinned Fantomas version,
  installed **without** editing `.config/dotnet-tools.json`).
- A non-fantomas-clean tree MUST fail the gate, and the failure output MUST name
  the reformat command (`fantomas <paths>`).
- After reformatting the tree once, the full suite MUST be green and every golden
  baseline byte-identical (layout-only).

## C4 — Warning ratchet (FR-009/010)

- `Directory.Build.local.props` MUST promote a wider set than `FS3261;FS0025`,
  appended to `$(WarningsAsErrors)` (canonical `NU1603;NU1608` preserved), OR set
  `TreatWarningsAsErrors=true` — chosen so the current tree builds clean.
- A newly-introduced warning of a promoted class MUST fail the build.
- `Directory.Build.props`, `Directory.Packages.props`, and `.config/dotnet-tools.json`
  MUST be byte-identical to `FS-GG/.github` (the `build-config-drift` gate stays green).

## C5 — Locked-restore composite action (FR-011)

- `.github/actions/locked-restore/action.yml` MUST exist as a `composite` action
  with input `target` (default `FS.GG.SDD.sln`).
- It MUST run `dotnet restore <target> --locked-mode` and, on failure, emit the
  single canonical `::error::` message naming `--force-evaluate` before exiting
  non-zero.
- All five jobs (`gate.yml` `gate`; `release.yml` `contracts-tests`, `cli-tests`,
  `publish-contracts`, `publish-cli`) MUST consume it, each passing its existing
  restore target. No inline `Restore (locked)` shell block may remain.

## C6 — Release tool smoke + RollForward (FR-012/013)

- `release.yml` MUST run `scripts/verify-cli-tool.sh` against the packed CLI tool
  **before** any `dotnet nuget push`; a non-zero smoke result MUST block the push.
- `FS.GG.SDD.Cli.fsproj` MUST declare a `<RollForward>` policy.

## C7 — Global invariant (FR-014)

- No `fsgg-sdd` CLI output, JSON automation contract, persisted schema, or golden
  baseline may change. `fsgg-sdd validate` MUST remain `overallPassed`.
- `git diff` on any `readiness/**`, `*.json` golden, or `.fsi` baseline attributable
  to this feature MUST be empty (the reformat's `.fsi` touches are layout-only and
  MUST NOT alter any declaration the surface baseline records).
