# Quickstart: Native SDD Lifecycle Commands

## Purpose

This guide describes the validation path for the native command feature. It is
written for the implementation phase after `$speckit-tasks` generates the task
list.

## Prerequisites

- .NET SDK with `net10.0` support
- Repository root as the working directory
- No Governance runtime installation required

## Validate The Public Surface

```bash
dotnet build FS.GG.SDD.sln
```

Expected outcome:

- `FS.GG.SDD.Artifacts` still builds.
- `FS.GG.SDD.Commands` builds with public `.fsi` signatures before
  implementation bodies.
- `FS.GG.SDD.Cli` builds as a thin executable host.
- Surface-baseline tests identify intentional public API changes.

## Run Command Workflow Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandWorkflow"
```

Expected outcome:

- `init` and `update` transitions are pure.
- File-changing commands produce explicit effects.
- Blocking diagnostics prevent write effects.
- Final command reports are produced for both successful and blocked commands.

## Run Initialization Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~InitCommand"
```

Expected outcome:

- Empty targets receive `.fsgg/project.yml`, `.fsgg/sdd.yml`,
  `.fsgg/agents.yml`, `work/`, and `readiness/`.
- Unrelated user files are preserved.
- Conflicting lifecycle files are reported before overwrite.
- Governance files are not required.

## Run Lifecycle Command Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~LifecycleCommand"
```

Expected outcome:

- A representative work item advances from charter through analysis.
- Each command creates or updates the expected authored artifact.
- Missing prerequisites, malformed artifacts, malformed work ids, and unknown
  references produce stable diagnostics.
- `analyze` emits consistency diagnostics and `analysis.json`.

## Prove Deterministic Command Reports

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson"
```

Expected outcome:

- Three dry-run executions over identical fixtures produce byte-identical JSON.
- Reports contain no timestamps, absolute host paths, ANSI output, or
  nondeterministic ordering.
- Changed artifacts, generated views, diagnostics, and Governance facts sort by
  documented keys.

## Prove Text Is A Projection

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~TextProjection"
```

Expected outcome:

- Plain text output is rendered from `CommandReport`.
- Text mode introduces no facts absent from JSON.
- Terminal width and styling do not affect the authoritative report.

## Run Generated-View Command Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedViewCommand"
```

Expected outcome:

- Valid lifecycle sources refresh `readiness/<id>/work-model.json`.
- `analyze` emits `readiness/<id>/analysis.json`.
- Missing, malformed, stale, or blocked views produce diagnostics that name the
  source artifact to fix.

## Run Governance Boundary Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GovernanceBoundaryCommand"
```

Expected outcome:

- Commands work when Governance files are absent.
- Optional Governance pointers appear only as compatibility facts.
- SDD does not select routes, evaluate freshness, adjust profiles, select
  gates, or emit protected-boundary verdicts.

## Exercise The API From FSI

```bash
dotnet build src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj -c Release
dotnet fsi scripts/prelude.fsx
```

Expected outcome:

- The prelude can construct a command request.
- The public command workflow returns a model, effect list, diagnostics, and a
  command report.
- Output shows command name, outcome, changed artifact count, generated-view
  count, blocking diagnostic count, and next action.

## Run A CLI Smoke Test

Use a disposable directory outside the repository.

```bash
tmp_dir="$(mktemp -d)"
dotnet run --project src/FS.GG.SDD.Cli -- init --root "$tmp_dir" --json
dotnet run --project src/FS.GG.SDD.Cli -- specify --root "$tmp_dir" --work 001-demo --title "Demo work" --json
dotnet run --project src/FS.GG.SDD.Cli -- analyze --root "$tmp_dir" --work 001-demo --json
```

Expected outcome:

- `init` creates the minimum SDD skeleton without Governance.
- `specify` creates the selected work item specification or reports the missing
  prerequisite that must be authored first.
- `analyze` reports generated-view currency and cross-artifact diagnostics.
- All JSON reports follow [contracts/command-report-json.md](contracts/command-report-json.md).

## Package Or Publish Checks

This feature does not define release distribution. Build and test evidence is
required; packaging or publishing behavior belongs to a later release-readiness
feature unless implementation tasks explicitly add a packable command library
surface.
