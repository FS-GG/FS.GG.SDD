# Quickstart / Validation Guide: feature 068

Runnable checks that prove each Success Criterion. All commands run from repo
root. The overarching guarantee is **byte-identity of every contract** — so most
checks are "run the suite" + "diff the pinned artifacts is empty".

## Prerequisites

```sh
dotnet build FS.GG.SDD.sln            # clean build, note warning count
dotnet test  FS.GG.SDD.sln            # baseline: 835 passed / 3 skipped (gated)
```

## SC-001 / SC-002 — readiness envelope, byte-identical (FR-001..003)

Before starting, capture the readiness golden state; after the envelope
extraction, confirm no view byte moved:

```sh
# the readiness views are exercised by these fact files
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~VerificationView|FullyQualifiedName~ShipView|FullyQualifiedName~Analysis"
# contract-diff gate: nothing pinned changed
git diff --stat -- 'src/**/*.fsi' '**/*.baseline'      # expect: empty
```

Structural check (SC-002): `analysis.json`/`verify.json`/`ship.json` are produced
through `writeReadinessEnvelope` (grep shows one frame, three thin callers):

```sh
grep -rn "writeReadinessEnvelope" src/FS.GG.SDD.Commands/CommandWorkflow/   # 1 def + 3 callers
```

## SC-003 — no stringly view/upgrade state (FR-004..005)

```sh
# no raw-string comparisons of these concepts remain in the three files
grep -nE '"(refreshed|blocked|stale|already-current|wouldApply|applied|skipped)"' \
  src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs \
  src/FS.GG.SDD.Commands/CommandWorkflow/HandlersUpgrade.fs \
  src/FS.GG.SDD.Commands/CommandWorkflow/Drift.fs
# expect: only inside the single toToken projection functions, nowhere else
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~Refresh|FullyQualifiedName~Upgrade|FullyQualifiedName~Drift"
```

## SC-004 — de-AutoOpen (FR-006)

```sh
grep -rn "AutoOpen" src/FS.GG.SDD.Commands/CommandWorkflow/    # only individually-justified survivors, if any
dotnet build FS.GG.SDD.sln                                     # builds; warning count NOT increased
```

## SC-005 — Parsing renames (FR-007)

```sh
grep -rn "ParsingEarly\|ParsingMid\|ParsingTasks" src/ tests/  # expect: 0 (outside git history)
dotnet build FS.GG.SDD.sln
```

## SC-006 — purity sites (FR-008)

```sh
# missing seeded-skill resource yields an actionable message, not TypeInitializationException
grep -n "failwithf" src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs   # expect: gone / replaced
grep -n "projectIdFromRoot" src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs
grep -n "intentional" src/FS.GG.SDD.Artifacts/LifecycleArtifacts/RegistryDocument.fs  # documenting comment present
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~SeededSkill"          # drift guard green
```

## SC-007 — CLAUDE.md == AGENTS.md (FR-009)

```sh
diff CLAUDE.md AGENTS.md && echo "IDENTICAL"          # expect: identical
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~AgentSurface|FullyQualifiedName~AgentsDocDrift"
# negative check: perturb one byte -> the guard fails
```

## SC-008 — global contract & suite gate (FR-010..011)

```sh
git diff --stat -- '**/*.baseline' 'src/**/*.fsi'     # expect: empty
dotnet test FS.GG.SDD.sln                             # 835+1 passed (new guard), 0 failed, warning count unchanged
```

## Definition of done

- Every command above behaves as annotated.
- `git diff` over baselines + `.fsi` + readiness/JSON golden fixtures is empty.
- Full suite green (including the new CLAUDE↔AGENTS guard); no new warnings.
