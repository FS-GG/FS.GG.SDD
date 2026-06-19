# Quickstart: Tasks Command

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Repository root as the working directory.
- The active feature remains `specs/009-tasks-command`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the tasks
feature.

## Focused Tests

Run artifact-model tests that cover task parsing, stable ids, graph
validation, and schema compatibility:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Task"
```

Run command workflow tests that cover task creation, rerun preservation,
blocked prerequisites, graph blockers, deterministic reports, generated views,
and text projection:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Task"
```

Expected result: focused tests pass and record evidence for all valid and
blocked fixture families listed in
[contracts/tasks-fixtures.md](contracts/tasks-fixtures.md).

## Full Test Suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all existing lifecycle command tests remain green and tasks
tests pass.

## CLI Smoke Scenario

Create a disposable initialized SDD project and advance one work item through
planned state with existing commands. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- tasks --work 009-tasks-command --root <temporary-project-root>
```

Expected JSON result:

- `command` is `tasks`;
- `outcome` is `succeeded` or `succeededWithWarnings`;
- `workId` is the selected work id;
- `changedArtifacts` includes `work/009-tasks-command/tasks.yml`;
- `tasks` summary includes task ids, dependency count, required skill count,
  required evidence count, skipped task count, stale task count, readiness,
  and source artifact paths;
- `generatedViews` includes `readiness/009-tasks-command/work-model.json` or
  a current diagnostic state;
- `nextAction.command` is `analyze`;
- no Governance route, freshness, profile, gate, audit, evidence freshness,
  protected-boundary, or release verdict appears.

## Rerun Preservation Scenario

Run the tasks command once, edit `work/009-tasks-command/tasks.yml` to mark one
task `inProgress` or `skipped` with a rationale, then rerun:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- tasks --work 009-tasks-command --root <temporary-project-root>
```

Expected result: existing task ids, statuses, owners, dependencies, required
skills, required evidence obligations, skip rationales, and user notes remain
unchanged. Compatible new source-derived tasks may be added without
renumbering existing tasks.

## Dry Run Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- tasks --work 009-tasks-command --root <temporary-project-root> --dry-run
```

Expected result: the report contains planned authored and generated artifact
changes, but `work/009-tasks-command/tasks.yml` and
`readiness/009-tasks-command/work-model.json` are not mutated.

## Text Projection Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- tasks --work 009-tasks-command --root <temporary-project-root> --text
```

Expected result: text output includes outcome, selected work id, changed
artifacts, task count, dependency count, required skill count, required
evidence count, skipped task count, stale task count, generated-view state,
diagnostics, and next action. Every text fact is present in the JSON report
contract.

## Blocked Prerequisite Scenario

Run `tasks` for a work item with a missing or failed plan:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- tasks --work 009-tasks-command --root <temporary-project-root>
```

Expected result: non-zero exit code, blocked outcome, missing or failed plan
diagnostic, no task artifact write, and next action pointing to the artifact
that needs correction.

## Graph Defect Scenario

Prepare `work/009-tasks-command/tasks.yml` with duplicate task ids, a
dependency cycle, an unknown source reference, or a completed task without
required evidence. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- tasks --work 009-tasks-command --root <temporary-project-root>
```

Expected result: blocked or needs-correction outcome, no unsafe mutation, an
actionable diagnostic naming the affected task or source id, and next action
pointing to task correction rather than analyze.

## Public Surface Evidence

Record FSI or prelude evidence that:

- `SddCommand.Tasks` parses from `tasks`;
- `commandStage Tasks` returns `tasks`;
- `nextLifecycleCommand Tasks` returns `Analyze`;
- task summary, task graph readiness, and task diagnostic contracts are
  visible through `.fsi` signatures before implementation bodies are
  completed.
