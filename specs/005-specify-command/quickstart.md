# Quickstart: Specify Command

## Purpose

This guide describes the validation path for the `fsgg-sdd specify` feature.
It is written for the implementation phase after `$speckit-tasks` generates
the task list.

## Prerequisites

- .NET SDK with `net10.0` support
- Repository root as the working directory
- No Governance runtime installation required

## Validate The Public Surface

```bash
dotnet build FS.GG.SDD.sln
```

Expected outcome:

- `FS.GG.SDD.Artifacts` builds with any new specification id and parser
  signatures declared before implementation bodies.
- `FS.GG.SDD.Commands` builds with public `.fsi` signatures before
  implementation bodies.
- `FS.GG.SDD.Cli` remains a thin executable host.
- Surface-baseline tests identify intentional public API changes.

## Run Specify Workflow Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~SpecifyCommand"
```

Expected outcome:

- An initialized project with a valid charter can create
  `work/<id>/spec.md`.
- The specification has valid structured front matter and standard sections.
- Successful reports identify `clarify` as the next lifecycle action.
- Reruns preserve authored prose and stable ids.
- Safe missing-section additions are reported precisely.
- Missing charter, missing intent, identity mismatches, duplicate ids,
  malformed front matter, and unsafe overwrites block before authored writes.

## Run Command Workflow Boundary Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandWorkflow"
```

Expected outcome:

- `init` and `update` transitions remain pure.
- Specify reads project, charter, specification, and generated-view snapshots
  before planning writes.
- Blocking diagnostics prevent write effects.
- Dry-run mode suppresses filesystem mutation at the edge.
- The CLI or runner dispatches additional effects until a final report exists.

## Prove Generated-View Reporting

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedViewCommand"
```

Expected outcome:

- Specify reports `readiness/<id>/work-model.json` state.
- Valid source data refreshes the work-model view when possible.
- Missing, stale, malformed, or blocked generated views produce diagnostics
  that name the source artifact to correct.
- Existing generated files are never treated as current just because they
  exist.

## Prove Deterministic Specify Reports

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson"
```

Expected outcome:

- Three dry-run specify executions over identical fixtures produce
  byte-identical JSON.
- Reports contain no timestamps, durations, absolute host paths, ANSI output,
  process ids, random ids, or nondeterministic ordering.
- Changed artifacts, parsed specification facts, generated views, diagnostics,
  and Governance facts sort by documented keys.

## Prove Text Is A Projection

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~TextProjection"
```

Expected outcome:

- Plain text output is rendered from `CommandReport`.
- Text mode includes specification id counts and unresolved ambiguity count
  only when those facts are present in the authoritative report.
- Text mode introduces no facts absent from JSON.
- Terminal width and styling do not affect the authoritative report.

## Run Governance Boundary Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GovernanceBoundaryCommand"
```

Expected outcome:

- Specify works when Governance files are absent.
- Optional Governance pointers appear only as compatibility facts.
- SDD does not select routes, evaluate freshness, adjust profiles, select
  gates, emit protected-boundary verdicts, emit audit reports, or evaluate
  release policy.

## Exercise The API From FSI

```bash
dotnet build src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj -c Release
dotnet fsi scripts/prelude.fsx
```

Expected outcome:

- The prelude can construct a `specify` command request.
- The public command workflow returns a model, effect list, diagnostics, and a
  command report.
- Output shows command name, outcome, changed artifact count, parsed
  specification fact count, generated-view count, blocking diagnostic count,
  and next action.

## Run A CLI Smoke Test

Use a disposable directory outside the repository.

```bash
tmp_dir="$(mktemp -d)"
dotnet run --project src/FS.GG.SDD.Cli -- init --root "$tmp_dir" --json
dotnet run --project src/FS.GG.SDD.Cli -- charter --root "$tmp_dir" --work 005-specify-command --title "Specify Command" --json
dotnet run --project src/FS.GG.SDD.Cli -- specify --root "$tmp_dir" --work 005-specify-command --title "Specify Command" --input "value: create a native specify command
scope: one chartered work item
requirement: create a specification artifact with stable ids" --json
dotnet run --project src/FS.GG.SDD.Cli -- specify --root "$tmp_dir" --work 005-specify-command --title "Specify Command" --input "value: create a native specify command
scope: one chartered work item
requirement: create a specification artifact with stable ids" --dry-run --json
dotnet run --project src/FS.GG.SDD.Cli -- specify --root "$tmp_dir" --work 005-specify-command --title "Specify Command" --text
```

Expected outcome:

- `init` creates the minimum SDD skeleton without Governance.
- `charter` creates `work/005-specify-command/charter.md`.
- `specify` creates `work/005-specify-command/spec.md`.
- Dry-run rerun does not mutate disk and reports no unsafe overwrite.
- Text mode reflects the same changed artifacts, parsed facts,
  generated-view state, diagnostics, and next action as JSON.
- All JSON reports follow
  [contracts/specify-report-json.md](contracts/specify-report-json.md).

## Full Suite

```bash
dotnet test FS.GG.SDD.sln
```

Expected outcome:

- Existing artifact-model and command tests remain green.
- Specify tests add coverage without introducing Governance runtime
  requirements.

## Package Or Publish Checks

This feature does not define release distribution. Build and test evidence is
required; packaging or publishing behavior belongs to a later release-readiness
feature unless implementation tasks explicitly add a packable command library
surface.
