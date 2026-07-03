# Implementation Plan: Format gate

**Branch**: `065-format-gate` | **Date**: 2026-07-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/065-format-gate/spec.md`

## Summary

Close the last of the five feature-064 hygiene gaps: the repo has **no format
gate**. Add a repo-root `.editorconfig` that is simultaneously the editor config
and the Fantomas 6+ config (no `fantomas.json`), reformat the tracked F# tree
**once, layout-only**, and add a **non-required** `format` job to
`.github/workflows/gate.yml` that installs **pinned Fantomas `7.0.5`
out-of-manifest** and runs `fantomas --check .`, naming the `fantomas <paths>`
fix command on failure. The one-time reformat's safety is proven by the unchanged
full suite, byte-identical golden/`.fsi` baselines, and `fsgg-sdd validate`
staying `overallPassed`. This is exactly US3 + FR-006..008 of feature 064,
deferred from PR #84 (roadmap #85 §1) so the large reformat lands as its own
reviewable PR.

## Technical Context

**Language/Version**: F# / .NET `net10.0` (SDK `10.0.x`; local `10.0.301`).
Change surface is one repo-config file (`.editorconfig`), one GitHub Actions job
(`gate.yml`), one developer-doc update (`DEVELOPING.md`), plus a **layout-only
reformat** of the tracked `.fs`/`.fsi` tree.

**Primary Dependencies**: **Fantomas `7.0.5`** (pinned dotnet tool, installed in
CI via `--tool-path` — *not* added to the managed `.config/dotnet-tools.json`);
`actions/setup-dotnet@v4`; `dotnet test`/`dotnet run … validate` for the
non-behaviour evidence.

**Storage**: N/A — no schema, persisted artifact, or JSON contract change.

**Testing**: The existing full solution suite (green, unchanged) is the primary
evidence that the reformat is layout-only. The only genuinely new behaviour is
the gate's reject-path, covered by a **negative check** (mangle a file →
`fantomas --check` non-zero + fix hint). No new xUnit contract tests — no code
contract changes, and a golden/`.fsi` diff attributable to the reformat would be
a defect, not a test.

**Target Platform**: The repo's CI (GitHub Actions `ubuntu-latest`) and any
contributor machine, both pinning Fantomas `7.0.5`.

**Project Type**: Single multi-project F# solution (`FS.GG.SDD.sln`; 172 tracked
`.fs` + 50 `.fsi` = 222 tracked F# files).

**Performance/Constraints**:
- Golden/deterministic JSON baselines and `.fsi` public-surface baselines
  byte-identical after reformat (SC-002); `fsgg-sdd validate` stays
  `overallPassed`.
- `.config/dotnet-tools.json` (and the other managed org files) byte-identical to
  `FS-GG/.github` `dist/dotnet/` → the `build-config-drift` gate stays green
  (SC-003). **This forbids adding Fantomas to the managed manifest** — the
  load-bearing constraint (research Decision 2).
- The `format` job is **non-required** — a red format job never blocks merge
  (FR-005).

**Scale/Scope**: 1 new config file (`.editorconfig`), 1 new CI job, 1 doc
update, and a layout-only reformat that (pre-tuning) touched ~77% of the 222
tracked F# files — the tuned `.editorconfig` (research Decision 3) is expected to
shrink that.

## Change Classification

**Tier 2 (internal / tooling change).** No public API, schema, generated-view,
command, artifact-layout, or agent-skill contract changes. The reformat touches
source but is layout-only (no signature or behaviour change), so `.fsi` baselines
and every golden stay byte-identical. The format gate is build-time policy, not a
contract surface. Nothing here is Tier 1.

## Constitution Check

*GATE: must pass before Phase 0. Re-checked after Phase 1 (unchanged).*

- **I. Spec → FSI → Tests → Impl**: PASS. No new public surface; `.fsi` files
  change only by layout-preserving reformat (no declaration change). The one new
  behaviour (gate reject-path) has a negative check.
- **II. Structured Artifacts Are the Machine Contract**: PASS. No structured/JSON
  artifact changes; `.editorconfig` is authoring config, not a machine contract.
- **III. Visibility in `.fsi`**: PASS. Reformat preserves every signature
  declaration; surface baselines byte-identical (a baseline diff would be a
  defect).
- **IV. Idiomatic Simplicity**: PASS. Uses the stock Fantomas + `setup-dotnet`
  mechanisms and one non-required CI job; no bespoke machinery.
- **V. MVU boundary**: N/A — no runtime code paths added.
- **VI. Test Evidence Mandatory**: PASS. The gate reject-path has a
  failing-before/passing-after negative check; the reformat's non-behavioural
  claim is evidenced by the unchanged full suite + byte-identical goldens/`.fsi`
  (real fixtures, no mocks).
- **VII. Agent & Human Workflows Share One Contract**: PASS. CI and contributors
  run the identical pinned Fantomas `--check` — one contract, documented
  (FR-007).
- **VIII. Observability & Safe Failure**: PASS. The format gate fails fast with
  an actionable `fantomas <paths>` fix message; being non-required, it degrades
  to advisory signal rather than a hard block.

**Result**: No violations. Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/065-format-gate/
├── plan.md                        # This file
├── spec.md                        # Feature spec (US1/US2, FR-001..007, SC-001..005)
├── research.md                    # Phase 0 — Fantomas 7.0.5 pin, key-set + install decisions
├── data-model.md                  # Phase 1 — config/CI entities (no runtime model)
├── quickstart.md                  # Phase 1 — runnable validation checks
├── contracts/
│   └── format-gate-contract.md    # Phase 1 — FG-1..FG-6 behavioural contract
└── checklists/
    └── requirements.md            # Spec quality checklist (all pass)
```

### Source Code (repository root)

```text
.editorconfig                      # NEW — editor + Fantomas config ([*.fs]/[*.fsi] fsharp_* keys)
.github/workflows/gate.yml         # EDIT — add non-required `format` job (pinned Fantomas --check)
DEVELOPING.md                      # EDIT — document the pinned install/check/fix commands
src/**/*.fs, src/**/*.fsi          # REFORMAT (layout-only) — one-time fantomas pass
tests/**/*.fs, tests/**/*.fsi      # REFORMAT (layout-only) — one-time fantomas pass

# UNCHANGED (invariant — must stay byte-identical):
.config/dotnet-tools.json          # managed org file — Fantomas NOT added here
Directory.Build.props              # managed org file
Directory.Packages.props           # managed org file
**/golden/*.json, baselines        # deterministic/golden baselines — byte-identical after reformat
```

**Structure Decision**: Single-solution F# repo; this feature edits only
repo-root tooling config, one CI workflow, one developer doc, and applies a
layout-only reformat across the existing `src/`/`tests/` F# tree. No new projects,
modules, or source files are created. The managed org files and every golden/`.fsi`
baseline are hard invariants.

## Implementation sequencing (guidance for `/speckit-tasks`)

The reformat and the gate must land together but be provable independently:

1. **Author `.editorconfig`** with defaults + a minimal tuned `fsharp_*` set;
   measure `fantomas 7.0.5 --check .` churn and tune `fsharp_max_line_length` (and
   only clearly-justified further keys) to minimise the diff (research Decision 3).
2. **Apply the one-time reformat** (`fantomas .`) as its own commit; prove
   layout-only: full suite green, `git diff` shows zero golden `.json` changes and
   `.fsi` diffs are whitespace-only, `fsgg-sdd validate` still `overallPassed`
   (FG-5 / SC-002).
3. **Add the non-required `format` job** to `gate.yml` (out-of-manifest
   `--tool-path` install of `7.0.5` + `--allow-roll-forward`, `fantomas --check .`,
   fix-hint on failure) (FG-2/3/4).
4. **Verify the manifest invariant**: `.config/dotnet-tools.json` unchanged; the
   `build-config-drift` gate stays green (FG-2 / SC-003).
5. **Document** the pinned install/check/fix commands in `DEVELOPING.md` (FG-6 /
   FR-007).
6. **Negative check** the gate: mangle a file → non-zero + fix hint → restore
   (FG-3 / SC-001).

## Complexity Tracking

No constitution violations — section intentionally empty.
