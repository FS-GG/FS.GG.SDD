# Feature Specification: API Surface Drift Check

**Feature Branch**: `086-api-surface-drift-check`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "A new `fsgg-sdd surface` command that enforces the FS-GG API-surface baseline convention in a scaffolded workspace. Today workspace authors hand-`cp` their `.fsi` signature files into `docs/api-surface/` and hand-write a per-package drift test; make it first-class. Each source project has a hand-authored `.fsi` at `src/<Pkg>/<Name>.fsi` (the compiler-enforced public surface, source of truth); a byte-identical committed baseline copy lives at `docs/api-surface/<Pkg>/<Name>.fsi`, mirroring the src-relative path. `surface --check` enumerates every authored `.fsi` under `src/`, maps each to its baseline, reports drift when a baseline is MISSING or DIFFERS byte-for-byte, is read-only, exits 1 on any drift (fails CI) and 0 when coherent, and also surfaces ORPHAN baselines as a warning. `surface --update` writes each source `.fsi` to its baseline path (creating dirs), refreshing missing/differing baselines, reporting changed paths, only rewriting on genuine change, exit 0. Default = check. Standard three projections (json/text/rich). Roots are convention-default (`src/`, `docs/api-surface/`) with optional `--param sourceRoot=… --param baselineRoot=…` override. Source-of-truth is the `.fsi` text file (copy/diff), NOT assembly reflection. Origin: FS-GG/.github#240 (FS.GG.Audio feedback report #1 §3.8, report #2 §3.4)."

## Overview

Scaffolded FS-GG workspaces adopt a surface-area baseline convention: every source project carries a hand-authored F# signature file (`src/<Pkg>/<Name>.fsi`) that the compiler enforces as the public surface, and a byte-identical committed copy of that file lives under `docs/api-surface/` at the mirrored path. A change to a project's public API therefore shows up as a diff against the committed baseline — a reviewable, greppable record of surface evolution. Until now the workspace author maintained this by hand: `cp`-ing each `.fsi` into `docs/api-surface/` and hand-writing a per-package test asserting the baseline exists and matches. This feature makes that convention a first-class `fsgg-sdd surface` command: `--check` (read-only, CI-gating) and `--update` (refresh the committed baselines), with the standard three projections.

## Clarifications

### Session 2026-07-07

- Q: What is the source of truth for the surface — the compiled assembly or the `.fsi` text? → A: The `.fsi` **text file** (copy/diff), never assembly reflection. This matches the FS.GG.Audio `docs/api-surface` convention and the org Governance constitution ("Visibility Lives in `.fsi` / surface-area baselines validated by an automated API surface-drift check"), needs no build, and works generically in any scaffolded workspace. Comparison is byte-for-byte with no normalization.
- Q: Does this replace FS.GG.SDD's own internal reflection-based `PublicSurface.baseline` test for the component's five libraries? → A: No. That is a separate mechanism for the component repo. The `surface` command targets scaffolded-workspace `.fsi`-file conventions and is explicitly a non-goal for the component's reflection baseline. (See Assumptions.)
- Q: Can `--check` auto-remove an orphan baseline (a `docs/api-surface/**/*.fsi` with no source)? → A: No. Orphans are reported as a warning only; no delete effect exists in this version. Removing a stale baseline stays a manual author action (documented limitation / non-goal).
- Q: What is the default with neither flag? → A: `--check`. The safe, read-only, CI-friendly behavior is the default; writing baselines is opt-in via `--update`.
- Q: Are the roots configurable? → A: They default to `src/` and `docs/api-surface/` by convention; an author may override each via `--param sourceRoot=…` / `--param baselineRoot=…`. The override is minimal and optional; the `<Pkg>/<Name>.fsi` mirroring rule is unchanged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fail CI when a committed surface baseline drifts (Priority: P1)

A workspace author or CI job runs `fsgg-sdd surface --check`. The command enumerates every authored `.fsi` under `src/`, maps each to its baseline under `docs/api-surface/` at the mirrored path, and compares byte-for-byte. When every baseline is present and identical it reports coherent and exits 0; when any baseline is missing or differs it reports the offending files and exits 1, so the CI gate fails and the author is forced to reconcile the committed surface.

**Why this priority**: This is the whole feature — an automated, CI-gating surface-drift check that replaces the hand-written per-package test. Without the failing-exit check there is nothing to enforce.

**Independent Test**: In a fixture workspace with two source `.fsi` files and matching baselines, run `surface --check` and confirm exit 0 / coherent. Delete one baseline and edit one byte of the other's baseline, re-run, and confirm exit 1 with both files reported (one `missing-baseline`, one `drifted`) and zero files written.

**Acceptance Scenarios**:

1. **Given** a workspace where every `src/<Pkg>/<Name>.fsi` has a byte-identical `docs/api-surface/<Pkg>/<Name>.fsi`, **When** the author runs `surface --check`, **Then** the report lists each pair as `matched`, the overall verdict is coherent, and the process exits 0.
2. **Given** a workspace where one source `.fsi` has no baseline and another's baseline differs by a single byte, **When** the author runs `surface --check`, **Then** the report marks the first `missing-baseline` and the second `drifted`, names both paths, the verdict is drift, and the process exits 1.
3. **Given** any `--check` run, **When** it completes, **Then** it has written zero files (read-only), regardless of verdict.

---

### User Story 2 - Refresh the committed baselines after an intentional surface change (Priority: P1)

After deliberately changing a project's public API (and its `.fsi`), the author runs `fsgg-sdd surface --update`. The command copies each source `.fsi` to its baseline path — creating any missing directories — refreshing every baseline that is missing or differs, and reports exactly which baseline paths changed. Baselines that already match are left byte-identical (no spurious rewrites). It exits 0. A subsequent `surface --check` then reports coherent.

**Why this priority**: The check is only usable if reconciling an intentional change is a single command rather than a hand `cp` per file. `--update` is the other half of the workflow and is on the P1 critical path.

**Independent Test**: In a fixture with one missing baseline and one drifted baseline, run `surface --update`; confirm it reports the two changed paths, creates the missing baseline's directory, leaves the already-matching baseline's bytes untouched, exits 0, and that a following `surface --check` exits 0.

**Acceptance Scenarios**:

1. **Given** a workspace with a missing baseline and a drifted baseline, **When** the author runs `surface --update`, **Then** both baselines are written to match their source `.fsi` byte-for-byte (creating directories as needed), the two changed paths are reported, and the run exits 0.
2. **Given** a baseline that is already byte-identical to its source, **When** `surface --update` runs, **Then** that baseline file is not rewritten (it stays byte-identical) and it is not listed among the changed paths.
3. **Given** an `--update` run has completed, **When** `surface --check` runs against the same tree, **Then** the check verdict is coherent and it exits 0.

---

### User Story 3 - Consistent projections, orphan warnings, and root overrides (Priority: P2)

The author consumes the surface report through the standard three projections and gets the same facts from each: the default machine-readable projection is a deterministic automation contract; `--text` is a portable plain report; `--rich` is a Spectre panel that degrades to plain text. The report carries the source root, the baseline root, a per-file status (`matched` / `missing-baseline` / `drifted` / `orphan`), counts, and the overall verdict. Orphan baselines (a `docs/api-surface/**/*.fsi` with no corresponding source) are surfaced as a warning. The author may point the command at non-default roots with `--param sourceRoot=… --param baselineRoot=…`.

**Why this priority**: Parity across projections and honest orphan reporting make the command trustworthy in both automation and human review, but they layer on top of the core check/update behavior rather than being the MVP itself.

**Independent Test**: Run `surface --check` with an orphan baseline present and all sources matched: confirm the orphan is listed as a warning in all three projections, the machine-readable and text projections carry an identical fact set, and the run exits 0 (an orphan alone is a warning, not drift). Separately, run with `--param sourceRoot=lib --param baselineRoot=docs/surface` and confirm the alternate roots are honored and echoed in the report.

**Acceptance Scenarios**:

1. **Given** a workspace with all sources matched but one baseline that has no corresponding source, **When** `surface --check` runs, **Then** the report lists that baseline as `orphan` with a warning, the overall verdict is coherent (orphan alone is not drift), and the run exits 0.
2. **Given** the same run rendered as `--json`, `--text`, and `--rich` (redirected/color-disabled), **When** the outputs are compared, **Then** they carry identical facts (source root, baseline root, per-file statuses, counts, verdict) and the redirected rich output contains zero color/box control sequences.
3. **Given** `--param sourceRoot=lib --param baselineRoot=docs/surface`, **When** `surface --check` runs, **Then** it enumerates `.fsi` under `lib/`, maps to `docs/surface/`, and the report echoes both roots.

### Edge Cases

- **No `.fsi` files under the source root**: the command reports zero source files, an empty coherent verdict, and exits 0 (nothing to drift). It does not error.
- **Source root does not exist**: reported as zero source files (coherent, exit 0) rather than a crash; if orphan baselines exist under the baseline root they are still surfaced as warnings.
- **Both `--check` and `--update` supplied**: `--update` takes precedence (a write pass runs); `--check` is redundant and adds no separate exit behavior. This precedence is stated in help.
- **Line-ending or whitespace-only difference (CRLF vs LF)**: byte-for-byte comparison flags this as `drifted` — there is no normalization. Reconciled by `--update` (the baseline becomes byte-identical to the source, including its line endings).
- **Orphan baseline alone (no source drift)**: warning only, verdict coherent, exit 0 — it never flips the exit code and is never auto-deleted.
- **A source `.fsi` and its baseline both present and identical except the baseline is newer on disk**: mtime is irrelevant; only content bytes are compared, so it is `matched`.
- **Rich output on a very narrow / non-interactive terminal**: degrades to the plain-text report with zero color/box control sequences; the per-file table wraps or scrolls within its own bounds.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST add a `surface` command to the `fsgg-sdd` CLI as a cross-cutting verb that is NOT a lifecycle stage; it MUST NOT require a `--work` id, and its next-lifecycle-command MUST be none.
- **FR-002**: `surface --check` MUST enumerate every authored `.fsi` file under the source root (default `src/`) and map each to a baseline path under the baseline root (default `docs/api-surface/`) such that the baseline-relative path mirrors the source-relative path (`src/<Pkg>/<Name>.fsi` → `docs/api-surface/<Pkg>/<Name>.fsi`).
- **FR-003**: For each source `.fsi`, `surface --check` MUST classify the pair as `matched` (baseline exists and is byte-identical), `missing-baseline` (no baseline at the mirrored path), or `drifted` (baseline exists but differs), using a byte-for-byte content comparison with NO normalization.
- **FR-004**: `surface --check` MUST be read-only — it MUST write zero files under any verdict.
- **FR-005**: `surface --check` MUST exit non-zero (exit 1) when any source file is `missing-baseline` or `drifted`, and exit 0 when every source file is `matched`.
- **FR-006**: The system MUST surface ORPHAN baselines — a `docs/api-surface/**/*.fsi` with no corresponding source `.fsi` — as a reported warning. An orphan alone MUST NOT change the exit code (it is not drift), and the command MUST NOT delete or modify an orphan in this version (documented limitation).
- **FR-007**: `surface --update` MUST write each source `.fsi` to its mirrored baseline path — creating parent directories as needed — refreshing every baseline that is `missing-baseline` or `drifted`, and MUST leave an already-`matched` baseline byte-identical (no rewrite on unchanged content). It MUST report the set of changed baseline paths and exit 0.
- **FR-008**: With neither flag supplied (and with `--check` supplied), the command MUST behave as `--check`. When both `--check` and `--update` are supplied, `--update` MUST take precedence.
- **FR-009**: The command MUST support the standard three projections — the default/`--json` machine-readable projection, `--text`, and `--rich` — carrying a report with: the source root, the baseline root, a per-file status (`matched` / `missing-baseline` / `drifted` / `orphan`), the file counts, and the overall coherent/drift verdict.
- **FR-010**: The default/`--json` projection MUST be a deterministic automation contract (stable field and file ordering) so repeated runs against an unchanged tree produce byte-identical machine-readable output.
- **FR-011**: The `--rich` projection MUST degrade to the plain-text report with zero color/box control sequences whenever output is non-interactive, redirected, or color is disabled, adding or dropping no facts relative to `--text`/`--json`.
- **FR-012**: The source and baseline roots MUST default to `src/` and `docs/api-surface/` respectively, with each optionally overridable via `--param sourceRoot=…` and `--param baselineRoot=…`; the `<Pkg>/<Name>.fsi` mirroring rule MUST be unchanged by an override.
- **FR-013**: Drift (a `missing-baseline` or `drifted` file) MUST be reported through a `DiagnosticError` (so `--check` exits 1), and an orphan baseline MUST be reported through a warning diagnostic (so it does not affect the exit code).
- **FR-014**: The command MUST embed NO provider/package-specific names, paths, or ids; the `<Pkg>` segment MUST be derived structurally from the source-relative path, never hardcoded, so the command works generically in any scaffolded workspace.

### Key Entities *(include if feature involves data)*

- **Surface Summary**: The structured fact carried on the command report. Represents one surface check/update pass: the resolved source root, the resolved baseline root, the ordered per-file entries, the counts by status, and the overall coherent/drift verdict.
- **Surface Entry**: One element of the summary — a source-relative `.fsi` path (or a baseline-relative path for an orphan), its mirrored counterpart path, and its status (`matched` / `missing-baseline` / `drifted` / `orphan`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a workspace where every source `.fsi` has a byte-identical baseline, `surface --check` exits 0 and reports 100% of pairs `matched`.
- **SC-002**: When a baseline is missing or differs by a single byte, `surface --check` exits 1 and names the offending file with its status; a one-byte difference is never reported as matched.
- **SC-003**: `surface --check` writes zero files under every verdict, confirmed by a byte-level before/after comparison of the workspace on a fixture.
- **SC-004**: `surface --update` makes a drifted workspace coherent — a subsequent `surface --check` exits 0 — while leaving every already-matched baseline byte-identical (no spurious rewrite), confirmed on a mixed fixture.
- **SC-005**: For any single run, the `--json`, `--text`, and `--rich` projections carry an identical fact set (roots, per-file statuses, counts, verdict); the redirected/color-disabled rich output contains zero color/box control sequences.
- **SC-006**: No provider/package-specific literal appears in the command's implementation or output; the command produces correct results on a fixture whose package names it has never seen (generic-SDD purity).
- **SC-007**: An orphan baseline is reported as a warning in all three projections and does not change the exit code (a coherent tree with an orphan still exits 0); it is never deleted.

## Assumptions

- **The source of truth is the `.fsi` text file, not the compiled assembly.** The command copies/diffs signature files and needs no build, matching the FS.GG.Audio `docs/api-surface` convention and the Governance constitution's surface-baseline rule. Assembly reflection is explicitly out of scope.
- **This command does NOT replace FS.GG.SDD's own internal reflection-based `PublicSurface.baseline` test** for the component's five libraries — that is a separate mechanism for the component repo. The `surface` command targets scaffolded-workspace `.fsi`-file conventions; the two coexist.
- **Byte-for-byte comparison, no normalization.** Whitespace, line endings, and BOM differences count as drift; reconciliation via `--update` makes the baseline byte-identical to the source, whatever its bytes.
- **Orphan removal is a manual author action in this version.** The command has no delete effect; surfacing orphans as warnings is the documented limit of its orphan handling.
- **Roots are convention-first.** `src/` and `docs/api-surface/` are the defaults; the `--param` overrides are minimal, optional, and do not change the mirroring rule.
- **No persisted-artifact schema change.** The command adds an additive `Surface` fact to the in-memory command report only; it does not change any on-disk artifact schema (work model, readiness, provenance).
