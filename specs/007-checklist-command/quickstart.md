# Quickstart: Checklist Command

This guide validates the `fsgg-sdd checklist` feature after implementation.
It references the contracts in `contracts/` and the model in `data-model.md`
instead of duplicating implementation details.

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Working directory at the repository root.
- The feature has completed implementation tasks generated from this plan.

## Restore And Build

```bash
dotnet restore FS.GG.SDD.sln
dotnet build FS.GG.SDD.sln -c Release --no-restore
```

Expected outcome:

- Restore succeeds.
- Release build succeeds.
- Public `.fsi` surface changes are intentional and covered by baseline tests.

## Focused Artifact Tests

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --no-build --filter "FullyQualifiedName~Checklist"
```

Expected outcome:

- Checklist front matter parses with `schemaVersion: 1`.
- `CHK-###` item ids and `CR-###` result ids validate and remain stable.
- Malformed schema, duplicate ids, unknown references, and stale source
  snapshots produce stable diagnostics.

## Focused Command Tests

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --no-build --filter "FullyQualifiedName~Checklist"
```

Expected outcome:

- `checklist-create` creates `work/<id>/checklist.md`.
- `checklist-rerun-preserves-results` preserves authored results and stable
  ids.
- `checklist-adds-missing-items` appends safe missing items.
- `checklist-stale-result` marks affected results stale when source snapshots
  change.
- `failed-requirements-quality` writes failed checklist results and points to
  correction rather than `plan`.
- `missing-clarification`, `unresolved-ambiguity`, `malformed-checklist`,
  `unknown-source-reference`, and `unsafe-checklist-result-change` block unsafe
  writes with actionable diagnostics.

## Generated View Tests

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --no-build --filter "FullyQualifiedName~GeneratedView"
```

Expected outcome:

- Checklist sources are included in generated work-model state when valid.
- Missing, stale, malformed, and blocked generated-view states are
  distinguished.
- Existing generated files are not treated as current solely because they
  exist.

## Report And Text Projection Tests

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --no-build --filter "FullyQualifiedName~CommandReportJson|FullyQualifiedName~TextProjection"
```

Expected outcome:

- Three identical dry-run checklist executions produce byte-identical JSON.
- JSON report ordering follows `contracts/checklist-report-json.md`.
- Text output includes only facts present in the command report.
- Human summaries expose changed artifact count, checklist item count, passed
  count, failed blocking count, accepted deferral count, stale result count,
  generated-view state, diagnostic count, and next action.

## CLI Smoke Scenario

Use a disposable directory and run the native lifecycle through checklist:

```bash
tmpdir="$(mktemp -d)"
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- init --root "$tmpdir"
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- charter --root "$tmpdir" --work 007-checklist-command --title "Checklist Command" --input "Define the checklist command boundaries."
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- specify --root "$tmpdir" --work 007-checklist-command --title "Checklist Command" --input "Create requirements-quality checklist readiness before planning."
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- clarify --root "$tmpdir" --work 007-checklist-command --input "Resolve blocking ambiguity with explicit checklist readiness decisions."
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- checklist --root "$tmpdir" --work 007-checklist-command --text
```

Expected outcome:

- `work/007-checklist-command/checklist.md` exists in the disposable project.
- The text projection reports checklist outcome, findings, generated-view
  state, and next action from the authoritative command report.
- If no blocking findings remain, next action is `plan`.
- If quality findings remain, next action is correction and the checklist
  artifact records failed results.
- No Governance runtime is required.

## Dry-Run Mutation Check

```bash
tmpdir="$(mktemp -d)"
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- init --root "$tmpdir"
# Create charter, specification, and clarification state as in the smoke scenario.
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- checklist --root "$tmpdir" --work 007-checklist-command --dry-run
test ! -f "$tmpdir/work/007-checklist-command/checklist.md"
```

Expected outcome:

- Dry-run reports proposed authored and generated changes.
- Dry-run does not create or modify `checklist.md` or generated readiness
  views.

## No-Governance Boundary Check

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --no-build --filter "FullyQualifiedName~GovernanceBoundary"
```

Expected outcome:

- Absent Governance files do not block checklist creation or update.
- Present Governance pointers appear only as compatibility facts.
- No route, profile, freshness, gate, protected-boundary, audit, or release
  verdict is produced.

## Full Suite

```bash
dotnet test FS.GG.SDD.sln -c Release --no-build
```

Expected outcome:

- All artifact, command, CLI smoke, surface-baseline, deterministic-report,
  generated-view, text-projection, and no-Governance tests pass.
- Readiness evidence for build, focused tests, full suite, FSI/prelude, CLI
  smoke, performance, artifact traceability, human summary review, and
  SDD/Governance boundary review is recorded under
  `specs/007-checklist-command/readiness/`.
