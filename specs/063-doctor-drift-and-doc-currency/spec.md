# Feature Specification: Surface skillDriftPaths + correct stale report/doc surfaces

**Feature Branch**: `063-doctor-drift-and-doc-currency`

**Created**: 2026-07-03

**Input**: FS.GG.SDD issue #73 — 2026-07-02 code-quality & architecture review (§3.3 + §5.3 / remediation #9, MEDIUM/LOW). Repo-local, not cross-repo. Stacked on #062 (`062-split-command-reports`).

**Change Tier**: Tier 1 (contracted change). Unlike #062 (which preserved every output), this feature **deliberately corrects** several tool-visible outputs — the `doctor`/`upgrade` text & rich projections gain a `skillDriftPaths` line, the `unknownCommand` correction string is completed, the reseed `NextAction` gains a path, and `projectRoot` is reconsidered. Each corresponding golden baseline is updated **as a reviewed part of the fix**, and the JSON automation contract is only changed where the change is itself the bug fix (never silently). Documentation changes are Tier 2.

## Overview

The 2026-07-02 review found a real observability bug plus a cluster of stale
surfaces:

- **The bug (§3.3):** `skillDriftPaths` — the primary feature-058 content-drift
  signal — is serialized into the `doctor`/`upgrade` JSON but **never rendered**
  in the text or rich projections. `fsgg-sdd doctor --text` (and `--rich`) cannot
  show *which* seeded skill copies drifted, so a human running doctor sees drift
  is present (via `missingArtifacts`/coherence) but not the drifted skill paths.
- **Stale report content (§3.3):** the `unknownCommand` correction lists only 11
  of the ~18 commands; the reseed `NextAction` omits the feature-056
  `.agents/skills` root; `buildReport` hardcodes `projectRoot = "."`.
- **Stale docs (§5.3):** README and `docs/quickstart.md` omit `doctor`/`upgrade`
  entirely; `docs/index.md` doesn't link `reference/doctor-upgrade.md` and still
  says the repo "starts as an empty Spec Kit product scaffold"; `DEVELOPING.md`
  counts four projects (omitting `FS.GG.Contracts`) and points at the wrong props
  file for the warning ratchet; `release.yml`'s header comment hardcodes stale
  versions.

This feature makes drift visible in the human projections and brings the stale
report/doc surfaces back to truth.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A human sees which skill copies drifted (Priority: P1)

An author runs `fsgg-sdd doctor --text` (or `--rich`) on a scaffolded product
whose seeded `fs-gg-sdd-*` skill copies have drifted from the authored set. Today
the human projections report that drift exists but never list the drifted paths —
only the JSON carries `skillDriftPaths`. The author must switch to `--json` and
read raw JSON to see what drifted.

After this feature, the `doctor` and `upgrade` text and rich projections render
the `skillDriftPaths` (the same list already in the JSON), mirroring how
`missingArtifacts` is already rendered.

**Why this priority**: It's the actual bug — the primary content-drift surface is
invisible in the projections a human actually reads, defeating the point of
`doctor` as a human-facing drift report.

**Independent Test**: Run `doctor --text` and `--rich` against a state with
drifted skill paths; confirm each drifted path appears in the output; confirm the
JSON is unchanged (it already carried them).

**Acceptance Scenarios**:

1. **Given** a `doctor` report whose summary has non-empty `skillDriftPaths`,
   **When** it is rendered as `--text`, **Then** each drifted path appears (sorted,
   mirroring `missingArtifacts`).
2. **Given** the same report rendered as `--rich`, **Then** each drifted path
   appears in the rich block.
3. **Given** an `upgrade` report with non-empty `skillDriftPaths`, **When**
   rendered as text or rich, **Then** the drifted paths appear.
4. **Given** any `doctor`/`upgrade` report, **When** rendered as `--json`, **Then**
   the JSON is byte-identical to before this feature (the fix is projection-only).
5. **Given** an empty `skillDriftPaths`, **When** rendered, **Then** no drift line
   is emitted (parity with the empty-`missingArtifacts` case).

---

### User Story 2 - Report content tells the truth (Priority: P2)

Three report-content facts are stale or wrong:

- The `unknownCommand` correction lists only `init, charter, specify, clarify,
  checklist, plan, tasks, analyze, evidence, verify, ship` — omitting `agents`,
  `refresh`, `scaffold`, `doctor`, `upgrade`, `validate`, and `registry`. A user
  who typos one of those sees a correction that doesn't mention the command they
  wanted.
- The reseed `NextAction` for missing seeded skills lists `.claude/skills`,
  `.codex/skills`, and `.fsgg/early-stage-guidance.md` but omits the feature-056
  neutral `.agents/skills` root, so the remediation hint under-describes what will
  be reseeded.
- `buildReport` hardcodes `projectRoot = "."`. Investigation (see Assumptions)
  shows this is **intentional determinism**, not a defect — the report's
  project-root display is deliberately decoupled from the request's possibly
  absolute/temporary root so the JSON stays reproducible. This feature documents
  that intent rather than changing behaviour.

After this feature the `unknownCommand` correction enumerates the full command
set and the reseed `NextAction` includes `.agents/skills`; `projectRoot` stays the
deterministic `"."` with its rationale documented at the code site.

**Why this priority**: User-facing correctness on the diagnostic/report surface,
but lower impact than the invisible-drift bug; these are misleading rather than
missing.

**Independent Test**: Assert the `unknownCommand` correction contains every
current command; assert the reseed `NextAction` affected paths include
`.agents/skills`; confirm `projectRoot` stays `"."` and carries a rationale comment.

**Acceptance Scenarios**:

1. **Given** an unknown command, **When** the diagnostic is produced, **Then** its
   correction names every command the CLI accepts (init, charter, specify, clarify,
   checklist, plan, tasks, analyze, evidence, verify, ship, agents, refresh,
   scaffold, doctor, upgrade, validate, registry).
2. **Given** a reseed remediation, **When** the `NextAction` is built, **Then** its
   affected paths include `.agents/skills` alongside `.claude/skills` and
   `.codex/skills`.
3. **Given** the report's `projectRoot` display, **When** inspected, **Then** it
   remains the deterministic `"."` and the code site documents why (decoupled from
   the request's possibly-absolute root for reproducible JSON) — no output change.

---

### User Story 3 - The docs match the shipped product (Priority: P2)

A newcomer reading the docs is misled: `doctor`/`upgrade` (shipped in feature 053)
are absent from the README and quickstart; `docs/index.md` doesn't link the
`doctor-upgrade` reference and still claims the repo "starts as an empty Spec Kit
product scaffold"; `DEVELOPING.md` says there are four projects (there are five —
`FS.GG.Contracts` is missing) and points at the wrong props file for the warning
ratchet; `release.yml`'s header comment cites stale versions.

After this feature the docs describe the shipped command set and current
structure.

**Why this priority**: Documentation correctness — important for onboarding and
trust, but no runtime behaviour depends on it.

**Independent Test**: Grep the docs for `doctor`/`upgrade` presence; confirm
`index.md` links `reference/doctor-upgrade.md` and drops the "empty scaffold"
claim; confirm `DEVELOPING.md` lists five projects and the correct ratchet props
file; confirm `release.yml`'s header no longer hardcodes versions.

**Acceptance Scenarios**:

1. **Given** the README and `docs/quickstart.md`, **When** read, **Then** both
   describe `doctor` and `upgrade` in the command set.
2. **Given** `docs/index.md`, **When** read, **Then** it links
   `reference/doctor-upgrade.md` and no longer says the repo starts as an empty
   Spec Kit scaffold.
3. **Given** `DEVELOPING.md`, **When** read, **Then** it lists all five projects
   (including `FS.GG.Contracts`) and names the correct props file for the warning
   ratchet.
4. **Given** `.github/workflows/release.yml`, **When** read, **Then** its header
   comment does not assert stale hardcoded versions.

---

### Edge Cases

- A `doctor`/`upgrade` report with empty `skillDriftPaths`: renders no drift line
  (never an empty header), matching `missingArtifacts`.
- `projectRoot`: if the request's project root is an absolute machine path, echoing
  it verbatim into JSON would make golden output non-deterministic. The fix MUST
  preserve deterministic test output (see Assumptions).
- Rich degradation: the new `skillDriftPaths` rich rendering degrades to zero-ANSI
  plain text under non-interactive/`NO_COLOR`/`TERM=dumb`, like every other rich
  element (presentation only, excluded from byte-golden).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `doctor` **text** projection MUST render each `skillDriftPaths`
  entry (sorted), mirroring how `missingArtifacts` is rendered, and MUST emit
  nothing when the list is empty.
- **FR-002**: The `doctor` **rich** projection MUST render each `skillDriftPaths`
  entry, degrading to zero-ANSI plain text when color/interactivity is unavailable.
- **FR-003**: The `upgrade` text and rich projections MUST likewise render
  `skillDriftPaths`.
- **FR-004**: The `doctor`/`upgrade` **JSON** output MUST be byte-identical to
  before this feature (the drift-rendering fix is projection-only; JSON already
  carried the field).
- **FR-005**: The `unknownCommand` correction MUST enumerate every command the CLI
  accepts (init, charter, specify, clarify, checklist, plan, tasks, analyze,
  evidence, verify, ship, agents, refresh, scaffold, doctor, upgrade, validate,
  registry), and MUST stay in sync with the command set (a test pins the
  correspondence).
- **FR-006**: The reseed-seeded-skills `NextAction` affected paths MUST include the
  neutral `.agents/skills` root alongside `.claude/skills` and `.codex/skills`.
- **FR-007**: The report `projectRoot` MUST remain the deterministic `"."` (its
  hardcoding is intentional — it decouples the display from the request's possibly
  absolute/temporary root so JSON stays reproducible), and the code site MUST carry
  a comment documenting that rationale. No output change. (Reclassifies the issue's
  flag as a false positive after investigation.)
- **FR-008**: README and `docs/quickstart.md` MUST describe `doctor` and `upgrade`
  within the command set.
- **FR-009**: `docs/index.md` MUST link `reference/doctor-upgrade.md` and MUST NOT
  state the repo "starts as an empty Spec Kit product scaffold" (product source and
  tests now exist).
- **FR-010**: `DEVELOPING.md` MUST list all five projects including
  `FS.GG.Contracts` and MUST name the correct props file for the warning ratchet.
- **FR-011**: `.github/workflows/release.yml`'s header comment MUST NOT hardcode
  stale versions.
- **FR-012**: Every golden baseline that changes MUST change **only** to reflect an
  enumerated correction in FR-001..FR-007; no unrelated output may shift. Each such
  baseline update is a reviewed part of the fix.

### Key Entities

- **DoctorSummary / UpgradeSummary**: The report summaries that already carry
  `SkillDriftPaths: string list` (alongside `MissingArtifactPaths`). This feature
  adds their rendering to the text and rich projections; the types are unchanged.
- **unknownCommand diagnostic**: Its `Correction` string is completed to the full
  command set.
- **reseed NextAction**: Its affected-paths list gains `.agents/skills`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `doctor --text` and `--rich` show 100% of `skillDriftPaths` entries
  present in the JSON (0 today).
- **SC-002**: The `doctor`/`upgrade` JSON is byte-identical before/after (0 JSON
  byte changes).
- **SC-003**: The `unknownCommand` correction names 100% of the CLI's commands
  (~18), up from 11, pinned by a test that fails if a command is added without
  updating the correction.
- **SC-004**: The reseed `NextAction` lists all three seeded-skill roots
  (`.claude`, `.codex`, `.agents`), up from two.
- **SC-005**: Zero docs among {README, quickstart, index, DEVELOPING} misstate the
  shipped command set or project count; `release.yml` header carries no stale
  hardcoded version.
- **SC-006**: The full test suite is green with only the enumerated golden-baseline
  updates (FR-012); `fsgg-sdd validate` overallPassed.

## Assumptions

- **`projectRoot` determinism (FR-007) — resolved during planning**: The request
  carries a `ProjectRoot` field, but the test/golden harness passes an **absolute
  temp-dir** root (`Path.GetTempPath()/fsgg-sdd-<guid>`), and no golden JSON
  contains a `projectRoot` other than `"."`. So `buildReport`'s hardcoded `"."` is
  a deliberate decoupling that keeps JSON reproducible; echoing the request root
  would emit random absolute paths and break every golden plus the determinism
  principle. FR-007 is therefore reclassified: **document the intent, do not change
  behaviour.** The issue's flag was a false positive.
- The `skillDriftPaths` rich rendering follows the existing rich doctor/upgrade
  block conventions (panels/tables) and the project's degrade-to-zero-ANSI rule.
- The full command set for FR-005 is taken from the live `SddCommand` set; if the
  CLI gains a command later, the pinning test (SC-003) forces the correction to be
  updated.
- The **CLAUDE.md/AGENTS.md drift-guard** candidate mentioned in issue #73 is
  **out of scope** for this feature — it is a new capability (a generator + pin
  test), not a correction of an existing surface, and is tracked as a separate
  follow-up. This feature covers only the bug and the enumerated stale surfaces.
- This is a repo-local change stacked on #062; no cross-repo coordination is
  required.
