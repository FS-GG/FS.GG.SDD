# Implementation Plan: Surface skillDriftPaths + correct stale report/doc surfaces

**Branch**: `063-doctor-drift-and-doc-currency` (stacked on `062-split-command-reports`) | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/063-doctor-drift-and-doc-currency/spec.md`

## Summary

Fix the feature-058 drift-visibility bug and correct a cluster of stale
report/doc surfaces flagged by the 2026-07-02 review:

1. **Render `skillDriftPaths` (the bug)** — add the drift lines to
   `CommandRendering.renderText` for both the `doctor` and `upgrade` blocks,
   mirroring `missingArtifacts`. Because the rich renderer builds its details table
   *from* `renderText`'s output (`Cli/Rendering.fs:117`), this single edit surfaces
   drift in **both** text and rich (FR-001/002/003); JSON is untouched (FR-004).
2. **Complete the `unknownCommand` correction** to all 18 accepted commands
   (16 lifecycle + `validate` + `registry`), pinned by a test (FR-005).
3. **Add `.agents/skills`** to the reseed `NextAction` affected paths (FR-006).
4. **Document `projectRoot = "."`** as intentional determinism — comment only, no
   behaviour change (FR-007, reclassified false positive).
5. **Refresh docs** — README, quickstart, index, DEVELOPING, release.yml
   (FR-008..011).

## Technical Context

**Language/Version**: F# / .NET `net10.0`; Markdown/YAML for docs.

**Primary Dependencies**: `System.Text.StringBuilder` (text projection),
Spectre.Console (rich, unchanged — it consumes the text facts).

**Storage**: N/A — no schema or persisted-artifact change.

**Testing**: xUnit — `RemediationProjectionTests` (doctor/upgrade projections),
`DiagnosticTests`/Commands tests (unknownCommand, reseed NextAction), golden text
baselines; plus doc-presence assertions and `fsgg-sdd validate`.

**Target Platform**: `fsgg-sdd` CLI.

**Project Type**: Single multi-project solution. Code touches
`FS.GG.SDD.Commands` (`CommandRendering.fs`, `CommandReports/*`); docs at repo root
and `docs/`.

**Performance/Constraints**: `doctor`/`upgrade` **JSON byte-identical**
(FR-004/SC-002); only the enumerated golden text baselines change (FR-012); rich
degrades to zero-ANSI (unchanged behaviour).

**Scale/Scope**: ~4 small code edits + 1 pin test + ~5 doc files.

## Change Classification

**Tier 1 (contracted change)** for the code portion: this feature **deliberately
changes** the `doctor`/`upgrade` **text** projection (adds drift lines), the
`unknownCommand` correction string, and the reseed `NextAction` paths — all
tool-visible. The corresponding golden **text** baselines are updated as a reviewed
part of the fix (FR-012). JSON stays byte-identical. Doc changes are Tier 2.

## Constitution Check

- **I. Spec → FSI → Tests → Impl**: PASS. No new public API surface (renderer edits
  are internal; the summary types already carry `SkillDriftPaths`). `.fsi` files
  unaffected. Tests precede/accompany each correction.
- **II. Structured Artifacts Are the Machine Contract**: PASS. JSON untouched
  (FR-004); the drift data already exists in the summary and serializer.
- **III. Visibility in `.fsi`**: PASS. No visibility changes.
- **IV. Idiomatic Simplicity**: PASS. Reuses the existing `missingArtifacts`
  render shape and the text→rich derivation; no new machinery.
- **V. MVU boundary**: N/A — pure rendering + static string/list edits.
- **VI. Test Evidence Mandatory**: PASS. Real projection tests + a command-set pin
  test; docs verified by presence assertions.
- **VII. Agent & Human Workflows Share One Contract**: PASS. Improves the human
  projection to match the JSON the agent already sees.
- **VIII. Observability & Safe Failure**: PASS. Strictly *more* observable (drift
  now visible); `projectRoot` determinism preserved.

**Result**: No violations. Complexity Tracking empty.

## Project Structure

```text
specs/063-doctor-drift-and-doc-currency/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/projection-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit-tasks)
```

### Source & docs touched

```text
src/FS.GG.SDD.Commands/CommandRendering.fs          # + skillDrift lines (doctor + upgrade) in renderText
src/FS.GG.SDD.Commands/CommandReports/
├── DiagnosticConstructors.fs                        # unknownCommand: full 18-command correction
├── NextActionRouting.fs                             # reseed NextAction: + ".agents/skills"
└── ReportAssembly.fs                                # projectRoot: rationale comment only
tests/FS.GG.SDD.Cli.Tests/RemediationProjectionTests.fs   # assert skillDrift in text/rich, JSON unchanged
tests/FS.GG.SDD.Commands.Tests/…                     # unknownCommand pin test; reseed NextAction path
README.md · docs/quickstart.md · docs/index.md · DEVELOPING.md · .github/workflows/release.yml
```

**Structure Decision**: Edit in place — no new modules. The drift-rendering fix is
localized to `renderText` precisely because the rich path already derives from it,
so one edit satisfies three FRs with no duplication.

## Complexity Tracking

> No constitution violations — intentionally empty.

## Out of Scope

- The **CLAUDE.md/AGENTS.md drift-guard** candidate (issue #73 final bullet) — a new
  generator + pin test, not a correction; tracked as a separate follow-up.
- Any `projectRoot` behaviour change (reclassified as intentional determinism).
- Any JSON/schema change.
