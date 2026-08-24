# Contract: `release.yml` three-package publish workflow

The external interface is a CI/release-engineering contract. It extends the feature-039
single-package producer to publish three independently consumable packages in one run:
`FS.GG.Contracts`, `FS.GG.SDD.Artifacts`, and the `FS.GG.SDD.Cli` (`fsgg-sdd`) tool. This document
is authoritative; `.github/workflows/release.yml` is its implementation.

## Triggers and version resolution

The workflow runs for published releases, pushed `v*` tags, and manual dispatch. Its optional
`version` input remains Contracts-scoped. An empty manual input is a pack-only dry run; a non-empty
input enables publishing and overrides only `contracts_version`.

`resolve-versions` evaluates all three project `<Version>` properties with MSBuild and outputs
`contracts_version`, `artifacts_version`, `cli_version`, and `push`. Artifacts and CLI are one
coherent product line and MUST have equal, non-empty versions. On a release or tag event, a
version-bearing tag MUST match at least one of the three evaluated versions. Every package is then
packed at its own resolved version.

## Jobs and gates

| Job | Gate and responsibility |
|-----|-------------------------|
| `resolve-versions` | Canonical-repository guard; resolve three versions and publish intent. |
| `contracts-tests` | Locked restore and Release tests for `FS.GG.Contracts`. |
| `artifacts-tests` | Locked restore, Release tests, and clean-package-consumer proof for `FS.GG.SDD.Artifacts`. |
| `cli-tests` | Locked restore and Release tests for `FS.GG.SDD.Cli`. |
| `publish-contracts` | Needs resolver + Contracts tests; pack and publish Contracts. |
| `publish-artifacts` | Needs resolver + Artifacts tests; pack and publish Artifacts. |
| `publish-cli` | Needs resolver + CLI tests; pack, self-containment smoke, and publish CLI. |

Every job is guarded to `FS-GG/FS.GG.SDD`; fork events cannot reach a publish path. Top-level
permissions are `contents: read`. Each publish job alone adds `packages: write` and
`id-token: write`.

## Pack and dual-feed publish

Each publish job performs a locked restore, packs one explicit project exactly once, and verifies
that the expected package exists. When `push == true`, it pushes that exact `.nupkg` first to
`https://nuget.pkg.github.com/FS-GG/index.json` with the run-scoped `GITHUB_TOKEN`, then pushes the
same bytes to `https://api.nuget.org/v3/index.json` using a short-lived key minted by
`NuGet/login@v1` through OIDC. Both pushes use `--skip-duplicate`; there is no repack between feeds.
Any non-duplicate failure fails the run.

The Artifacts package glob in both push steps MUST be
`artifacts/packages/FS.GG.SDD.Artifacts.*.nupkg`. It may never target the CLI package. Before the
Artifacts publish job can run, `artifacts-tests` MUST execute
`tests/fixtures/typed-specifications/run-clean-consumer.sh`, proving that the package works without
a source-tree project reference.

The CLI package MUST contain its full runtime closure. Its publish job installs the just-packed
tool from the local package directory and runs the standalone validation smoke before either push.

## Conformance checks

- **C1** — Manual dispatch without `version` runs seven jobs, packs all three packages, and pushes none.
- **C2** — A version-bearing event tag matching none of the three lines fails; a match publishes all three at their resolved versions.
- **C3** — Re-running an already published version succeeds through `--skip-duplicate`.
- **C4** — A failing package test gate prevents its corresponding publish job.
- **C5** — Both feeds list `fs.gg.contracts`, `fs.gg.sdd.artifacts`, and `fs.gg.sdd.cli` at the published versions.
- **C6** — The just-packed CLI passes its isolated install-and-run smoke.
- **C7** — The just-packed Artifacts package passes the clean-consumer fixture.
- **C8** — Static contract tests require two exact Artifacts pushes and reject a CLI glob inside `publish-artifacts`.
