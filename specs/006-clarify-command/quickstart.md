# Quickstart: Clarify Command

## Purpose

This guide describes the validation path for the `fsgg-sdd clarify` feature.
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

- `FS.GG.SDD.Artifacts` builds with any new clarification id and parser
  signatures declared before implementation bodies.
- `FS.GG.SDD.Commands` builds with public `.fsi` signatures before
  implementation bodies.
- `FS.GG.SDD.Cli` remains a thin executable host.
- Surface-baseline tests identify intentional public API changes.

## Run Clarify Workflow Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ClarifyCommand"
```

Expected outcome:

- An initialized project with a valid specification can create
  `work/<id>/clarifications.md`.
- The clarification artifact has valid structured front matter and standard
  sections.
- Successful reports identify `checklist` as the next lifecycle action when no
  blocking ambiguity remains.
- Reruns preserve authored prose, answers, decision ids, and accepted
  deferrals.
- Safe missing-section additions are reported precisely.
- Missing specification, missing answers, identity mismatches, unknown
  references, duplicate ids, malformed front matter, and unsafe decision
  changes block before authored writes.

## Run Command Workflow Boundary Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandWorkflow"
```

Expected outcome:

- `init` and `update` transitions remain pure.
- Clarify reads project, specification, clarification, and generated-view
  snapshots before planning writes.
- Blocking diagnostics prevent write effects.
- Dry-run mode suppresses filesystem mutation at the edge.
- The CLI or runner dispatches additional effects until a final report exists.

## Prove Generated-View Reporting

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedViewCommand"
```

Expected outcome:

- Clarify reports `readiness/<id>/work-model.json` state.
- Valid source data refreshes the work-model view when possible.
- Missing, stale, malformed, or blocked generated views produce diagnostics
  that name the source artifact to correct.
- Existing generated files are never treated as current just because they
  exist.

## Prove Deterministic Clarify Reports

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson"
```

Expected outcome:

- Three dry-run clarify executions over identical fixtures produce
  byte-identical JSON.
- Reports contain no timestamps, durations, absolute host paths, ANSI output,
  process ids, random ids, or nondeterministic ordering.
- Changed artifacts, parsed clarification facts, generated views, diagnostics,
  and Governance facts sort by documented keys.

## Prove Text Is A Projection

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~TextProjection"
```

Expected outcome:

- Plain text output is rendered from `CommandReport`.
- Text mode includes clarification question count, decision count, accepted
  deferral count, remaining ambiguity count, and blocking ambiguity count only
  when those facts are present in the authoritative report.
- Text mode introduces no facts absent from JSON.
- Terminal width and styling do not affect the authoritative report.

## Run Governance Boundary Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GovernanceBoundaryCommand"
```

Expected outcome:

- Clarify works when Governance files are absent.
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

- The prelude can construct a `clarify` command request.
- The public command workflow returns a model, effect list, diagnostics, and a
  command report.
- Output shows command name, outcome, changed artifact count, parsed
  clarification question count, decision count, accepted deferral count,
  generated-view count, blocking diagnostic count, and next action.

## Run A CLI Smoke Test

Use a disposable directory outside the repository.

```bash
tmp_dir="$(mktemp -d)"
dotnet run --project src/FS.GG.SDD.Cli -- init --root "$tmp_dir" --json
dotnet run --project src/FS.GG.SDD.Cli -- charter --root "$tmp_dir" --work 006-clarify-command --title "Clarify Command" --json
dotnet run --project src/FS.GG.SDD.Cli -- specify --root "$tmp_dir" --work 006-clarify-command --title "Clarify Command" --input "value: create a native clarify command
scope: one specified work item
requirement: create a clarification artifact with stable decisions
ambiguity: how should unanswered ambiguity advance to checklist?" --json
dotnet run --project src/FS.GG.SDD.Cli -- clarify --root "$tmp_dir" --work 006-clarify-command --input "AMB-001: create a blocking clarification question and record DEC-001 that checklist is next only after the ambiguity is answered." --json
dotnet run --project src/FS.GG.SDD.Cli -- clarify --root "$tmp_dir" --work 006-clarify-command --input "AMB-001: create a blocking clarification question and record DEC-001 that checklist is next only after the ambiguity is answered." --dry-run --json
dotnet run --project src/FS.GG.SDD.Cli -- clarify --root "$tmp_dir" --work 006-clarify-command --text
```

Expected outcome:

- `init` creates the minimum SDD skeleton without Governance.
- `charter` creates `work/006-clarify-command/charter.md`.
- `specify` creates `work/006-clarify-command/spec.md`.
- `clarify` creates `work/006-clarify-command/clarifications.md`.
- Dry-run rerun does not mutate disk and reports no unsafe decision change.
- Text mode reflects the same changed artifacts, parsed facts,
  generated-view state, diagnostics, and next action as JSON.
- All JSON reports follow
  [contracts/clarify-report-json.md](contracts/clarify-report-json.md).

## Full Suite

```bash
dotnet test FS.GG.SDD.sln
```

Expected outcome:

- Existing artifact-model and command tests remain green.
- Clarify tests add coverage without introducing Governance runtime
  requirements.

## Package Or Publish Checks

This feature does not define release distribution. Build and test evidence is
required; packaging or publishing behavior belongs to a later release-readiness
feature unless implementation tasks explicitly add a packable command library
surface.
