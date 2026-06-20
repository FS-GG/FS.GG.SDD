# Contract: Automated Lifecycle Smoke

The lifecycle smoke (`tests/FS.GG.SDD.Commands.Tests/LifecycleSmokeTests.fs`) is
the automated verification that the documented bootstrap experience works and
stays true to command behavior. It drives the existing command workflow
in-process over a disposable project; it adds no new public surface.

## Harness

- Project root: `TestSupport.tempDirectory()` (a fresh OS temp directory per run;
  the test cleans it up).
- Drive: the existing `TestSupport` run helpers, which route requests through the
  real `init`/`update`/effect interpreter and write real files under the temp
  root:
  `initializeProject` → `runCharter` → `runSpecify` → `runClarify` →
  `runChecklist` → `runPlan` → `runTasks` → `runAnalyze` → `runEvidence` →
  `runVerify` → `runShip`, then `runAgents` and `runRefresh`.
- No surrounding-repository state is read; no repository file is written.

## Required assertions

1. **Stage success + artifacts**: each stage returns a success/non-blocked
   outcome and produces its authored source and generated readiness view:
   - charter → `work/<id>/charter.md`; specify → `spec.md`; clarify →
     `clarifications.md`; checklist → `checklist.md`; plan → `plan.md` (+
     `contracts/`); tasks → `tasks.yml`; evidence → `evidence.yml`.
   - analyze → `readiness/<id>/analysis.json`; verify → `verify.json`; ship →
     `ship.json`; the work model `work-model.json` is present and current.
   - agents → `readiness/<id>/agent-commands/<target>/` for each configured
     target; refresh → `summary.md` plus a current cross-view report.
2. **Canonical order + next-action chain (FR-014)**: the `nextAction` /
   `nextLifecycleCommand` each stage emits matches the documented quickstart
   chain (charter→specify→…→ship; analyze→evidence→verify→ship; `agents` and
   `refresh` are cross-cutting with `nextLifecycleCommand = None`). A behavioral
   change to ordering or pointers must break this assertion.
3. **No Governance required (FR-005)**: after a full run, no `.fsgg/policy.yml`,
   `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml` exists or was required; the
   lifecycle completed with Governance absent.
4. **Determinism (FR-006)**: running the full lifecycle twice over identical
   inputs (two temp projects with identical authored inputs) yields byte-
   identical machine-readable readiness outputs (`work-model.json`,
   `analysis.json`, `verify.json`, `ship.json`, and the refresh report JSON),
   after path-normalizing the temp root.
5. **Well-formed readiness**: each asserted JSON view parses and carries its
   generation manifest (sources, source digests, schema version, generator
   identity); `summary.md` is marked generated and records its sources.

## Determinism constraints

Asserted output excludes implicit clocks, durations, terminal width, ANSI
styling, directory enumeration order, host path separators, random values, and
absolute host paths. The temp root is normalized before comparison.

## CLI process evidence

Separately, a real `FS.GG.SDD.Cli` process run (`init` through `ship` over a
disposable directory, JSON output) is captured as readiness evidence
(`cli-smoke.txt`) to prove the shipped executable path, per Constitution VI. This
capture is evidence, not part of the deterministic assertion suite.

## Out of scope

No Governance routing, freshness, profile, gate, audit, or release behavior is
exercised or asserted (FR-016). No runtime product templates or FS.GG.Rendering
package are required (FR-013).
