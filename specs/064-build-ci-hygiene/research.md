# Research: Build/CI hygiene (feature 064)

Resolves the deferred-to-planning values from the spec's Assumptions and the
technical unknowns from the plan.

## Decision 1 — Hermetic `nuget.config`: `<clear/>` + explicit sources + source mapping

**Decision**: Rewrite `nuget.config` to (a) `<clear/>` inherited `packageSources`
and (b) declare exactly two sources — `nuget.org`
(`https://api.nuget.org/v3/index.json`) and the existing repo-local
`fsgg-local` (`./.fsgg-local-feed`).

**IMPLEMENTATION AMENDMENT 1 (source mapping removed)**: package source mapping was
initially added but **removed** — NuGet rejects `dotnet tool install --add-source` when
any source mapping is active ("--add-source cannot be combined with package source
mapping"), which broke the release tool-smoke (`scripts/verify-cli-tool.sh`) and would
break local-feed scaffold/consumer flows. So `<clear/>` + explicit sources is the design.

**IMPLEMENTATION AMENDMENT 2 (lockfiles NOT reconciled — CI-corrected)**: the lockfiles
are **left unchanged** at the canonical `FwQFuqOA1+...`. An initial pass mistook this
dev-container's `excLf2zM/...` for the authentic nuget.org hash (its `.nupkg.metadata`
*claims* `source: nuget.org`, but the container's baked `~/.nuget` global cache/config
serves a non-nuget.org FSharp.Core under that label). CI — a clean, authoritative env —
**rejected** `excLf2zM/...` with NU1403, and historically produced `FwQFuqOA1+...`, which
is therefore the canonical value. Corrective action: revert lockfiles to origin/main and
keep `<clear/>`. Consequence: `--locked-mode` and SC-001/002 are verifiable **in CI**, not
on this polluted dev-container (where restore still resolves the baked `excLf2zM/...`).
`<clear/>` remains correct hygiene — it makes a *clean* contributor machine with an
inherited feed resolve nuget.org's canonical package instead of a divergent one.

**Rationale**: The recorded `FSharp.Core` `contentHash` local↔CI divergence is the
classic symptom of a non-hermetic restore: without `<clear/>`, NuGet unions the
repo config with per-machine/global sources, so the *same* package version can be
served from different feeds with different repackaged content hashes, and the
committed lockfile hash then depends on whose machine restored it. `<clear/>` +
explicit sources makes the source set identical everywhere; source mapping
additionally pins *which* feed each id may come from, so a package can never be
silently satisfied from an unexpected source. This is the standard, documented
NuGet remedy for lockfile-hash nondeterminism.

**Alternatives considered**:
- *Only `<clear/>`, no mapping*: fixes the union but still lets any id resolve from
  either declared source; mapping is cheap insurance and makes the intent explicit.
- *Pin `FSharp.Core` version harder*: the version already agrees (10.1.301); the
  problem is the *hash/source*, not the version — version pinning wouldn't fix it.
- *Restore-time org GitHub Packages source*: rejected — it needs a token and would
  break fork restore; org feed and nuget.org Trusted Publishing stay **push-time**
  targets only (release.yml), never restore-time sources.

**Validation**: SC-001 (0 modified lockfiles on a foreign-source machine) + SC-002
(`--locked-mode` succeeds; all 11 lockfiles agree on one FSharp.Core hash). If the
divergence survives `<clear/>` + mapping, pin the residual cause during
implementation (Assumptions) — the criterion is the observable outcome.

## Decision 2 — NuGet caching via `setup-dotnet` keyed on lockfiles

**Decision**: On every `actions/setup-dotnet@v4` step across `gate.yml`,
`release.yml`, and `composition-acceptance.yml`, set `cache: true` and
`cache-dependency-path: '**/packages.lock.json'`.

**Rationale**: The committed lockfiles are the exact, complete pin of the restore
graph — a perfect cache key. `setup-dotnet`'s built-in cache keys the global
packages folder on the hash of the dependency-path glob, so an unchanged lockfile
set is a cache hit and any real dependency change busts the cache into a fresh cold
restore. Because restore stays `--locked-mode` (Decision 1 + existing gate),
caching changes only speed, never the enforced graph (FR-005).

**Alternatives considered**:
- *Manual `actions/cache` on `~/.nuget/packages`*: more YAML, same effect; the
  built-in `setup-dotnet` cache is the idiomatic minimal path.
- *No cache-dependency-path (default `**/packages.lock.json` when
  RestorePackagesWithLockFile)*: `setup-dotnet` requires an explicit
  `cache-dependency-path` when `cache: true`; set it explicitly for clarity.

**Validation**: SC-003 — 100% of `setup-dotnet` steps enable lockfile-keyed
caching; a second unchanged run is a cache hit.

## Decision 3 — Format gate: `.editorconfig` as the Fantomas config, pinned tool in CI

**Decision**: Add a repo-root `.editorconfig` carrying general whitespace rules and
a `[*.fs]`/`[*.fsi]` section with the Fantomas `fsharp_*` settings (house style).
Add a **non-required** `format` job to `gate.yml` that installs a **pinned**
Fantomas version (`dotnet tool install`/`update` to a `--tool-path` or global, NOT
via `.config/dotnet-tools.json`) and runs `fantomas --check .`, failing with the
exact `fantomas <paths>` fix command. Reformat the existing tree once
(layout-only) so it is clean.

**Rationale**: Fantomas 6+ reads *all* of its configuration from `.editorconfig`
(there is no `fantomas.json`), so one file is both the editor config (FR-006) and
the fantomas config. The critical constraint: `.config/dotnet-tools.json` is a
**managed org file** the `build-config-drift` gate pins byte-identical to
`FS-GG/.github` — adding Fantomas there would fail that gate (FR-010). Installing a
pinned Fantomas in the CI job (and documenting the same command for contributors)
keeps the managed manifest untouched while still pinning the formatter version for
determinism.

**Alternatives considered**:
- *Add Fantomas to `.config/dotnet-tools.json`*: **rejected** — breaks the managed
  drift gate (FR-010). This is the load-bearing constraint of this decision.
- *A second nested tool manifest*: possible, but `dotnet tool restore` resolution
  and the drift gate's expectations make an explicit pinned install cleaner and
  less surprising.
- *MSBuild-integrated format check (FSharpLint/analyzer)*: heavier, changes build
  behaviour, and risks interacting with the widened ratchet; a standalone check job
  keeps formatting orthogonal to compilation.

**Validation**: SC-004 — the gate fails a mis-formatted PR, passes on the
reformatted tree, and the full suite stays green with 0 golden changes (the
reformat is layout-only).

**Risk / mitigation**: A large one-time reformat diff. Mitigation — land it as its
own reviewable commit, and prove non-behaviour by the unchanged suite + goldens;
tune `fsharp_max_line_length` and a few `fsharp_*` keys to minimise churn where the
existing house style is deliberate (spec Assumptions).

## Decision 4 — Widened warning ratchet: measured maximal already-clean set

**Decision**: During implementation, run a clean `dotnet build FS.GG.SDD.sln`
capturing all warnings, enumerate the residual warning IDs, and in
`Directory.Build.local.props` append to `$(WarningsAsErrors)` the maximal set of
warning classes that are **already at zero** (on top of the existing
`FS3261;FS0025` and canonical `NU1603;NU1608`). If the residual count can be driven
to zero with a trivial, behaviour-neutral edit, prefer flipping
`TreatWarningsAsErrors=true` in the local props instead of an explicit list.

**Rationale**: The review notes the tree is "at 2 warnings", so the clean state is
nearly total — cheap to lock in. Enumerating the already-clean classes (rather than
guessing IDs) guarantees the current build stays green (FR-009) while still failing
any *new* occurrence of a promoted class. Full `TreatWarningsAsErrors=true` is the
strongest ratchet and is preferred *iff* the tree is (or trivially becomes) clean;
otherwise the explicit widened list avoids a warning-fixing campaign (out of scope).

**Alternatives considered**:
- *Guess a fixed ID list now*: brittle — a wrong guess either under-ratchets or
  reddens the build. Deferring to the measured set is more robust and is pinned by
  SC-005.
- *Flip full TWAE unconditionally*: rejected unless the tree is clean — it would
  otherwise force out-of-scope code changes.

**Validation**: SC-005 — current tree builds clean under the widened ratchet; a
newly-promoted warning class fails the build; `Directory.Build.props` unchanged.

## Decision 5 — Locked-restore composite action + release smoke + RollForward

**Decision**: Create `.github/actions/locked-restore/action.yml` (a `composite`
action) with one input, `target` (default `FS.GG.SDD.sln`), running the
`--locked-mode` restore with the single canonical NU1603/`--force-evaluate` error
message. Replace the five inline `Restore (locked)` steps
(`gate.yml` `gate`; `release.yml` `contracts-tests`, `cli-tests`,
`publish-contracts`, `publish-cli`) with `uses: ./.github/actions/locked-restore`
passing each job's existing target scope. In `release.yml`, add a step that runs
`scripts/verify-cli-tool.sh` on the CLI before the publish push so a
non-self-contained pack blocks the release. Add
`<RollForward>Major</RollForward>` to `FS.GG.SDD.Cli.fsproj`.

**Rationale**: One definition removes the 5× duplication (and the already-divergent
copy) so the error text and regenerate hint can never drift again; parameterising
only `target` preserves each job's restore scope (solution for `gate`, single
project for the four release jobs) exactly. Wiring `verify-cli-tool.sh` (already an
offline, self-contained smoke) as a release gate closes the "publish an unverified
tool" gap (FR-012). `RollForward=Major` lets the installed tool run on a newer
major runtime if the exact `net10.0` runtime is absent — standard global-tool
resilience (FR-013).

**Alternatives considered**:
- *Reusable workflow (`workflow_call`)*: heavier than needed for a single restore
  step within the same repo; a composite action is the minimal fit.
- *`RollForward=LatestMajor`/`Minor`*: `Major` is the conventional tool default
  (roll forward to the next available major); the exact policy is confirmed against
  the tool's runtime needs during implementation.

**Validation**: SC-006 — locked-restore exists in exactly 1 place consumed by all 5
jobs; release runs the smoke before publish; the tool declares `RollForward`.

## Cross-cutting invariant

Every decision is constrained by **FR-014 (no `fsgg-sdd` output/JSON/schema/golden
change)** and **FR-010 (no managed org file change)**. The reformat (Decision 3) is
the only one that touches source, and it is layout-only — proven by the unchanged
full suite and byte-identical goldens, and by `fsgg-sdd validate` staying
`overallPassed` (SC-007).
