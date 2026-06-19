# Quickstart: Analyze Command

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Repository root as the working directory.
- The active feature remains `specs/010-analyze-command`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the
analyze feature.

## Focused Tests

Run artifact-model tests that cover analysis view shape, generated-view
currency, source relationships, and schema compatibility:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Analysis"
```

Run command workflow tests that cover analysis creation, rerun currency,
blocked prerequisites, cross-artifact findings, deterministic reports,
generated views, authored-source preservation, and text projection:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Analyze"
```

Expected result: focused tests pass and record evidence for all valid and
blocked fixture families listed in
[contracts/analysis-fixtures.md](contracts/analysis-fixtures.md).

## Full Test Suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all existing lifecycle command tests remain green and analyze
tests pass.

## CLI Smoke Scenario

Create a disposable initialized SDD project and advance one work item through
tasks-ready state with existing commands. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root>
```

Expected JSON result:

- `command` is `analyze`;
- `outcome` is `succeeded` or `succeededWithWarnings`;
- `workId` is the selected work id;
- `changedArtifacts` includes `readiness/010-analyze-command/analysis.json`
  when the generated analysis view is created or refreshed;
- `analysis` summary includes source count, source relationship count, ready
  finding count, advisory count, warning count, blocking count, stale source
  count, missing disposition count, generated-view finding count, readiness,
  and analysis artifact path;
- `generatedViews` includes `readiness/010-analyze-command/work-model.json`
  and `readiness/010-analyze-command/analysis.json` or current diagnostic
  states;
- `nextAction.actionId` is `analysis.next.implement` for
  implementation-ready state;
- no Governance route, freshness, profile, gate, audit, evidence freshness,
  protected-boundary, or release verdict appears.

## Rerun Current Scenario

Run the analyze command twice without changing any source artifacts:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root>
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root>
```

Expected result: authored lifecycle artifacts remain byte-identical, generated
analysis state is current or no-change, and readiness counts remain stable.

## Dry Run Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root> --dry-run
```

Expected result: the report contains proposed generated artifact changes, but
`readiness/010-analyze-command/work-model.json` and
`readiness/010-analyze-command/analysis.json` are not mutated. Authored sources
are not mutated.

## Text Projection Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root> --text
```

Expected result: text output includes outcome, selected work id, generated
analysis artifact, ready finding count, advisory count, warning count,
blocking count, stale source count, missing disposition count, generated-view
state, diagnostics, and next action. Every text fact is present in the JSON
report contract.

## Blocked Prerequisite Scenario

Run `analyze` for a work item with missing tasks or failed planning state:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root>
```

Expected result: non-zero exit code, blocked outcome, missing or failed
prerequisite diagnostic, no analysis view write, authored lifecycle artifacts
unchanged, and next action pointing to the artifact that needs correction.

## Consistency Defect Scenario

Prepare lifecycle sources with a stale plan decision, stale task, unknown
source reference, dependency cycle, missing task disposition, or completed task
without required evidence. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- analyze --work 010-analyze-command --root <temporary-project-root>
```

Expected result: blocked or needs-correction outcome, no authored mutation, an
actionable diagnostic naming the affected artifact or id, an analysis finding
for the consistency defect, and next action pointing to source or generated
view correction rather than implementation.

## Determinism Scenario

Run three dry-run analysis requests over identical inputs and compare the JSON
reports and generated analysis payloads that would be written.

Expected result: all three report payloads and proposed analysis payloads are
byte-identical and contain no absolute host paths.

## No-Governance Scenario

Run analysis in a valid initialized SDD project with `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` absent.

Expected result: analysis succeeds or reports only SDD lifecycle diagnostics,
and Governance compatibility facts are marked not evaluated without requiring
Governance installation.

## Public Surface Evidence

Record FSI or prelude evidence that:

- `SddCommand.Analyze` parses from `analyze`;
- `commandStage Analyze` returns `analyze`;
- `nextLifecycleCommand Tasks` returns `Analyze`;
- `nextLifecycleCommand Analyze` returns `None`;
- analysis summary, analysis finding, generated analysis view, and analysis
  diagnostic contracts are visible through `.fsi` signatures before
  implementation bodies are completed.
