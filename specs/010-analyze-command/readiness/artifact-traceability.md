# Artifact Traceability

Review date: 2026-06-20

Feature: `010-analyze-command`

Trace:

- `spec.md` defines `fsgg-sdd analyze`, implementation-readiness analysis,
  cross-artifact diagnostics, authored-source preservation, deterministic
  output, and optional Governance boundaries.
- `plan.md` defines the analysis generated-view contract, MVU/report boundary,
  generated-view behavior, CLI smoke path, and verification evidence plan.
- `contracts/analysis-view.md`, `contracts/analyze-command.md`, and
  `contracts/analyze-report-json.md` define the generated analysis view,
  command behavior, and authoritative JSON report shape.
- `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` and `.fs` expose and parse
  `AnalysisView`, source records, source relationships, readiness counts,
  findings, generated-view records, optional boundary facts, diagnostics, and
  next action.
- `src/FS.GG.SDD.Commands/CommandTypes.fsi`, `.fs`,
  `CommandReports.fsi`, `.fs`, `CommandSerialization.fs`,
  `CommandRendering.fs`, and `CommandWorkflow.fs` expose `Analyze`,
  `AnalysisSummary`, analysis diagnostics, JSON/text output, lifecycle read
  effects, generated writes, dry-run behavior, and generated-view refresh.
- `src/FS.GG.SDD.Cli/Program.fs` dispatches `analyze --work <id> --root <path>`
  through the same command workflow as the library surface.
- `tests/FS.GG.SDD.Artifacts.Tests/AnalysisViewTests.fs` verifies analysis view
  parsing and malformed generated JSON diagnostics.
- `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs` verifies analysis
  create, no-Governance execution, missing tasks blocking, dry-run no
  mutation, authored-source preservation, deterministic JSON, text projection,
  performance, and CLI JSON/dry-run/text smokes.
- `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` verifies analyze
  read effects and generated write planning through the MVU boundary.

Readiness Evidence:

- `artifact-analysis-tests.txt`
- `command-analyze-tests.txt`
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
