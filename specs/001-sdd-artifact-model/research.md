# Phase 0 Research: SDD Artifact Model

## Decision: Start with one packable artifact-model library

**Rationale**: The first feature must define the lifecycle artifact contract
before commands or generators exist. One `FS.GG.SDD.Artifacts` package keeps the
initial surface inspectable while still allowing internal modules for ids,
schema versions, artifact references, diagnostics, generation manifests,
normalized work models, and lifecycle rule contracts.

**Alternatives considered**: Multiple packages for Artifacts, WorkModel, and
LifecycleRules were deferred because the first slice has no independent release
or dependency reason to split packages. A CLI-first project was rejected because
the feature explicitly excludes lifecycle authoring commands.

## Decision: Use F# on `net10.0` with `.fsi` signatures first

**Rationale**: The constitution sets F# and `net10.0` as the default stack and
requires public visibility to live in `.fsi` files. The artifact model is a
Tier 1 contract, so signatures, surface baselines, FSI usage, tests, and docs
must move together.

**Alternatives considered**: A script-only model was rejected because this
feature needs stable APIs, schema contracts, fixtures, tests, and package
metadata. Implementing `.fs` bodies before signatures was rejected by the
constitution.

## Decision: Keep dependencies at the edge

**Rationale**: Core identifiers, diagnostics, generation manifests, and work
model types can use BCL and FSharp.Core. System.Text.Json is sufficient for
generated readiness JSON. YamlDotNet is allowed only for strict parsing of
authored `.yml` artifacts because project, SDD, agent, task, and evidence
contracts are YAML-shaped.

**Alternatives considered**: A broader serialization framework was rejected as
unnecessary. Hand-rolled YAML parsing was rejected because structured artifacts
need real parser diagnostics, not ad hoc string handling.

## Decision: Model ids, schema versions, digests, and diagnostics as typed values

**Rationale**: Stable lifecycle references are the foundation for tasks,
evidence, generated views, and optional Governance compatibility. Typed values
make duplicate ids, malformed ids, unknown references, unsupported schema
versions, and invalid digest formats explicit and testable.

**Alternatives considered**: Plain strings everywhere were rejected because they
make invalid states too easy to pass between layers. Globally unique opaque ids
were rejected for human-authored lifecycle artifacts because work ids,
requirement ids, task ids, and evidence ids need readable diagnostics.

## Decision: Structured graph data wins executable decisions

**Rationale**: Markdown remains the authoring surface, but schema-versioned
structured data is the machine contract. When prose and structured lifecycle
data disagree on status, dependency, ownership, required evidence, or reference
links, the normalized work model keeps the prose visible, uses the structured
graph for execution, and emits a consistency diagnostic.

**Alternatives considered**: Treating Markdown as authoritative was rejected
because it preserves ambiguity for tools. Failing all mismatches immediately was
rejected because contributors need actionable diagnostics during authoring
before stricter verify/ship behavior exists.

## Decision: Generated-view currency is based on manifests and digests

**Rationale**: Generated views must not pass just because files exist. Each
generated view contract records source artifacts, source digests, generator id,
generator version, schema version, output digest, and diagnostics. Staleness is
reported when any recorded source, schema, generator, or output identity no
longer matches.

**Alternatives considered**: Timestamp-based freshness was rejected because it
is not deterministic. Git status alone was rejected because generated views must
be checkable in unpacked fixture directories and CI artifacts.

## Decision: Expose Governance compatibility as contracts, not runtime dependency

**Rationale**: SDD owns lifecycle rule contracts and readiness facts. Governance
owns route selection, evidence freshness, profiles, protected-boundary
enforcement, and audit decisions. The first SDD feature therefore defines rule
contract shapes and optional boundary artifacts without referencing or requiring
the Governance runtime.

**Alternatives considered**: Depending on Governance packages now was rejected
because SDD must remain independently buildable and useful. Reimplementing
Governance behavior in SDD was rejected because it violates the product
boundary.

## Decision: Use xUnit, fixture directories, FSI evidence, and golden JSON

**Rationale**: xUnit runs cleanly through `dotnet test` and is enough for
schema, fixture, and snapshot-style assertions. FSI/prelude evidence proves the
public surface is usable before implementation hardens. Golden JSON fixtures
prove deterministic ordering and diagnostics.

**Alternatives considered**: No test framework was rejected because this Tier 1
feature requires automated evidence. Heavy integration harnesses were deferred
because the feature has no CLI, process runner, or external system.
