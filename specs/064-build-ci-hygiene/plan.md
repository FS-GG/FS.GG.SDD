# Implementation Plan: Build/CI hygiene — hermetic restore, caching, format & warning gates, de-duplicated locked-restore

**Branch**: `064-build-ci-hygiene` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/064-build-ci-hygiene/spec.md` (board item #74, review §5.2 / remediation #10)

## Summary

Harden the build/CI fabric along five independent tracks, ordered by value:

1. **Hermetic restore (P1, root cause)** — `nuget.config` gains `<clear/>`,
   explicit sources, and package source mapping so `dotnet restore` is
   machine-independent, killing the `FSharp.Core` `contentHash` local↔CI
   divergence and the lockfile-regeneration churn (FR-001..003).
2. **CI restore caching (P2)** — enable `actions/setup-dotnet@v4` NuGet caching
   keyed on the committed lockfiles across all three workflows (FR-004/005).
3. **Format gate (P2)** — add `.editorconfig` (which, for Fantomas 6+, *is* the
   fantomas configuration) and a CI job that fails a non-fantomas-clean tree;
   reformat the existing tree layout-only (FR-006..008).
4. **Wider warning ratchet (P2)** — widen the promoted-warning set in the
   repo-owned `Directory.Build.local.props` to the maximal set the current tree
   already satisfies, never touching the managed canonical props (FR-009/010).
5. **De-duplicated locked-restore + release smoke (P3)** — extract the 5×
   `Restore (locked)` block into one local composite action, wire
   `scripts/verify-cli-tool.sh` as a release gate, and set `RollForward` on the
   packed tool (FR-011..013).

Hard invariant across all five: **no `fsgg-sdd` CLI output, JSON contract,
persisted schema, or golden baseline changes** (FR-014), and no managed org file
changes (FR-010).

## Technical Context

**Language/Version**: F# / .NET `net10.0`. Change surface is repo config
(`nuget.config`, `Directory.Build.local.props`, `.editorconfig`), GitHub Actions
YAML, and a shell composite action — plus a layout-only reformat of existing
`.fs`/`.fsi` source.

**Primary Dependencies**: `dotnet restore/build/test/pack`; `actions/setup-dotnet@v4`
(cache support); **Fantomas** (pinned dotnet tool, installed in CI — *not* added to
the managed `.config/dotnet-tools.json`); NuGet package source mapping.

**Storage**: N/A — no schema or persisted-artifact change.

**Testing**: The existing full solution suite (green, unchanged) plus new
verification that is mostly *infrastructural assertion*: a mis-formatted-source
negative check for the format gate, a warning-ratchet negative check, and the
release CLI-tool smoke (`verify-cli-tool.sh`, already offline-runnable). No new
xUnit contract tests are required because no code contract changes; the
reformat's safety is proven by the unchanged suite + byte-identical goldens.

**Target Platform**: The repo's build + CI (GitHub Actions, `ubuntu-latest`) and
any contributor machine.

**Project Type**: Single multi-project F# solution (`FS.GG.SDD.sln`, 5 src + 6 test
projects, 11 committed lockfiles).

**Performance/Constraints**:
- JSON/golden byte-identical (FR-014); `fsgg-sdd validate` stays `overallPassed`.
- Managed org files byte-identical → the `build-config-drift` gate stays green
  (FR-010). **This forbids adding Fantomas to `.config/dotnet-tools.json`.**
- Restore must stay fork-friendly (no token-required restore-time source).

**Scale/Scope**: ~4 config files edited (`nuget.config`, `Directory.Build.local.props`,
+ new `.editorconfig`), 3 workflows edited, 1 new composite action, 1 CLI fsproj
prop, potentially many `.fs`/`.fsi` files touched by the layout-only reformat.

## Change Classification

**Tier 2 (internal / tooling change).** No public API, schema, generated-view,
command, artifact-layout, or agent-skill contract changes. The reformat touches
source but is layout-only (no signature or behaviour change), so `.fsi` baselines
and every golden stay byte-identical. The warning ratchet and format gate are
build-time policy, not a contract surface. Nothing here is Tier 1.

## Constitution Check

*GATE: must pass before Phase 0. Re-checked after Phase 1 (unchanged).*

- **I. Spec → FSI → Tests → Impl**: PASS. No new public surface; `.fsi` files change
  only by layout-preserving reformat (no declaration change). Verification (format
  negative check, ratchet negative check, tool smoke) accompanies the change.
- **II. Structured Artifacts Are the Machine Contract**: PASS. No structured/JSON
  artifact changes; lockfiles become *more* deterministic, not different in intent.
- **III. Visibility in `.fsi`**: PASS. Reformat preserves every signature
  declaration; surface baselines unchanged (a baseline diff would be a defect).
- **IV. Idiomatic Simplicity**: PASS. Uses stock NuGet/`setup-dotnet`/Fantomas
  mechanisms and one small composite action; no bespoke machinery.
- **V. MVU boundary**: N/A — no runtime code paths added.
- **VI. Test Evidence Mandatory**: PASS. Each new gate has a failing-before/
  passing-after negative check; the reformat's non-behavioural claim is evidenced
  by the unchanged full suite + byte-identical goldens (real fixtures, no mocks).
- **VII. Agent & Human Workflows Share One Contract**: PASS. CI and contributors
  run the identical hermetic restore, format, and ratchet — one contract.
- **VIII. Observability & Safe Failure**: PASS. The format and ratchet gates fail
  fast with an actionable "run X to fix" message; caching degrades to a cold
  restore, never a wrong graph; fork PRs degrade cleanly.

**Result**: No violations. Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/064-build-ci-hygiene/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/ci-hygiene-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit-tasks)
```

### Source & config touched

```text
nuget.config                                   # + <clear/>, explicit sources, packageSourceMapping   (FR-001)
.editorconfig                                  # NEW — editor + Fantomas 6 config (fsharp_* keys)      (FR-006)
Directory.Build.local.props                    # widen WarningsAsErrors (repo-owned only)              (FR-009)
src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj         # + <RollForward>Major</RollForward>                    (FR-013)
.github/actions/locked-restore/action.yml      # NEW — single composite locked-restore action          (FR-011)
.github/workflows/gate.yml                      # use composite; + NuGet cache; + format job            (FR-004/007/011)
.github/workflows/release.yml                   # use composite; + NuGet cache; + verify-cli-tool gate  (FR-004/011/012)
.github/workflows/composition-acceptance.yml    # + NuGet cache                                          (FR-004)
**/*.fs, **/*.fsi                               # layout-only Fantomas reformat (as needed)             (FR-008)
```

**Structure Decision**: Edit config/CI in place; add exactly two new files
(`.editorconfig`, the composite action). Each of the five tracks is independently
landable and independently testable (matching the spec's P1..P3 user-story
slicing) — the tasks graph will order them P1→P3 but they share no code.

## Key Design Decisions (see research.md for full rationale)

- **Fantomas is installed as a pinned global/local tool in CI, never added to the
  managed `.config/dotnet-tools.json`** — that manifest is drift-checked
  byte-identical to `FS-GG/.github`, so editing it fails the `build-config-drift`
  gate (FR-010). Fantomas 6+ reads its settings from `.editorconfig`, so no
  separate `fantomas.json` exists; the spec's "fantomas config" *is* the
  `[*.fs]`/`[*.fsi]` section of `.editorconfig`.
- **The widened warning set is measured, not guessed.** Implementation runs a
  clean build, enumerates the residual warning classes, and promotes the maximal
  set that is already at zero (appended to `$(WarningsAsErrors)` so the canonical
  `NU1603;NU1608` and local `FS3261;FS0025` are preserved). If the tree is fully
  clean after a trivial, behaviour-neutral fix, prefer flipping
  `TreatWarningsAsErrors=true`; otherwise keep the explicit widened list. No
  warning-fixing campaign (FR-009 / spec Assumptions).
- **The format gate is a new, non-required `format` job** (parallel to `gate`),
  running `fantomas --check` over the tracked tree and printing the exact
  `fantomas <paths>` fix command — the same enforcement-at-workflow-level model
  the repo already uses (no branch protection).
- **The composite action parameterises only the restore target** (solution vs a
  single project); the `--locked-mode` enforcement, the NU1603 error text, and the
  `--force-evaluate` regenerate hint live in that one place.
- **The release CLI-tool smoke runs before the publish push**, gating it: a
  non-self-contained pack fails the release loud (FR-012).

## Complexity Tracking

> No constitution violations — intentionally empty.

## Out of Scope

- Any warning-fixing campaign (only the already-clean set is ratcheted).
- Adding Fantomas (or any tool) to the managed `.config/dotnet-tools.json`.
- An org-level reusable restore workflow in `FS-GG/.github` — this consolidation is
  repo-local (a local composite action is sufficient; spec Assumptions).
- Any `fsgg-sdd` behaviour, JSON, schema, or golden change (FR-014).
