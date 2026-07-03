# Feature Specification: Test-infrastructure hardening

**Feature Branch**: `067-test-infra-hardening`

**Created**: 2026-07-03

**Status**: Draft

**Input**: FS.GG.SDD roadmap issue #75 (repo-local, not cross-repo) — the test-infrastructure remediation batch from the 2026-07-02 code-quality & architecture review §4.3 + §3.6 / remediation item #11 (MEDIUM). The review found the product code healthy but the *test suite* carrying accumulated defects: process-global environment races that make scheduled CI non-deterministic, ~106 fixture manifests consumed by nothing, an inconsistent baseline-regeneration switch, temp-directory leaks, validation-harness cells that don't actually vary their environment, and triplicated helpers. Source: `docs/reports/2026-07-02-140616-code-quality-architecture-review.md` @ 8881620.

**Change Tier**: Tier 2 (internal change). This feature is centered on test projects, test fixtures, and test-support helpers, and additionally makes **contract-neutral internal edits** to the `fsgg-sdd validate` harness in `src/FS.GG.SDD.Validation/ValidationRunner.fs` (temp-artifact cleanup and genuinely-distinct environment cells — items 4 and 5). All such product-code edits are confined to `.fs`-internal functions that appear in no `.fsi`, and the emitted `validation-report` is not byte-golden-pinned (its verdict structure and schema are unchanged; only its already-non-deterministic sensed metadata is unpinned). The feature introduces **no** change to any `fsgg-sdd` CLI output, JSON automation contract, persisted schema, generated view, artifact layout, agent-skill contract, public `.fsi` surface, or committed baseline. Surface-area baselines and golden fixtures remain byte-identical; only the *mechanism* by which baseline tests regenerate their expected files is unified. Per the constitution, signatures and baselines remain unchanged. (This scope was refined during planning — see `research.md` "Scope correction".)

## Overview

The 2026-07-02 review confirmed that FS.GG.SDD's product source is in good
shape, but flagged the test suite as the largest remaining source of risk. The
defects are not incorrect assertions — they are infrastructure hazards that make
the suite **non-deterministic, misleading, leaky, and hard to maintain**:

- Tests mutate process-global state (`PATH`, `FSGG_SDD_ACCEPTANCE_REGISTRY`)
  while sibling test collections run in parallel and spawn processes, so a green
  local run can go red on scheduled CI for reasons unrelated to the code under
  test.
- 106 of 107 lifecycle-command fixture manifests are dead: they read like a
  documented contract but no test consumes them, so they silently drift from
  reality.
- Only one of four public-surface baseline tests honors the
  `FSGG_UPDATE_BASELINE` regeneration switch, so regenerating baselines is a
  partly-manual, error-prone ritual.
- Roughly 800 per-fact temp directories and ~350 per-run project copies are
  created and never deleted, leaking disk on developer machines and CI runners.
- The validation harness advertises five distinct environment cells but four run
  the identical neutral comparison — the degradation guarantees it claims to
  exercise are not actually exercised.
- `findRepoRoot` / `writeRelative` helpers and an evidence-ladder task list are
  copy-pasted across projects, so a fix in one place silently misses the others.

This feature pays that debt down. The product's observable behavior is unchanged;
what changes is that the **suite that guards that behavior becomes deterministic,
honest, self-cleaning, and de-duplicated**.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the developers and CI systems that run and
maintain the FS.GG.SDD test suite.

### User Story 1 - Deterministic, parallel-safe suite (Priority: P1)

A developer or CI runner executes the full test suite — including the scheduled
run where `FSGG_SDD_ACCEPTANCE_REGISTRY` is set — and the result depends only on
the code under test, never on which test collections happened to run
concurrently.

**Why this priority**: A flaky suite is worse than a slow one — it erodes trust
in every gate and hides real regressions in the noise. Process-global `PATH` and
environment mutation racing against parallel process-spawning collections is the
single highest-risk defect in the batch, and the only one that produces
intermittent red CI.

**Independent Test**: Run the full suite repeatedly (locally and with the
acceptance registry set) with parallelization enabled; every run yields the same
pass/fail result with no environment-ordering flakes.

**Acceptance Scenarios**:

1. **Given** the suite runs with test parallelization enabled, **When** a test that mutates process-global `PATH` executes concurrently with sibling collections that spawn processes, **Then** the environment-mutating test's changes are isolated and no sibling observes a mutated `PATH`.
2. **Given** `FSGG_SDD_ACCEPTANCE_REGISTRY` is set (the scheduled-CI condition), **When** the acceptance tests null the variable and reshape `PATH`, **Then** those mutations are serialized so no other collection reads a half-mutated environment, and the run is deterministic.
3. **Given** the same commit, **When** the full suite is run multiple times in succession, **Then** the pass/fail outcome is identical every time.

---

### User Story 2 - Honest, consumed fixtures (Priority: P2)

A developer reading `tests/fixtures/lifecycle-commands/` can trust that every
fixture manifest present is actually exercised by a test — there is no
directory of authoritative-looking-but-dead documentation.

**Why this priority**: Dead fixtures are actively misleading: they invite
maintainers to keep them "correct" for no benefit and they rot into
false documentation of the CLI contract. Resolving them (wire in or delete)
restores the suite's honesty but is lower-risk than the race fix.

**Independent Test**: Enumerate the fixture manifests; every one is either
referenced by an executing test or removed. No orphaned manifest remains.

**Acceptance Scenarios**:

1. **Given** the lifecycle-command fixture manifests, **When** the suite is inventoried, **Then** each remaining manifest is consumed by at least one executing test.
2. **Given** a manifest that no test consumes, **When** this feature completes, **Then** that manifest has been either wired into a test that verifies its declared behavior or removed from the tree.

---

### User Story 3 - Uniform baseline regeneration (Priority: P2)

A developer intentionally changing a public surface regenerates **every**
affected baseline with one consistent switch, instead of hand-editing the
baselines that lack the regeneration path.

**Why this priority**: An inconsistent regeneration mechanism makes a routine,
sanctioned operation (updating a baseline after a deliberate surface change)
error-prone and asymmetric across the four baseline tests. Unifying it removes a
recurring papercut without changing any baseline content.

**Independent Test**: With the regeneration switch enabled, run each baseline
test; each one regenerates its own expected file. With the switch off, each
asserts against the committed baseline.

**Acceptance Scenarios**:

1. **Given** the baseline-regeneration switch is enabled, **When** each public-surface baseline test runs, **Then** every one of them regenerates its committed baseline file (not just one).
2. **Given** the switch is not set, **When** the baseline tests run, **Then** every one asserts the current surface against its committed baseline and the baselines are byte-identical to today's.

---

### User Story 4 - Self-cleaning temp artifacts (Priority: P2)

A developer running the suite on their machine, and a CI runner executing it
repeatedly, do not accumulate abandoned temp directories or project copies —
disk usage returns to its pre-run level once the suite finishes.

**Why this priority**: Leaked temp directories (~800 per-fact, ~350 per-run
project copies) are a slow-burn hazard: they fill developer disks and can
destabilize long-lived CI runners. The fix is contained and observable.

**Independent Test**: Record temp-space usage before and after a full suite run;
the net growth attributable to test temp artifacts is zero (allocated temp
artifacts are deterministically removed).

**Acceptance Scenarios**:

1. **Given** a full suite run, **When** it completes (passing or failing), **Then** the per-fact temp directories it allocated are removed.
2. **Given** the validation harness copies the project many times per run, **When** the run completes, **Then** those project copies are cleaned up.
3. **Given** a test fails mid-run, **When** its fixture is torn down, **Then** the temp artifacts it created are still removed (cleanup is not skipped on failure).

---

### User Story 5 - Genuinely distinct validation-harness cells (Priority: P3)

Someone auditing the validation harness's environment matrix sees each
environment cell exercise a real, distinct condition, so the harness's
degradation and determinism guarantees are actually tested rather than asserted
against an unvaried baseline.

**Why this priority**: A harness that claims to cover `NO_COLOR`, `TERM=dumb`,
and a perturbed host but actually runs the same neutral comparison five times
gives false coverage confidence. Correctness of the harness's own guarantees
depends on the cells differing. Lower priority because it affects meta-coverage,
not the product suite's day-to-day reliability.

**Independent Test**: Inspect each environment cell's inputs; the color-disabled
cells actually set the color-disabling condition, the perturbed-host cell varies
the working directory, and the rich cell can genuinely diverge from plain text so
an ANSI regression would fail it.

**Acceptance Scenarios**:

1. **Given** the environment matrix, **When** the color-disabled cells run, **Then** the color-disabling condition (e.g. `NO_COLOR` / `TERM=dumb`) is actually applied for those cells and not for the neutral cell.
2. **Given** the perturbed-host cell, **When** it runs, **Then** it varies the working directory as its description promises.
3. **Given** a rich-output cell, **When** an ANSI-emitting regression is introduced, **Then** the cell's check can fail (rich output is not defined to be identical to plain text).

---

### User Story 6 - De-duplicated test helpers (Priority: P3)

A maintainer fixing a shared test helper (`findRepoRoot`, `writeRelative`, or the
evidence-ladder task list) edits it in one place and every consumer picks up the
fix.

**Why this priority**: Triplicated helpers guarantee that a fix or a
repo-layout change will be applied inconsistently. Consolidating removes a
latent maintenance trap, but nothing is broken today, so it is the lowest
priority.

**Independent Test**: Search the test tree for the previously-duplicated
helpers; each exists in a single shared location and the former copies are gone.

**Acceptance Scenarios**:

1. **Given** the test-support code, **When** `findRepoRoot` / `writeRelative` are located, **Then** each is defined once and shared by all former call sites.
2. **Given** the evidence-ladder task list previously hardcoded as `T001`–`T006`, **When** it is located, **Then** it is defined in one place rather than copied per consumer.

### Edge Cases

- **A test fails or throws mid-run**: temp-directory and project-copy cleanup MUST still run (cleanup is failure-safe, not success-only).
- **The acceptance registry is unset (default offline inner loop)**: the env-isolation changes MUST NOT alter behavior — the default fast local run stays green and unchanged.
- **A fixture manifest is genuinely useful but merely not yet wired**: it MUST be wired in (verifying its declared behavior), not deleted, so real coverage is not lost when resolving orphans.
- **Baseline regeneration switch enabled on an unchanged surface**: regenerating MUST reproduce byte-identical baselines (no spurious diff).
- **Running the suite with parallelization disabled**: results MUST remain identical to the parallel run (isolation does not depend on serialization being globally off).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Tests that mutate process-global environment state (`PATH`, `FSGG_SDD_ACCEPTANCE_REGISTRY`) MUST NOT allow that mutation to be observed by any concurrently-executing test — the mutation MUST be isolated or serialized against every collection that reads the same state or spawns processes.
- **FR-002**: The acceptance-test class that reshapes `PATH` and nulls `FSGG_SDD_ACCEPTANCE_REGISTRY` MUST be prevented from running in parallel with other process-spawning collections when the registry is set (the scheduled-CI condition).
- **FR-003**: Repeated runs of the full suite against the same commit MUST produce the same pass/fail outcome, with parallelization enabled, both offline and with the acceptance registry set.
- **FR-004**: Every lifecycle-command fixture manifest that remains in the tree MUST be consumed by at least one executing test; any manifest consumed by no test MUST be either wired into a test that verifies its declared behavior or removed.
- **FR-005**: All public-surface baseline tests MUST honor a single, consistent baseline-regeneration switch, so enabling that switch regenerates every baseline and leaving it unset asserts every baseline against its committed file.
- **FR-006**: The committed baselines and golden fixtures MUST remain byte-identical after this feature — only the regeneration mechanism is unified, not any baseline content.
- **FR-007**: Temp directories allocated per fact and project copies allocated per validation run MUST be deterministically removed when the run completes, including when a test fails.
- **FR-008**: Net temp-space growth attributable to a completed full suite run MUST be zero (allocated temp artifacts are cleaned up rather than abandoned).
- **FR-009**: Each validation-harness environment cell MUST exercise a genuinely distinct condition: the color-disabled cells MUST actually apply the color-disabling condition (`NO_COLOR` / `TERM=dumb`), and the perturbed-host cell MUST vary the working directory in addition to locale/time-zone. The library's deliberate rich→plain-text degradation (research Decision 6 — no Spectre dependency in the validation library) is preserved rather than reversed; the rich-output ANSI-degradation guarantee remains covered by the CLI-process tests (`ValidateCommandTests`), so forcing library rich output to differ from plain text is explicitly out of scope (it would be a Tier-1 architectural change).
- **FR-010**: The `findRepoRoot` and `writeRelative` helpers MUST each be defined once and shared by all former call sites; the former duplicated copies MUST be removed.
- **FR-011**: The evidence-ladder task list previously hardcoded as `T001`–`T006` across consumers MUST be defined in a single shared location.
- **FR-012**: This feature MUST NOT change any `fsgg-sdd` CLI output, JSON automation contract, persisted schema, generated view, artifact layout, agent-skill contract, public `.fsi` surface, committed baseline, or the `validation-report` schema/verdict structure — verified by the surface-area baselines and golden/deterministic contracts remaining byte-identical (a pre/post `git diff` over `**/*.baseline`, `src/**/*.fsi`, and the golden fixture dirs is empty). Internal (`.fs`-only) edits to `src/FS.GG.SDD.Validation` are permitted only insofar as they preserve all of the above.

### Key Entities

- **Fixture manifest**: a `tests/fixtures/lifecycle-commands/<name>/manifest.yml` describing a lifecycle-command scenario; authoritative only if an executing test consumes it.
- **Public-surface baseline**: a committed expected-surface file for a public assembly, regenerated via the baseline-regeneration switch and otherwise asserted against.
- **Temp artifact**: a per-fact temp directory or a per-run project copy allocated by test support; must be cleaned up on completion.
- **Validation-harness environment cell**: one row in the harness's environment matrix, defined by the environment condition (color/`TERM`/host/cwd) it applies before comparing output projections.
- **Shared test helper**: a cross-project test-support function (`findRepoRoot`, `writeRelative`) or data list (the evidence ladder) that must have a single definition.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The full test suite produces an identical pass/fail result across at least 5 consecutive runs with parallelization enabled, both offline and with `FSGG_SDD_ACCEPTANCE_REGISTRY` set — zero environment-ordering flakes.
- **SC-002**: Zero lifecycle-command fixture manifests remain unconsumed — every manifest in the tree is exercised by an executing test, or has been removed.
- **SC-003**: 100% of public-surface baseline tests (currently 4) regenerate their baseline under the unified switch; with the switch unset, 100% assert against committed baselines, and all committed baselines are byte-identical to their pre-feature content.
- **SC-004**: Test temp artifacts do not accumulate across runs. *Refined during implementation:* xUnit v2 under the VSTest host force-kills process-exit handlers before a large recursive delete finishes, so strict "0 immediately after a single run" is not achievable without per-test disposal. The delivered guarantee is **self-healing bounded residue**: each run's temp dirs nest under one pid-tagged root, and every run's startup reclaims roots owned by *dead* processes — so residue stays bounded to at most the most-recent run and never accumulates (the pre-feature bug leaked ~800/run + ~350/run *unbounded*; measured 567 orphaned dirs). The product-side `validate` harness (`ValidationRunner`, whose `finally` runs with the process alive) leaks **0**. Cleanup is failure-safe.
- **SC-005**: Each of the validation harness's environment cells applies a distinct, verifiable environment condition — no two cells run the identical neutral comparison.
- **SC-006**: `findRepoRoot`, `writeRelative`, and the evidence-ladder task list each have exactly one definition in the test tree (duplicate count reduced from 3 to 1).
- **SC-007**: The `fsgg-sdd` surface-area baselines, JSON automation contracts, and golden/deterministic fixtures are unchanged — a diff of those artifacts before and after the feature is empty.

## Assumptions

- The "resolution" of the 106 orphaned fixture manifests is decided per-manifest during planning: those describing behavior worth guarding are wired into tests; the remainder are deleted. Losing no *real* coverage is the constraint; keeping dead files is not.
- The default developer inner loop runs offline (`FSGG_SDD_ACCEPTANCE_REGISTRY` unset); the network-gated acceptance path is opt-in and the primary place the env-race manifests, so isolation work targets both but must leave the offline path unchanged.
- xUnit collection/parallelization semantics are the mechanism for env isolation; the exact grouping is an implementation/plan decision, not a spec constraint.
- No new external dependency, tool, or CI service is required — this is contained to existing test projects, fixtures, and contract-neutral internal edits to the `src/FS.GG.SDD.Validation` harness (items 4–5).
- The 2026-07-02 review report (@ 8881620) is the authoritative inventory of defects; any additional test-infra defect discovered during implementation may be folded in only if it does not change the Tier-2, no-contract-change boundary.
