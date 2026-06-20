# Quickstart: Agent Guidance Generation

## Prerequisites

- .NET SDK capable of building the repository target framework (`net10.0`).
- Repository root as the working directory.
- The active feature remains `specs/014-agent-guidance`.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected result: the solution builds with no warnings introduced by the
agent-guidance feature.

## Focused tests

Artifact-model tests covering the generated guidance manifest shape, derived
behavior model, source-digest currency, stale detection, and schema
compatibility:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~AgentGuidance"
```

Command workflow tests covering guidance generation, rerun currency,
authored-source preservation, missing/malformed configuration, no targets,
missing/stale/malformed work model, unknown references, malformed/stale generated
guidance, Claude/Codex divergence, dry-run, deterministic report, text
projection, and no-Governance behavior:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Agents"
```

## Full suite

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected result: all tests pass, including the new agent-guidance artifact and
command tests, with no regressions in the existing 258-test baseline.

## Manual validation (real filesystem)

In a disposable directory, initialize a project, author a minimal work item
through the lifecycle so a current work model exists, then generate guidance:

```bash
# 1. initialize and advance a work item until readiness/<id>/work-model.json exists
dotnet run --project src/FS.GG.SDD.Cli -- init --root .
# (charter … ship as needed to produce a current work model)

# 2. generate agent guidance (JSON)
dotnet run --project src/FS.GG.SDD.Cli -- agents --work <id> --root .

# 3. dry-run (no writes)
dotnet run --project src/FS.GG.SDD.Cli -- agents --work <id> --root . --dry-run

# 4. human-readable projection
dotnet run --project src/FS.GG.SDD.Cli -- agents --work <id> --root . --text
```

Expected outcomes:

- Step 2 creates `readiness/<id>/agent-commands/claude/` and
  `readiness/<id>/agent-commands/codex/`, each with a `guidance.json` manifest
  plus `commands.md` and `skills.md` projections, all marked as generated with
  source digests and generator identity. `disposition` is `generated-current`,
  `equivalenceRequired` is `true`, and `divergentTargetIds` is empty.
- Re-running step 2 with no source changes yields `NoChange` and writes nothing.
- Step 3 reports proposed generated changes and writes 0 files.
- Step 4 prints the same facts as the JSON report with no extra facts.
- Authored `.fsgg/agents.yml`, `CLAUDE.md`, `AGENTS.md`, and all
  `work/<id>/` sources remain byte-unchanged in every run.
- All steps succeed without Governance installed.

## Determinism check

Run the JSON generation three times over identical state and confirm the
generated manifests and report JSON are byte-identical:

```bash
dotnet run --project src/FS.GG.SDD.Cli -- agents --work <id> --root . > run1.json
dotnet run --project src/FS.GG.SDD.Cli -- agents --work <id> --root . > run2.json
dotnet run --project src/FS.GG.SDD.Cli -- agents --work <id> --root . > run3.json
diff run1.json run2.json && diff run2.json run3.json
```

Expected result: no differences.

## Contract references

- [contracts/agent-guidance-view.md](contracts/agent-guidance-view.md)
- [contracts/agents-command.md](contracts/agents-command.md)
- [contracts/agent-guidance-report-json.md](contracts/agent-guidance-report-json.md)
- [contracts/agent-guidance-fixtures.md](contracts/agent-guidance-fixtures.md)
- [data-model.md](data-model.md)
