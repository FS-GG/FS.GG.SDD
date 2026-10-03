# Contract: `release.yml` four-package publish workflow

The external interface is a CI/release-engineering contract. It extends the feature-039
single-package producer to publish four independently consumable packages in one run:
`FS.GG.Contracts`, `FS.GG.SDD.Artifacts`, the `FS.GG.SDD.Cli` (`fsgg-sdd`) tool, and
`FS.GG.SDD.Knowledge`. This document
is authoritative; `.github/workflows/release.yml` is its implementation.

## Triggers and version resolution

The workflow runs for published releases, pushed `v*` tags, and manual dispatch. Its optional
`version` input remains Contracts-scoped. An empty manual input is the only package-candidate build:
it runs every package gate, packs the coherent SDD set once, writes a commit/version/inventory/hash
manifest, and retains that exact artifact without publishing. A non-empty input or tag/release event
enables publication and overrides only `contracts_version`, but it MUST locate a unique successful
no-push candidate run at the exact event commit; it never rebuilds the SDD archives.

`resolve-versions` evaluates all four project `<Version>` properties with MSBuild and outputs
`contracts_version`, `artifacts_version`, `cli_version`, `knowledge_version`, and `push`. Artifacts, CLI, and Knowledge are one
coherent product line and MUST have equal, non-empty versions. On a release or tag event, a
version-bearing tag MUST match at least one of the four evaluated versions. The retained SDD set uses the coherent resolved version; Contracts keeps its independent version.

## Jobs and gates

| Job | Gate and responsibility |
|-----|-------------------------|
| `resolve-versions` | Canonical-repository guard; resolve the independent Contracts and coherent SDD versions and publish intent. |
| `contracts-tests` | Locked restore and Release tests for `FS.GG.Contracts`. |
| `artifacts-tests` | Locked restore, Release tests, and clean-package-consumer proof for `FS.GG.SDD.Artifacts`. |
| `cli-tests` | Locked restore and Release tests for `FS.GG.SDD.Cli`. |
| `knowledge-tests` | Locked restore and Release tests for `FS.GG.SDD.Knowledge`. |
| `publish-contracts` | Needs resolver + Contracts tests; pack and publish Contracts. |
| `publish-artifacts` | No-push dispatch only: needs resolver + Artifacts/CLI/Knowledge tests; pack all three coherent members once, verify/install them, write the identity/hash manifest, and retain the only publishable bytes. |
| `locate-artifacts` | Publish events only: find exactly one successful, unexpired no-push artifact at the exact release commit; zero or multiple candidates fail closed. |
| `publish-cli` | Download the retained run artifact by run id, re-verify commit/version/inventory/hashes, then publish the same three files to both feeds and read them back. |

Every job is guarded to `FS-GG/FS.GG.SDD`; fork events cannot reach a publish path. Top-level
permissions are `contents: read`. Each publish job alone adds `packages: write` and
`id-token: write`.

## Pack and dual-feed publish

`publish-contracts` retains its independently governed pack path. For the coherent SDD set, only the
no-push `publish-artifacts` job packs. It uploads exactly three nupkgs with `candidate.env` and
`pre-push.sha256` in an immutable Actions artifact named for the source commit. A later publishing
run locates exactly one successful, unexpired workflow-dispatch artifact for its own `GITHUB_SHA`,
downloads it by source run id, and verifies the manifest, exact filenames/versions, nuspec source
commit, and SHA-256 values before any feed credential is used. It never invokes `dotnet pack`.

When `push == true`, the publisher pushes each retained `.nupkg` first to
`https://nuget.pkg.github.com/FS-GG/index.json` with the run-scoped `GITHUB_TOKEN`, then pushes the
same bytes to `https://api.nuget.org/v3/index.json` using a short-lived key minted by
`NuGet/login@v1` through OIDC. Both pushes use `--skip-duplicate`; there is no repack between feeds.
Any non-duplicate failure fails the run.

Whole nupkg SHA-256 is a custody identity, not a reproducible-build claim. Two independent packs may
contain byte-identical payload entries while differing as ZIP containers. The retained manifest
authorizes one three-member archive set; substituting a fresh pack—even with equal extracted payloads—must fail
hash verification.

The Artifacts job must not pass a global `Version` or `PackageVersion` override. The resolver has
already proved that the source-evaluated Artifacts, CLI, and Knowledge versions are equal, so pack consumes that
source identity directly. Both command-line properties propagate into NuGet's separate
project-reference version evaluation and would incorrectly replace the independently versioned
`FS.GG.Contracts` dependency. With no override, the Artifacts nuspec retains both the source package
version and the producer-declared Contracts dependency (`7.5.2` for this release). A real-pack
metadata test and the static workflow contract guard this boundary.

The Artifacts package glob in both push steps MUST be
`artifacts/packages/FS.GG.SDD.Artifacts.*.nupkg`. It may never target the CLI package. Before the
Artifacts publish job can run, `artifacts-tests` MUST execute
`tests/fixtures/typed-specifications/run-clean-consumer.sh`, proving that the package works without
a source-tree project reference.

The CLI package MUST contain its full runtime closure. Its publish job installs the just-packed
tool from the local package directory and runs the standalone validation smoke before either push.

## Conformance checks

- **C1** — Manual dispatch without `version` runs all gates, packs the coherent SDD set once, retains its manifest-bound artifact, and pushes none.
- **C2** — A version-bearing event tag matching neither the independent Contracts nor coherent SDD line fails; a match publishes all four at their resolved versions.
- **C3** — Re-running an already published version succeeds only when its non-signature payloads match the retained candidate, then uses `--skip-duplicate`.
- **C4** — A failing package test gate prevents its corresponding publish job.
- **C5** — Both feeds list `fs.gg.contracts`, `fs.gg.sdd.artifacts`, `fs.gg.sdd.cli`, and `fs.gg.sdd.knowledge` at the published versions.
- **C6** — The just-packed CLI passes its isolated install-and-run smoke.
- **C7** — The just-packed Artifacts package passes the clean-consumer fixture.
- **C8** — Static contract tests require two exact Artifacts pushes and reject a CLI glob inside `publish-artifacts`.
- **C9** — Packing Artifacts from the source-resolved coherent line writes package version 2.1.0 while retaining an exact `FS.GG.Contracts` dependency of `7.5.2`; adding either a `Version` or `PackageVersion` override fails tests.
- **C10** — A publish event with zero, multiple, expired, wrong-head, wrong-version, wrong-inventory, or hash-mismatched candidate artifacts fails before feed credentials; one exact candidate is downloaded by source run id and no SDD pack command exists in the publishing path.
- **C11** — Back-to-back equal-payload/different-container packages have different hashes, and substituting the second archive into the first candidate handoff fails verification.

The reviewed `scripts/sdd-release-packages.txt` inventory governs coherent packing. Before retention,
actual native credentials must establish unused versions on both feeds; inaccessible reads refuse.
Before promotion, occupied members are allowed only when their extracted non-signature payloads
match the retained archives exactly. The packed APIs are compared with public 2.0.3, and the
installed CLI and NuGet-resolved Knowledge SDK must pass the same qualifier locally and after public
readback. The new Knowledge package has no historical API baseline and is not classified as compared.
