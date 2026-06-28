# Quickstart & Validation: Feature 038

Runnable validation scenarios proving the re-type and declared-command flow.
See [data-model.md](./data-model.md) and [contracts/](./contracts/) for shapes.

## Prerequisites

- .NET SDK with `net10.0` support.
- Repo restored: `dotnet restore FS.GG.SDD.sln`.

## Build & test (offline inner loop)

```sh
dotnet build FS.GG.SDD.sln -c Release
dotnet test  FS.GG.SDD.sln -c Release
```

Expected: the full solution builds with the single canonical `ProviderDescriptor`
(the local re-encoding deleted), and all tests pass offline. The network-gated
composition tests report **Skipped** (no `FSGG_SDD_ACCEPTANCE_REGISTRY`).

## Scenario A — One canonical type, zero regression (US1 / SC-001, SC-002)

1. After deleting `Config.ProviderDescriptor` / `Config.ProviderParameterSpec`, the
   solution compiles — confirming `parseProviderRegistry`, the scaffold handler,
   default-parameter resolution, and missing-required-parameter detection all bind
   to `Fsgg.Provider` types.
2. Run the existing scaffold + provenance test matrix
   (`FS.GG.SDD.Artifacts.Tests`, `FS.GG.SDD.Commands.Tests`,
   `FS.GG.SDD.Cli.Tests`). Expected: byte-identical `CommandReport` / scaffold
   summary / provenance / diagnostics (`scaffold.providerUnknown`,
   `scaffold.providerMissing`, unsupported-contract, missing-required-parameter).
3. Confirm a single definition remains: a repo search finds exactly one
   `ProviderDescriptor` and one `ProviderParameterSpec` (in `FS.GG.Contracts`).

## Scenario B — Registry reads the extended fields (US2 / SC-003)

Parse a synthetic registry declaring commands and a name parameter:

```yaml
schemaVersion: 1
providers:
  - name: extended
    contractVersion: "1.0.0"
    templateId: t
    source: s
    build:  { executable: dotnet, arguments: [build, -c, Release] }
    run:    { executable: dotnet, arguments: [run, --no-build] }
    nameParameter: projectName
```

Expected descriptor: `Build = Some { Executable = "dotnet"; Arguments = ["build";
"-c"; "Release"] }`, `Run = Some {...}`, `Test = None`, `Verify = None`,
`NameParameter = "projectName"`.

Parse a today-shape registry (no extended keys) → `Build/Test/Run/Verify = None`,
`NameParameter = "name"`.

Parse an entry with a blank executable
(`build: { executable: "   ", arguments: [x] }`) → `Build = None` (FR-005).

## Scenario C — Probes honor declared commands (US3 / SC-004, SC-005)

Offline (`ProbeResolutionTests`), using a **synthetic** `Fsgg.Provider` descriptor:

- `buildProbe None root` → resolves to `dotnet build` (default branch).
- `buildProbe (Some { Executable = "<trivial-exe>"; Arguments = [...] }) root` →
  invokes the trivial command, **never** starting a `dotnet` process (assert by
  observing the trivial command's deterministic exit, not a dotnet build).
- `runProbe` mirrors this for the declared `run` command under the same
  grace/overall window.

Network-gated (`CompositionAcceptanceTests`, requires `FSGG_SDD_ACCEPTANCE_REGISTRY`):

```sh
FSGG_SDD_ACCEPTANCE_REGISTRY=/path/to/real.providers.yml \
  dotnet test tests/FS.GG.SDD.Acceptance.Tests -c Release
```

Expected: with the reference provider (declares no `build`/`run`), the
`composition-acceptance-result` verdict is identical in pass/fail to the pre-change
harness (FR-009, SC-004).

## Scenario D — Provider-agnostic invariant holds (FR-011 / SC-006)

Run the T021a test (`acceptance project carries no Governance reference`). Expected:
PASS after adding the `FS.GG.Contracts` reference and `Fsgg.Provider` usage —
neither the package nor the namespace matches the scanned Governance/provider-identity
tokens.

## Done when

- [X] Solution builds with the local provider re-encoding removed.
- [X] Existing scaffold/provenance matrix passes byte-identically.
- [X] New parse tests (extended fields, defaults, blank executable) pass.
- [X] New probe-honors-declared tests pass offline without starting `dotnet` for
      the declared case.
- [X] T021a still passes.
