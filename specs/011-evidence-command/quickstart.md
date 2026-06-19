# Quickstart: Evidence Command

## Prerequisites

- .NET SDK capable of building the repository target framework.
- Repository root as the working directory.
- The active feature remains `specs/011-evidence-command`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the
evidence feature.

## Focused Tests

Run artifact-model tests that cover evidence artifact shape, evidence
declarations, obligation dispositions, stale source snapshots, generated-view
currency, and schema compatibility:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Evidence"
```

Run command workflow tests that cover evidence creation, rerun currency,
compatible updates, unsafe update refusal, blocked prerequisites, missing
evidence, deterministic reports, generated views, authored-source
preservation, and text projection:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Evidence"
```

Expected result: focused tests pass and record evidence for all valid and
blocked fixture families listed in
[contracts/evidence-fixtures.md](contracts/evidence-fixtures.md).

## Full Test Suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all existing lifecycle command tests remain green and evidence
tests pass.

## CLI Smoke Scenario

Create a disposable initialized SDD project and advance one work item through
analyzed implementation-ready state with existing commands. After implementation
work has produced supporting output, run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root> --input <evidence-declaration-text>
```

Expected JSON result:

- `command` is `evidence`;
- `outcome` is `succeeded` or `succeededWithWarnings`;
- `workId` is the selected work id;
- `changedArtifacts` includes `work/011-evidence-command/evidence.yml` when
  authored evidence is created or safely updated;
- `evidence` summary includes declaration count, obligation count, supported
  count, deferred count, missing count, stale count, synthetic count, invalid
  count, blocking count, readiness, and evidence artifact path;
- `generatedViews` includes `readiness/011-evidence-command/work-model.json`
  or current diagnostic state;
- `nextAction.actionId` is `evidence.next.verify` for evidence-ready state;
- no Governance freshness, route, profile, gate, audit, protected-boundary, or
  release verdict appears.

## Rerun Current Scenario

Run the evidence command twice without changing any source artifacts:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root> --input <evidence-declaration-text>
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root> --input <same-evidence-declaration-text>
```

Expected result: prerequisite lifecycle artifacts remain byte-identical,
existing evidence ids and meaning are preserved, generated work-model state is
current or no-change, and readiness counts remain stable.

## Dry Run Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root> --input <evidence-declaration-text> --dry-run
```

Expected result: the report contains proposed authored and generated artifact
changes, but `work/011-evidence-command/evidence.yml` and
`readiness/011-evidence-command/work-model.json` are not mutated.
Prerequisite sources are not mutated.

## Text Projection Scenario

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root> --text
```

Expected result: text output includes outcome, selected work id, evidence
artifact path, declaration count, obligation count, supported count, deferred
count, missing count, stale count, synthetic count, invalid count, blocking
count, generated-view state, diagnostics, and next action. Every text fact is
present in the JSON report contract.

## Blocked Prerequisite Scenario

Run `evidence` for a work item without a current implementation-ready analysis
view:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root>
```

Expected result: non-zero exit code, blocked outcome, missing or invalid
analysis prerequisite diagnostic, no evidence artifact write, authored
lifecycle artifacts unchanged, and next action pointing to analysis rerun or
source correction.

## Evidence Defect Scenario

Prepare lifecycle sources with completed tasks missing evidence, stale source
snapshots, unknown references, duplicate evidence ids, undisclosed synthetic
evidence, missing deferral rationale, or unsafe update attempts. Then run:

```bash
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- evidence --work 011-evidence-command --root <temporary-project-root>
```

Expected result: blocked or needs-correction outcome, no unsafe authored
mutation, an actionable diagnostic naming the affected artifact or id, evidence
dispositions for the defect, and next action pointing to evidence, task,
analysis, or generated-view correction rather than verify.

## Determinism Scenario

Run three dry-run evidence requests over identical inputs and compare the JSON
reports and proposed evidence payloads.

Expected result: all three report payloads and proposed evidence payloads are
byte-identical and contain no absolute host paths.

## No-Governance Scenario

Run evidence in a valid initialized SDD project with `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` absent.

Expected result: evidence succeeds or reports only SDD lifecycle diagnostics,
and Governance compatibility facts are marked not evaluated without requiring
Governance installation.

## Public Surface Evidence

Record FSI or prelude evidence that:

- `SddCommand.Evidence` parses from `evidence`;
- `commandStage Evidence` returns `evidence`;
- `nextLifecycleCommand Analyze` returns `Evidence`;
- `nextLifecycleCommand Evidence` returns `None` until a later verify feature
  adds that command;
- evidence summary, evidence declaration, evidence disposition, evidence
  diagnostic, and evidence report contracts are visible through `.fsi`
  signatures before implementation bodies are completed.
