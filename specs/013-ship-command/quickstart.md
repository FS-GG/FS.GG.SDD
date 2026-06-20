# Quickstart: Ship Command

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Repository root as the working directory.
- The active feature remains `specs/013-ship-command`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the ship
feature.

## Focused Tests

Run artifact-model tests that cover the ship view shape, aggregated lifecycle
readiness, ship-readiness disposition, generated-view currency, stale sources, and
schema compatibility:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Ship"
```

Run command workflow tests that cover ship view generation, rerun currency,
work-model/verification refresh, blocked prerequisites, missing or not-ready
verification, stale evidence, unknown references, undisclosed synthetic evidence,
invalid deferrals, deterministic reports, generated views, authored-source
preservation, and text projection:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Ship"
```

Expected result: focused tests pass and record evidence for all valid and blocked
fixture families listed in [contracts/ship-fixtures.md](contracts/ship-fixtures.md).

## Full Test Suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all existing lifecycle command tests remain green and ship tests
pass.

## CLI Smoke Scenario

Create a disposable initialized SDD project and advance one work item through
verification-ready state with existing commands. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root>
```

Expected JSON result:

- `command` is `ship`;
- `outcome` is `succeeded` or `succeededWithWarnings`;
- `workId` is the selected work id;
- `changedArtifacts` includes `readiness/013-ship-command/ship.json` when the
  generated ship view is created or safely refreshed;
- `ship` summary includes finding counts, the ship-readiness disposition,
  lifecycle stage readiness, verification readiness, evidence disposition counts,
  generated-view state, readiness, and the ship artifact path;
- `generatedViews` includes `readiness/013-ship-command/work-model.json`,
  `readiness/013-ship-command/verify.json`, and `readiness/013-ship-command/ship.json`
  or current diagnostic state;
- `nextAction.actionId` is `ship.next.protectedBoundary` for ship-ready state with
  a null command pointer;
- no Governance freshness, route, profile, gate, audit, protected-boundary, or
  release verdict appears.

## Rerun Current Scenario

Run the ship command twice without changing any source artifacts:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root>
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root>
```

Expected result: authored lifecycle artifacts and the verification view remain
byte-identical, the generated ship view is current or no-change, and readiness
counts remain stable.

## Dry Run Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root> --dry-run
```

Expected result: the report contains proposed generated artifact changes, but
`readiness/013-ship-command/ship.json` and
`readiness/013-ship-command/work-model.json` are not mutated. Authored sources and
the verification view are not mutated.

## Text Projection Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root> --text
```

Expected result: text output includes outcome, selected work id, ship artifact
path, ready finding count, advisory count, warning count, blocking count, the
ship-readiness disposition, lifecycle stage readiness, verification readiness,
evidence disposition counts, generated-view state, diagnostics, and next action.
Every text fact is present in the JSON report contract.

## Blocked Prerequisite Scenario

Run `ship` for a work item without a current verification-ready `verify.json`
view:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root>
```

Expected result: non-zero exit code, blocked outcome, missing or not-ready
verification diagnostic, no ship view write, authored lifecycle artifacts and the
verification view unchanged, and next action pointing to verification rerun,
evidence correction, or source correction.

## Ship Defect Scenario

Prepare lifecycle sources whose verification view reports unresolved blocking
findings, or where evidence or verification source digests are stale relative to
current sources. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- ship --work 013-ship-command --root <temporary-project-root>
```

Expected result: blocked or needs-correction outcome, no authored or verification
mutation, an actionable diagnostic naming the affected artifact or id, the
ship-readiness disposition for the defect, and next action pointing to
verification, evidence, generated-view, or stale-source correction rather than the
protected-boundary handoff.

## Determinism Scenario

Run three dry-run ship requests over identical inputs and compare the JSON reports
and proposed ship payloads.

Expected result: all three report payloads and proposed ship payloads are
byte-identical and contain no absolute host paths.

## No-Governance Scenario

Run ship in a valid initialized SDD project with `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` absent.

Expected result: ship succeeds or reports only SDD lifecycle diagnostics, and
Governance compatibility facts are marked not evaluated without requiring
Governance installation.

## Public Surface Evidence

Record FSI or prelude evidence that:

- `SddCommand.Ship` parses from `ship`;
- `commandStage Ship` returns `ship`;
- `nextLifecycleCommand Verify` returns `Ship`;
- `nextLifecycleCommand Ship` returns `None` because the protected-boundary
  handoff is Governance-owned rather than another SDD command;
- ship summary, ship-readiness finding, ship-readiness disposition, ship
  diagnostic, and ship view contracts are visible through `.fsi` signatures before
  implementation bodies are completed.
