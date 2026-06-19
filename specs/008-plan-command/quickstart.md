# Quickstart: Plan Command

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Repository root as the working directory.
- The active feature remains `specs/008-plan-command`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the plan
feature.

## Focused Tests

Run artifact-model tests that cover plan parsing and stable ids:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Plan"
```

Run command workflow tests that cover plan creation, rerun, blocked
prerequisites, deterministic reports, generated views, and text projection:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Plan"
```

Expected result: focused tests pass and record evidence for all valid and
blocked fixture families listed in [contracts/plan-fixtures.md](contracts/plan-fixtures.md).

## Full Test Suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all existing lifecycle command tests remain green and plan
tests pass.

## CLI Smoke Scenario

Create a disposable initialized SDD project and advance one work item through
checklist readiness with existing commands. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root>
```

Expected JSON result:

- `command` is `plan`;
- `outcome` is `succeeded` or `succeededWithWarnings`;
- `workId` is the selected work id;
- `changedArtifacts` includes `work/008-plan-command/plan.md`;
- `plan` summary includes decision, contract reference, verification
  obligation, migration note, and generated-view impact ids;
- `generatedViews` includes `readiness/008-plan-command/work-model.json` or a
  current diagnostic state;
- `nextAction.command` is `tasks`;
- no Governance route, freshness, profile, gate, audit, protected-boundary, or
  release verdict appears.

## Dry Run Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root> --dry-run
```

Expected result: the report contains planned authored and generated artifact
changes, but `work/008-plan-command/plan.md` and
`readiness/008-plan-command/work-model.json` are not mutated.

## Text Projection Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root> --text
```

Expected result: text output includes outcome, selected work id, changed
artifacts, plan decision count, contract reference count, verification
obligation count, accepted deferral count, stale decision count,
generated-view state, diagnostics, and next action. Every text fact is present
in the JSON report contract.

## Blocked Prerequisite Scenario

Run `plan` for a work item with a missing or failed checklist:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root>
```

Expected result: non-zero exit code, blocked outcome, missing or failed
checklist diagnostic, no plan artifact write, and next action pointing to the
artifact that needs correction.

## Public Surface Evidence

Record FSI or prelude evidence that:

- `SddCommand.Plan` parses from `plan`;
- `commandStage Plan` returns `plan`;
- `nextLifecycleCommand Plan` returns `Tasks`;
- plan summary and plan diagnostic contracts are visible through `.fsi`
  signatures before implementation bodies are completed.
