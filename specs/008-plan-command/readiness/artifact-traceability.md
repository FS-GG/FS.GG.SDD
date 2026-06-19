# Artifact Traceability

Feature sources:

- `specs/008-plan-command/spec.md`
- `specs/008-plan-command/plan.md`
- `specs/008-plan-command/data-model.md`
- `specs/008-plan-command/contracts/`
- `specs/008-plan-command/tasks.md`

Implemented contracts:

- Plan ids and plan parser contracts:
  `src/FS.GG.SDD.Artifacts/Identifiers.fsi`,
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi`
- Plan command report and MVU state:
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`
- Plan diagnostics, JSON, text, workflow, effects, and CLI entry point:
  `src/FS.GG.SDD.Commands/CommandReports.fs`,
  `src/FS.GG.SDD.Commands/CommandSerialization.fs`,
  `src/FS.GG.SDD.Commands/CommandRendering.fs`,
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`,
  `src/FS.GG.SDD.Cli/Program.fs`

Evidence:

- Parser and id tests: `artifact-plan-tests.txt`
- Command tests: `command-plan-tests.txt`
- Full suite: `full-suite.txt`
- FSI/prelude: `fsi-plan-surface.txt`
- CLI smoke: `cli-plan-json-smoke.txt`, `cli-plan-dry-run.txt`,
  `cli-plan-text-smoke.txt`
