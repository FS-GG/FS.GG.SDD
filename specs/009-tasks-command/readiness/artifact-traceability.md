# Artifact Traceability

Review date: 2026-06-19

Feature: `009-tasks-command`

Trace:

- `spec.md` defines task creation, rerun preservation, invalid-state diagnostics, and deterministic output requirements.
- `plan.md` defines the task artifact contract, MVU/report boundary, generated-view behavior, and optional Governance compatibility boundary.
- `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` and `.fs` expose and parse `tasks.yml` facts, source snapshots, task statuses, graph findings, and source ids.
- `src/FS.GG.SDD.Artifacts/WorkModel.fsi`, `.fs`, and `Serialization.fs` project task graph fields into the normalized work model.
- `src/FS.GG.SDD.Commands/CommandTypes.fsi`, `.fs`, `CommandReports.fs`, `CommandSerialization.fs`, `CommandRendering.fs`, and `CommandWorkflow.fs` expose `TasksSummary`, diagnostics, JSON/text output, lifecycle effects, safe writes, rerun merge behavior, and generated-view refresh.
- `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs` verifies task parser shape, stale status, source ids, required evidence, duplicate ids, and schema validation.
- `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs` verifies task creation, no-Governance execution, missing plan blocking, rerun preservation, stale source marking, dependency cycle blocking, done-without-evidence blocking, dry-run no mutation, generated-view refresh, deterministic JSON, text projection, and performance budget.
- Projection and boundary tests in `CommandReportJsonTests.fs`, `TextProjectionTests.fs`, `GeneratedViewCommandTests.fs`, and `GovernanceBoundaryCommandTests.fs` verify the dedicated report surfaces for tasks.

Readiness Evidence:

- `artifact-task-tests.txt`
- `command-task-tests.txt`
- `output-boundary-tests.txt`
- `build-release.txt`
- `full-suite.txt`
- `fsi-public-surface.txt`
- `cli-json-smoke.txt`
- `cli-dry-run-smoke.txt`
- `cli-text-smoke.txt`
- `human-summary-review.md`
- `performance.md`
- `sdd-governance-boundary.md`
