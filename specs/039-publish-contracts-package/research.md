# Phase 0 Research: Publish FS.GG.Contracts to the org feed on release

Reference implementation: **FS-GG/FS.GG.Rendering#15** (merged) — the `publish-packages`
job in `.github/workflows/release.yml`. SDD mirrors its gating, version-normalization,
and `--skip-duplicate` push, diverging only where SDD's facts differ (single package,
an independently-versioned package, and a landed coherence gate).

## Decision 1 — Version source: the Contracts fsproj `<Version>`, not the SDD product tag

**Decision**: The published `FS.GG.Contracts` version is the package's own declared
`<Version>` line (currently `1.0.0`), read from the project at the release commit. The
release/tag (or a manual run) acts only as the **gate + trigger**; a manual run MAY
override with an explicit `version` input, and omitting it is a pack-only dry run.

**Rationale**: SDD carries **two independent version lines**:
- the SDD product line — `Directory.Build.local.props` `<Version>0.2.0</Version>`,
  inherited by the `fsgg-sdd` CLI and every `FS.GG.SDD.*` package;
- the org-shared contract line — `src/FS.GG.Contracts/FS.GG.Contracts.fsproj`
  overrides to `<Version>1.0.0</Version>`.

The landed org contract-coherence gate (**FS-GG/.github#18**,
`scripts/validate-registry.py`) asserts that the registry's declared `fsgg-contracts`
version **equals the actual `FS.GG.Contracts` package version read from this repo** and
fails on drift. Therefore the only value that keeps FR-008 / SC-003 three-way agreement
(*feed == fsproj == registry*) coherent by construction is the fsproj `<Version>`.
Stamping an SDD product release tag (`v0.2.0`) onto Contracts would publish the package
as `0.2.0`, contradicting the declared/registry `1.0.0` and failing #18.

**Reading the effective version**: `dotnet msbuild <fsproj> -getProperty:Version`
(MSBuild ≥17.8, present in the net10 SDK) returns the *evaluated* value `1.0.0`,
correctly resolving the project-level override of the `Directory.Build.local.props`
default. Grepping the `.fsproj` text is rejected — it would miss the import precedence.

**Alternatives considered**:
- *Contracts-scoped tag grammar* (`contracts-v1.0.0` drives the version): honours FR-002
  literally but adds a second release ritual and only stays coherent if the maintainer's
  tag matches the fsproj line — discipline the fsproj-as-source model makes unnecessary.
- *Single SDD `v*` tag drives the version* (literal sibling mirror): re-couples the two
  version lines and fails #18 whenever the SDD tag ≠ the contracts line. Rejected.
- *(chosen)* fsproj `<Version>` as source — drift-proof against #18; the maintainer bumps
  exactly one number (the fsproj line) to release a new contracts version.

## Decision 2 — Reconciling FR-002 ("derive from the release tag") + loud-fail edge cases

**Decision**: Treat FR-002's "release-derived version" as satisfied by the fsproj
`<Version>` (the version the release actually ships), and add a **coherence guard**: on a
release/tag event, if the triggering tag is version-bearing (`v<semver>` after stripping a
leading `v`) and its version differs from the fsproj `<Version>`, **fail loudly** — the tag
and the package line have drifted. A non-version-bearing tag is acceptable (the fsproj is
the authority) but the guard still fires on a *mismatched* version-bearing tag.

**Rationale**: Directly serves the spec edge cases ("Release with no version-bearing tag /
malformed tag … must fail loudly rather than publish under an empty or wrong version") and
FR-008 drift protection, while keeping the fsproj as the single source of truth. The
workflow enforces three-way agreement itself; org #18 is the cross-repo backstop.

## Decision 3 — Dry run vs. loud failure (Principle VIII: user input ≠ defect)

**Decision**:
- `workflow_dispatch` **with** a non-empty `version` input → push that exact version.
- `workflow_dispatch` **without** a `version` input → **pack-only dry run**, `push=false`,
  nothing pushed (FR-003). This is the *only* path where an absent version is benign.
- `release: published` / `push: tags v*` → always a real publish at the fsproj version;
  an empty/unreadable version here is a **defect → fail the run** (never a silent dry run).

**Rationale**: The sibling collapses every empty version into a dry run. SDD's spec
requires distinguishing an intentional manual dry run (benign user input) from a release
event that cannot produce a version (a defect that must fail). This is Principle VIII
(distinguish malformed input from tool defect; optional paths degrade explicitly, critical
paths fail fast).

## Decision 4 — Single-package pack scope

**Decision**: Pack the explicit project — `dotnet pack
src/FS.GG.Contracts/FS.GG.Contracts.fsproj` — not the solution.

**Rationale**: SDD's solution contains the CLI and the `FS.GG.SDD.*` libraries on the
`0.2.0` line; only `FS.GG.Contracts` is in scope here ("single-package scope" assumption).
Packing the explicit project is precise and drift-proof: a future project flipped to
`IsPackable` cannot accidentally ride this release. `IsPackable=true` is already set on the
contracts project (feature 036); this feature does not re-scope packability.

## Decision 5 — Gating, canonical-only, least-privilege credentials

**Decision**: Two jobs in a new `.github/workflows/release.yml`:
1. `contracts-tests` — `dotnet test tests/FS.GG.Contracts.Tests/FS.GG.Contracts.Tests.fsproj
   -c Release` (FR-005);
2. `publish` — `needs: [contracts-tests]`, runs only if tests passed.

Both gate on `if: github.repository == 'FS-GG/FS.GG.SDD'` so forks never publish (FR-006).
Top-level `permissions: contents: read`; the publish job adds `packages: write` and pushes
with `--api-key ${{ secrets.GITHUB_TOKEN }}` — the run-scoped token is sufficient for
same-org push, no personal access token (FR-007). Mirrors the sibling exactly.

## Decision 6 — Idempotency and "packed nothing" safety

**Decision**: Push with `dotnet nuget push "artifacts/packages/*.nupkg" --source
https://nuget.pkg.github.com/FS-GG/index.json --skip-duplicate` (FR-004 idempotent
re-run). After pack, assert at least one `.nupkg` exists before treating the run as a
success (FR-009 — "tests pass but pack produced no package" must fail, not silently
succeed). `set -euo pipefail` throughout; a failed push fails the run (FR-009).

## Decision 7 — Deterministic, locked restore consistent with the gate

**Decision**: Restore in locked mode once, then pack/test `--no-restore`, mirroring
`gate.yml`'s single-restore discipline. `src/FS.GG.Contracts/packages.lock.json` and the
test project's lockfile exist; `RestoreLockedMode` auto-enables under `GITHUB_ACTIONS`, so
an explicit `--locked-mode` restore both documents intent and fails loudly on graph drift.

**Rationale**: Keeps the release path's dependency graph as deterministic as the inner-loop
gate; this is release-time only and changes no offline/golden contract (spec assumption).

## Decision 8 — Registry coherence record (FR-011) lands cross-repo

**Decision**: The `fsgg-contracts` coherence note in **FS-GG/.github**
`registry/dependencies.yml` is updated *after* the first real publish, via the cross-repo
coordination protocol — outside this repository's product code. This repo owns only the
producer path that makes the package real; SC-006's declared-vs-feed check is the org #18
gate, which passes once the feed serves `1.0.0`.

## Resolved unknowns

- **Tag grammar**: leading `v` stripped, consistent with the sibling; but the tag is a
  coherence *check* against the fsproj version, not the version *source* (Decisions 1–2).
- **Canonical repo string**: `FS-GG/FS.GG.SDD`.
- **Feed**: `https://nuget.pkg.github.com/FS-GG/index.json`; read side already provisioned
  and verified (epic #16 / #21 / #22) — out of scope here.
- **No existing release workflow / no existing tags**: confirmed (`git tag` empty; only
  `gate.yml` + `composition-acceptance.yml` present) — this workflow is net-new.
