# Implementation Plan: Publish FS.GG.Contracts to the org package feed on release

**Branch**: `039-publish-contracts-package` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/039-publish-contracts-package/spec.md`

## Summary

Add the **producer half** of the cross-repo auto-update fabric for `FS.GG.Contracts`: a
net-new `.github/workflows/release.yml` that, on a release of FS.GG.SDD, gates on the
`FS.GG.Contracts` tests, packs the single contracts package, and pushes it to the org
GitHub Packages feed (`nuget.pkg.github.com/FS-GG`) with `--skip-duplicate`, using the
run-scoped `GITHUB_TOKEN` (`packages: write`, no PAT), only from the canonical repo.

The one material divergence from the merged rendering sibling (FS-GG/FS.GG.Rendering#15):
SDD has **two independent version lines** — the SDD product line (`0.2.0`) and the
org-shared `FS.GG.Contracts` line (`1.0.0`) — and a landed org coherence gate
(FS-GG/.github#18) pins the registry/feed version to the **Contracts fsproj `<Version>`**.
So the published version is the **fsproj `<Version>`** (manual run may override; omit =
dry run), not the SDD product release tag; the tag is a coherence check, not the source.
This keeps FR-008 / SC-003 three-way agreement (*feed == fsproj == registry*) coherent by
construction. No `.fsgg` schema, contract surface, contract version, or CLI behavior
changes (FR-010); this is release-time only and touches no offline/golden contract.

## Technical Context

**Language/Version**: GitHub Actions YAML workflow; invokes the .NET SDK `10.0.x`. Product
code (F# / `net10.0`) is unchanged.

**Primary Dependencies**: `dotnet restore/pack/nuget push`, `dotnet msbuild -getProperty`,
`actions/checkout@v4`, `actions/setup-dotnet@v4`. Reference: FS-GG/FS.GG.Rendering#15
`release.yml` `publish-packages` job.

**Storage**: N/A — transient `artifacts/packages/*.nupkg` on the runner.

**Testing**: `tests/FS.GG.Contracts.Tests/FS.GG.Contracts.Tests.fsproj` gates the publish
(FR-005). No new F# tests (no product behavior change); verification is the workflow's own
dry run + a real feed query — see `quickstart.md`.

**Target Platform**: `ubuntu-latest` GitHub-hosted runner; canonical repo `FS-GG/FS.GG.SDD`.

**Project Type**: CI / release-engineering — one net-new workflow file.

**Performance Goals**: N/A (release-cadence, not inner-loop).

**Constraints**: Release-time only; does not run in the default offline inner loop and
changes no deterministic CLI/golden contract (spec assumption). Least-privilege creds; no
PAT. Locked restore consistent with `gate.yml`.

**Scale/Scope**: One workflow file (`.github/workflows/release.yml`), one package
(`FS.GG.Contracts`). Cross-repo registry note (FR-011) handled in FS-GG/.github.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: Release-engineering change with **no** product contract/schema/CLI surface
change (FR-010) → **Tier 2** per *Change Classification*. It is **not** Tier 1: the spec is
explicit that it "does not add or change a versioned cross-repo contract." The cross-repo
`fsgg-contracts` coherence-record update (FR-011) is a follow-on record-keeping step in
FS-GG/.github, handled via the cross-repo coordination protocol (ADR), not a contract change
in this repo.

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec→FSI→Tests→Impl | N/A | No non-trivial F# change; the artifact is a YAML workflow. |
| II. Structured artifacts are the machine contract | **PASS** | The workflow + version-resolution protocol are documented as a contract in `contracts/release-workflow.md`; behavior is not folklore. |
| III. Visibility in `.fsi` | N/A | No F# public surface added/changed. |
| IV. Idiomatic simplicity | **PASS** | Plain YAML + `bash`; mirrors existing `gate.yml`/`composition-acceptance.yml`. No clever machinery. |
| V. Elmish/MVU is the boundary for stateful/I-O | **PASS (justified)** | The only I/O lives in GitHub Actions, not SDD F# product code. A release CI workflow is not a lifecycle command/generator/validator (`nextLifecycleCommand` unaffected); sibling workflows are likewise plain YAML. No MVU ceremony is owed. |
| VI. Test evidence is mandatory | **PASS (justified)** | No product behavior changes, so no new F# tests are owed. The YAML CI path is verified by its own `workflow_dispatch` dry run (push=false) and a post-merge feed query (`quickstart.md` C0–C6), matching the merged sibling which added no F# test. Gating reuses the existing `FS.GG.Contracts.Tests`. |
| VII. Agent & human share one contract | N/A | No agent command/skill surface change. |
| VIII. Observability & safe failure | **PASS** | Central to the design: loud fail on tag/fsproj version mismatch and on unreadable version (defect); explicit dry-run notice for an intentional no-version manual run (user input); fail on "packed nothing" and on push failure; `--skip-duplicate` for idempotency. Distinguishes malformed/absent input from tool defect. |

**Engineering Constraints**: `net10.0` unchanged; the `FS.GG.Contracts` namespace exception
is already sanctioned (constitution v1.1.0) and unaffected; no rendering/Governance/provider
identity is added to generic SDD. **PASS.**

**Result**: No violations. Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/039-publish-contracts-package/
├── plan.md              # This file
├── research.md          # Phase 0 — 8 decisions (version source, gating, safety)
├── data-model.md        # Phase 1 — CI entities + version-resolution state machine
├── quickstart.md        # Phase 1 — C0–C6 verification (dry run, release, feed query)
├── contracts/
│   └── release-workflow.md   # Phase 1 — the workflow/trigger/version-resolution contract
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
.github/workflows/
├── gate.yml                    # existing — per-PR locked restore + build (unchanged)
├── composition-acceptance.yml  # existing — nightly network-gated acceptance (unchanged)
└── release.yml                 # NEW — contracts-tests gate + publish job

src/FS.GG.Contracts/
└── FS.GG.Contracts.fsproj      # unchanged — IsPackable=true, <Version>1.0.0 (the version source)

tests/FS.GG.Contracts.Tests/
└── FS.GG.Contracts.Tests.fsproj  # unchanged — gates the publish (FR-005)
```

**Structure Decision**: Single net-new workflow file at `.github/workflows/release.yml`
beside the two existing workflows. No `src/` or `tests/` change — the contracts project is
already packable (feature 036) and already has tests. The producer path is entirely
release-engineering CI; product code is untouched.

## Complexity Tracking

> No constitution violations — no entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
