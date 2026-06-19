# Quickstart: Validate The SDD Artifact Model

This guide defines the validation path for the implementation tasks that follow
this plan. It is not a source-generation guide and does not include
implementation bodies.

## Prerequisites

- .NET SDK capable of targeting `net10.0`
- Standard Spec Kit workflow context for `specs/001-sdd-artifact-model`

## Expected Project Files After Implementation Tasks

```text
FS.GG.SDD.sln
src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj
tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj
tests/fixtures/sdd-artifact-model/
scripts/prelude.fsx
```

## Build And Test

```bash
dotnet restore FS.GG.SDD.sln
dotnet build FS.GG.SDD.sln --configuration Release
dotnet test FS.GG.SDD.sln --configuration Release
dotnet fsi scripts/prelude.fsx
```

Expected outcome:

- restore, build, tests, and FSI prelude all succeed;
- public modules compile through `.fsi` signatures;
- no test depends on FS.GG.Governance packages.

## Scenario 1: Valid Work Item

Run the artifact model tests over
`tests/fixtures/sdd-artifact-model/valid-work-item/`.

Expected outcome:

- project-level SDD files parse;
- work-item metadata, tasks, and evidence normalize;
- `readiness/<id>/work-model.json` shape can be emitted;
- zero blocking diagnostics are produced.

## Scenario 2: Invalid Fixtures

Run tests over these fixture directories:

- `malformed-schema-version`
- `missing-artifact`
- `duplicate-identifiers`
- `unknown-reference`
- `prose-structured-mismatch`
- `stale-generated-view`

Expected outcome:

- each fixture produces the diagnostic ids listed in
  [contracts/fixture-catalog.md](contracts/fixture-catalog.md);
- diagnostics name the affected artifact and correction;
- missing artifacts report required paths rather than failing as tool defects;
- prose/structured mismatch keeps prose visible and uses structured graph data
  for executable lifecycle decisions;
- stale generated views fail currency checks by digest and generator metadata,
  not by timestamps.

## Scenario 3: Deterministic JSON

Run the deterministic ordering test three consecutive times against
`tests/fixtures/sdd-artifact-model/deterministic-ordering/`.

Expected outcome:

- work-model JSON bytes are identical for all three runs;
- diagnostics are ordered by the contract in
  [contracts/work-model-json.md](contracts/work-model-json.md);
- no wall-clock timestamp or terminal formatting changes the output.

## Scenario 4: Governance Boundary Review

Run the Governance boundary tests.

Expected outcome:

- optional `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and
  `.fsgg/tooling.yml` are recognized as compatibility boundaries;
- SDD readiness can reference those files;
- SDD does not implement route selection, profile adjustment, evidence
  freshness, protected-boundary enforcement, or audit verdicts.

## Scenario 5: Artifact Traceability Walkthrough

Use the recorded artifact traceability review in
`specs/001-sdd-artifact-model/readiness/artifact-traceability.txt`.

Expected outcome:

- every modeled lifecycle artifact names its source of truth, generated view
  relationship, stale-view behavior, and diagnostic family;
- the walkthrough can be followed from the contract and fixture files without
  inspecting implementation internals.

## Scenario 6: Package Contract

After tests pass, pack the artifact library:

```bash
dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj --configuration Release
```

Expected outcome:

- a package for `FS.GG.SDD.Artifacts` is produced;
- package metadata identifies the first artifact-model contract version;
- the public surface baseline test remains current.
