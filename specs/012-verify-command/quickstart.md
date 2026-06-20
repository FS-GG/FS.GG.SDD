# Quickstart: Verify Command

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Repository root as the working directory.
- The active feature remains `specs/012-verify-command`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the verify
feature.

## Focused Tests

Run artifact-model tests that cover the verification view shape, task/evidence/
test/skill dispositions, generated-view currency, stale sources, and schema
compatibility:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Verification"
```

Run command workflow tests that cover verification view generation, rerun
currency, work-model/analysis refresh, blocked prerequisites, missing evidence,
missing required tests, missing required skills, invalid task graphs, unknown
references, undisclosed synthetic evidence, invalid deferrals, deterministic
reports, generated views, authored-source preservation, and text projection:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Verify"
```

Expected result: focused tests pass and record evidence for all valid and
blocked fixture families listed in
[contracts/verify-fixtures.md](contracts/verify-fixtures.md).

## Full Test Suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all existing lifecycle command tests remain green and verify
tests pass.

## CLI Smoke Scenario

Create a disposable initialized SDD project and advance one work item through
evidence-ready state with existing commands. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root>
```

Expected JSON result:

- `command` is `verify`;
- `outcome` is `succeeded` or `succeededWithWarnings`;
- `workId` is the selected work id;
- `changedArtifacts` includes `readiness/012-verify-command/verify.json` when the
  generated verification view is created or safely refreshed;
- `verification` summary includes finding counts, obligation count, evidence
  disposition counts, test disposition counts, skill visibility counts,
  readiness, and the verify artifact path;
- `generatedViews` includes `readiness/012-verify-command/work-model.json` and
  `readiness/012-verify-command/verify.json` or current diagnostic state;
- `nextAction.actionId` is `verify.next.ship` for verification-ready state;
- no Governance freshness, route, profile, gate, audit, protected-boundary, or
  release verdict appears.

## Rerun Current Scenario

Run the verify command twice without changing any source artifacts:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root>
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root>
```

Expected result: authored lifecycle artifacts remain byte-identical, the
generated verification view is current or no-change, and readiness counts remain
stable.

## Dry Run Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root> --dry-run
```

Expected result: the report contains proposed generated artifact changes, but
`readiness/012-verify-command/verify.json` and
`readiness/012-verify-command/work-model.json` are not mutated. Authored sources
are not mutated.

## Text Projection Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root> --text
```

Expected result: text output includes outcome, selected work id, verification
artifact path, ready finding count, advisory count, warning count, blocking
count, evidence disposition counts, test disposition counts, skill visibility
counts, generated-view state, diagnostics, and next action. Every text fact is
present in the JSON report contract.

## Blocked Prerequisite Scenario

Run `verify` for a work item without a current implementation-ready analysis
view or without evidence:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root>
```

Expected result: non-zero exit code, blocked outcome, missing or invalid
prerequisite diagnostic, no verification view write, authored lifecycle
artifacts unchanged, and next action pointing to analysis rerun, evidence
correction, or source correction.

## Verification Defect Scenario

Prepare lifecycle sources with completed tasks missing evidence, missing
required tests, missing required skills, stale source snapshots, unknown
references, dependency cycles, undisclosed synthetic evidence, or invalid
deferrals. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- verify --work 012-verify-command --root <temporary-project-root>
```

Expected result: blocked or needs-correction outcome, no authored mutation, an
actionable diagnostic naming the affected artifact or id, verification
dispositions for the defect, and next action pointing to evidence, task,
analysis, generated-view, missing-skill, or required-test correction rather than
ship.

## Determinism Scenario

Run three dry-run verify requests over identical inputs and compare the JSON
reports and proposed verification payloads.

Expected result: all three report payloads and proposed verification payloads
are byte-identical and contain no absolute host paths.

## No-Governance Scenario

Run verify in a valid initialized SDD project with `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` absent.

Expected result: verify succeeds or reports only SDD lifecycle diagnostics, and
Governance compatibility facts are marked not evaluated without requiring
Governance installation.

## Public Surface Evidence

Record FSI or prelude evidence that:

- `SddCommand.Verify` parses from `verify`;
- `commandStage Verify` returns `verify`;
- `nextLifecycleCommand Evidence` returns `Verify`;
- `nextLifecycleCommand Verify` returns `None` until a later ship feature adds
  that command;
- verification summary, verification finding, evidence disposition, required
  test disposition, skill visibility, verification diagnostic, and verification
  view contracts are visible through `.fsi` signatures before implementation
  bodies are completed.
