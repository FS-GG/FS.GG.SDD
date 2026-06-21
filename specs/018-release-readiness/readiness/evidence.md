# Validation evidence — 018 Release and Distribution Readiness

Captured 2026-06-21 from real runs (no synthetic evidence).

## Build & test (T026 / quickstart full suite)

```
dotnet build -c Release   →   0 Error(s)
dotnet test  -c Release    →   FS.GG.SDD.Artifacts.Tests : Passed 103 / Failed 0
                               FS.GG.SDD.Commands.Tests  : Passed 265 / Failed 0
                               Total: 368 passed, 0 failed
```

## SC-001 / FR-002, FR-003 — single version identity (Scenario 1)

`docs/release/release-readiness.json` `identity.version = 0.2.0`, `channel = preRelease`,
`cliCommandName = fsgg-sdd`; `compatibility[0]` = `{ sddVersionLine: 0.2.x,
specKitRange: ">=0.8.5", governanceContractVersionRange: 1.x }`. Equals the single
`<Version>` in `Directory.Build.props` and the generator version (asserted by
`ReleaseContractTests.T011`).

## SC-002 / FR-012 — coverage (Scenario 3)

`ReleaseReadinessCheckTests.T019` enumerates all 7 `GeneratedViewKind` + the
`--json` report; `evaluate` reports not-ready on a missing entry/baseline/source and
ready when complete.

## SC-003 / FR-015 — conformance (Scenario 3)

`ReleaseConformanceTests.T015`: a real lifecycle run (init…ship + agents + refresh)
produces 10 public outputs; `evaluate currentRelease produced = []` — every produced
artifact matches its documented catalog entry (no undocumented/absent field).

## SC-005 / FR-008 — determinism (Scenario 5)

`ReleaseDeterminismTests.T020`: serializing the contract twice is byte-identical;
`work-model.json`/`governance-handoff.json` are byte-identical across two productions
over one tree; the contract carries no ANSI/host-path/timestamp/duration.

## SC-006 / FR-009, FR-010 — migration obligation (Scenario 7)

This 0.2.0 release is **additive** (adds public surface, breaks no existing contract):
`migrations[]` is empty and no `docs/release/migrations/<version>.md` is required
(`ReleaseContractTests.T023`). The obligation + template live at
`docs/release/migrations/{README,TEMPLATE}.md`.

## SC-007 / FR-011 — CLI distribution (Scenario 6, partially manual)

Automated (`ReleaseInstallTests.T021`): `FS.GG.SDD.Cli.fsproj` carries
`<PackAsTool>true</PackAsTool>` + `<ToolCommandName>fsgg-sdd</ToolCommandName>`;
`--version` source equals the single `<Version>`.

Manual smoke (real binary):

```
$ dotnet src/FS.GG.SDD.Cli/bin/Release/net10.0/FS.GG.SDD.Cli.dll --version
0.2.0

$ dotnet pack src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -c Release
Successfully created package 'FS.GG.SDD.Cli.0.2.0.nupkg'
  <packageType name="DotnetTool" />   (tools/ present → .NET tool package)
```

The clean-environment `dotnet tool install` from a public registry is out of scope
(registry/signing are Governance/release-ops) and validated per
`docs/release/installation.md`, not as an automated gate.

## SC-008 / FR-014 — boundary exclusion (Scenario 8)

`ReleaseBoundaryTests.T024`: the contract carries no gate-logic vocabulary
(gate/route/profile/freshness/publish/provenance/verdict/enforce); a clean produced
handoff selects no route/profile/gate/verdict; the catalog adds no `GeneratedViewKind`
and no lifecycle command (`parseCommand "release" = Error`; `nextLifecycleCommand
Agents/Refresh = None`).
