# Artifact Traceability

Feature: `006-clarify-command`

## Requirements

- `specs/006-clarify-command/spec.md` defines `fsgg-sdd clarify`, durable
  clarification artifacts, stable question/decision ids, safe reruns,
  diagnostics, deterministic reports, text projection, dry-run behavior, and
  optional Governance boundary facts.
- `specs/006-clarify-command/plan.md` binds the feature to the existing
  `FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` projects.
- `specs/006-clarify-command/contracts/` documents the clarification artifact,
  command behavior, JSON report shape, and fixture families.

## Implementation

- `src/FS.GG.SDD.Artifacts/Identifiers.fsi` and `.fs` add
  `ClarificationQuestionId`.
- `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` and `.fs` add
  clarification front matter, questions, answers, decisions, accepted
  deferrals, remaining ambiguity, parser contracts, and optional decision
  contribution to the normalized work model when `clarifications.md` exists.
- `src/FS.GG.SDD.Commands/CommandTypes.fsi` and `.fs` add
  `ClarificationSummary` to the command report and model.
- `src/FS.GG.SDD.Commands/CommandWorkflow.fs` routes `Clarify` through the
  existing MVU/effect boundary, plans real filesystem reads before writes,
  validates specification prerequisites, creates or safely reruns
  `clarifications.md`, blocks unsafe updates, and reports generated-view state.
- `src/FS.GG.SDD.Commands/CommandReports.fsi` and `.fs` add clarify diagnostic
  builders and next-action artifacts.
- `src/FS.GG.SDD.Commands/CommandSerialization.fs` and
  `CommandRendering.fs` serialize/render clarification facts.
- `src/FS.GG.SDD.Cli/Program.fs` continues to host commands through the same
  command workflow and interpreter.

## Fixtures And Tests

- `tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs` covers
  parser extraction, duplicate ids, and schema compatibility diagnostics.
- `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` covers create,
  no-open-ambiguity, no-Governance, rerun preservation, missing-section
  insertion, accepted deferral, missing answer, unknown reference, identity
  mismatch, unsafe decision change, dry-run, generated-view refresh, and
  deterministic JSON.
- `CommandWorkflowTests`, `GeneratedViewCommandTests`, `CommandReportJsonTests`,
  `TextProjectionTests`, and `GovernanceBoundaryCommandTests` cover MVU read
  planning, blocking write prevention, generated-view reporting, report
  determinism, text projection, and optional Governance compatibility.
- `tests/fixtures/lifecycle-commands/*/manifest.yml` names the clarify fixture
  families and corresponding test evidence.

## Evidence

- Focused clarify tests: `clarify-create-tests.txt`,
  `clarify-rerun-tests.txt`, `clarify-diagnostics-tests.txt`, and
  `clarify-traceability-tests.txt`.
- Generated-view slice: `generated-view-tests.txt`.
- Release build: `build-release.txt`.
- Public FSI transcript: `fsi-session.txt`.
- Real CLI smoke path: `cli-smoke.txt`.
- Performance evidence: `performance.txt`.
- Full suite: `full-suite.txt`.

All evidence uses real filesystem and process execution. No synthetic
dependencies are required for this feature.
