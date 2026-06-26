# Contract: Internal module layout & compile order

This is the structural contract for the split. It is internal (no `.fsi`, no
public surface) but binding on the implementation so the result is navigable
(FR-001/FR-005) and compiles (FR-008).

## L-1 — Facade over child-namespace internal modules

- The facade `CommandWorkflow.fs` declares `module CommandWorkflow` in namespace
  `FS.GG.SDD.Commands`, `open`s `FS.GG.SDD.Commands.Internal`, and contains only
  `nextLifecycleEffects`, `init`, `update`.
- Every other moved binding lives in `[<AutoOpen>] module internal <Name>` in
  namespace `FS.GG.SDD.Commands.Internal`, under `src/FS.GG.SDD.Commands/CommandWorkflow/`.
- No internal module is given a `.fsi` (mirrors `LifecycleArtifacts/Internal.fs`).
- Each internal file redeclares, at its top, the artifact-namespace abbreviations
  it uses (`module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics`, and the
  other six as needed) — abbreviations are file-local in F#.

## L-2 — `.fsproj` compile order

`FS.GG.SDD.Commands.fsproj` `<Compile>` items, in order:

```xml
<Compile Include="CommandTypes.fsi" /><Compile Include="CommandTypes.fs" />
<Compile Include="CommandReports.fsi" /><Compile Include="CommandReports.fs" />
<Compile Include="CommandWorkflow/Foundation.fs" />
<Compile Include="CommandWorkflow/ParsingEarly.fs" />
<Compile Include="CommandWorkflow/ParsingMid.fs" />
<Compile Include="CommandWorkflow/ParsingTasks.fs" />
<Compile Include="CommandWorkflow/ViewGeneration.fs" />
<Compile Include="CommandWorkflow/Prerequisites.fs" />
<Compile Include="CommandWorkflow/HandlersEarly.fs" />
<Compile Include="CommandWorkflow/HandlersAnalyze.fs" />
<Compile Include="CommandWorkflow/HandlersEvidence.fs" />
<Compile Include="CommandWorkflow/HandlersVerify.fs" />
<Compile Include="CommandWorkflow/HandlersShip.fs" />
<Compile Include="CommandWorkflow/HandlersAgents.fs" />
<Compile Include="CommandWorkflow/HandlersRefresh.fs" />
<Compile Include="CommandWorkflow.fsi" />
<Compile Include="CommandWorkflow.fs" />
<Compile Include="CommandEffects.fsi" /><Compile Include="CommandEffects.fs" />
<Compile Include="CommandSerialization.fsi" /><Compile Include="CommandSerialization.fs" />
<Compile Include="CommandRendering.fsi" /><Compile Include="CommandRendering.fs" />
```

The internal-file order matches the monolith's top-to-bottom section order, so
F#'s definition-before-use holds with no forward references. The facade compiles
after all internals; the three sibling files keep their existing position and do
**not** `open FS.GG.SDD.Commands.Internal`, so they are unaffected.

## L-3 — File-size cap

No resulting `.fs` exceeds ~1,500 lines (soft cap; a single oversized binding may
overrun slightly). Target sizes are in `data-model.md`; the largest projected
file is ~1,020 lines.

## L-4 — Cohesion & naming

Each file is named for its concern or handler family; the twelve `compute*Plan`
handlers are locatable by file name without reading unrelated concerns (SC-005).
File/module names follow the table in `data-model.md`. The exact module names and
cut points are an implementation detail provided the cap, cohesion, and compile
order above hold.
