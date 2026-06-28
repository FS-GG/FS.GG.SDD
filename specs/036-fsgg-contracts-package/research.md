# Phase 0 Research: FS.GG.Contracts Package

All Technical-Context unknowns are resolved below. No open NEEDS CLARIFICATION remain.

## R1 — Canonical schema versions SDD emits today (FR-005, SC-001/002)

**Decision**: Every SDD-owned `.fsgg` schema is at major version `1` today; the package
exposes each as a named integer/string constant equal to that value. Two schemas also
carry a string contract version.

**Findings (grounded in `src/FS.GG.SDD.Artifacts`)**:

| Schema (contract name) | Owner | Emitted version today | Source of truth in code |
|------------------------|-------|-----------------------|-------------------------|
| `providers` (`.fsgg/providers.yml`) | SDD | `schemaVersion: 1` | `LifecycleArtifacts/Config.fs` (`parseProviderRegistry`) |
| `project` (`.fsgg/project.yml`) | SDD | `1` | `LifecycleArtifacts/Config.fs/.fsi` |
| `sdd` (`.fsgg/sdd.yml`) | SDD | `1` | `LifecycleArtifacts/Config.fs/.fsi` |
| `agents` (`.fsgg/agents.yml`) | SDD | `1` | `LifecycleArtifacts/Config.fs/.fsi` |
| `scaffold-provenance` (`.fsgg/scaffold-provenance.json`) | SDD | `SchemaVersion = 1` (int) | `ScaffoldProvenance.fs:68` |
| `governance-handoff` (`readiness/<id>/governance-handoff.json`) | SDD | `SchemaVersion = 1` (int) + `ContractVersion = "1.0.0"` | `GovernanceHandoff.fs:247-248` |
| `governance` | Governance | declared to published reference (assume `1`) | not emitted by SDD — see R6 |
| `policy` | Governance | declared to published reference (assume `1`) | not emitted by SDD — see R6 |
| `capabilities` | Governance | declared to published reference (assume `1`) | not emitted by SDD — see R6 |
| `tooling` | Governance | declared to published reference (assume `1`) | not emitted by SDD — see R6 |

**Rationale**: FR-005 requires the package's constant to equal the value SDD writes.
SDD writes `1` everywhere. Mirroring that exactly (and adding the `"1.0.0"` governance
contract version) keeps the "one fact in one place" invariant. The provider descriptor
also carries the provider **contract version** `"1.0.0"` (`HandlersScaffold.fs`).

**Alternatives considered**: Deriving versions reflectively from SDD at build time —
rejected: FR-001/FR-010 forbid the package referencing SDD; the constant must stand
alone. Re-modelling versions as the rich `SchemaVersion` record from SDD.Artifacts —
rejected: that type lives in SDD and would create a dependency; the package exposes
plain `int`/`string` constants (the actual on-the-wire value), keeping it BCL-only.

## R2 — BCL-only realization vs. YAML/JSON (FR-002, SC-004)

**Decision**: The package contains only typed records/DUs, module-level constants, and
pure functions. It performs **no** (de)serialization. Consumers parse YAML/JSON at their
existing edge (e.g. SDD.Artifacts' YamlDotNet reader) and hand the package already-built
models. The `.fsproj` references `FSharp.Core` only.

**Rationale**: A YAML/JSON dependency is the only realistic way third-party packages
creep into a contract library. Keeping (de)serialization at the consumer edge is exactly
the spec's stated BCL-only strategy (spec Assumptions + Edge Cases) and makes SC-004
mechanically verifiable.

**Alternatives considered**: Bundling a minimal JSON reader — rejected: re-introduces
serialization concerns the contract layer must not own. `System.Text.Json` (a BCL-ish
package, centrally versioned in the repo) — rejected: still a `PackageReference` in the
closure, violating the literal SC-004 closure check; unnecessary since the package never
parses.

## R3 — Module organization & namespace (spec Assumptions; constitution deviation)

**Decision**: One assembly `FS.GG.Contracts` (PackageId + assembly name) exposing three
public modules — `Schemas`, `Provider`, `Registry` — addressed as `Fsgg.Schemas`,
`Fsgg.Provider`, `Fsgg.Registry` per the item's mandated breakdown, plus a small
`ContractVersion` module. Each public module has a paired `.fsi`.

**Rationale**: The item explicitly names `Fsgg.Schemas`/`Fsgg.Provider`/`Fsgg.Registry`.
Using a `Fsgg` top-level namespace with these modules honours that while keeping the
distributable identity `FS.GG.Contracts`. The `FS.GG.SDD.*` namespace constant is
deliberately not applied (see plan Complexity Tracking) because this is an org-shared,
not SDD-scoped, contract.

**Alternatives considered**: `namespace FS.GG.Contracts` with modules `Schemas` etc. —
viable, but diverges from the item's literal `Fsgg.*` names; the chosen `Fsgg` namespace
matches the item text and is recorded as the surface baseline. Final namespace string is
a thin presentation choice over identical types and may be confirmed at implementation.

## R4 — Extended provider descriptor & declared-or-default commands (FR-006/007)

**Decision**: The package's provider descriptor is a superset of SDD's current
`ProviderDescriptor` (`Name`, `ContractVersion`, `TemplateId`, `Source`, `Parameters`)
plus four **optional** command declarations `Build`/`Test`/`Run`/`Verify`, each a
`{ Executable: string; Arguments: string list }` (the Feature 035 `DeclaredCommand`
shape), and a `NameParameter: string` that defaults to `"name"`. Absent command =
`None`; a declared-but-empty executable is representable and flagged malformed (not
silently treated as absent).

**Rationale**: This is the contract half of the Feature 035 (H1) declared-or-default
probe work, shaped for a 1:1 forward-compatible read in FS-GG/FS.GG.SDD#9. Existing
fields are preserved unchanged so the extension is additive (FR-006, Scenario 4). The
`name` default reproduces today's behaviour (FR-007, SC-003).

**Alternatives considered**: A single `Commands: Map<string,DeclaredCommand>` — rejected:
weaker typing, harder to expose "absent" per command, and diverges from the four named
probes. Reusing SDD's `DeclaredCommand` type directly — rejected: it lives in the test
harness, not a shared library; the package defines the canonical shape that #9/#35
re-type onto.

## R5 — Dependency-registry model & pure validator (FR-008/009, SC-007)

**Decision**: The package models `registry/dependencies.yml` as typed records — a set of
**components** (repo/package + declared version) and **dependency edges** (consumer →
provider with a declared compatible version range) — and exposes a pure
`validate : RegistryModel -> ValidationResult`. `ValidationResult` is success or a list
of diagnostics; each diagnostic names the offending **entry** and the **rule** violated
(`MissingField` of name, or `CoherenceViolation` describing the range/version mismatch).
Version-range compatibility is evaluated with a small BCL-only SemVer comparison (no
third-party SemVer package).

**Rationale**: FR-008/009 require typed model + pure validator + actionable diagnostics
that name entry and rule — directly enabling the H3 coherence workflow. Purity keeps
Principle V exempt (no MVU) and makes the validator trivially testable (SC-007).

**Alternatives considered**: Returning a `bool` — rejected: fails FR-009's "name the
entry and rule." Taking a third-party SemVer library — rejected: violates BCL-only; a
constrained `Major.Minor.Patch` comparator covers the registry's needs.

## R6 — Governance-owned schemas the package must still declare (spec Edge Cases / Assumptions)

**Decision**: The package declares typed shapes + version constants for the four
Governance-owned schemas (`governance`, `policy`, `capabilities`, `tooling`) even though
SDD never emits them, so Governance can later re-type onto one source. Their version
constants are set to the published-reference value; pending confirmation from the
Governance published schema reference, they are declared at `1` (the value every current
FS-GG schema uses), and the authoritative cross-check happens in the FR-013 registry
update.

**Rationale**: Spec Assumptions enumerate these as part of the package contract; omitting
them would force Governance to re-encode the same fact, defeating the backbone. Declaring
to the published reference (not to anything SDD emits) keeps SDD free of Governance
runtime identity (FR-014).

**Alternatives considered**: Excluding Governance schemas until Governance owns them —
rejected: SC-001 requires all 10 named schemas; the package is the shared source. Hard
SDD dependency on Governance to read its versions — rejected: violates FR-002/FR-014 and
SDD's "useful without Governance" constraint.

## R7 — Packing to a local feed (FR-011, SC-005)

**Decision**: `dotnet pack` produces `FS.GG.Contracts.<version>.nupkg`; a local
folder-based feed (a directory added as a NuGet source) satisfies "publish to the feed"
until the org feed lands in H4. A pack-and-consume test restores the package from the
local feed into a throwaway probe project and asserts the types resolve.

**Rationale**: FR-011 explicitly accepts a local feed for now. A folder feed needs no
infrastructure and proves the package is installable/consumable (SC-005).

**Alternatives considered**: Standing up a real feed (GitHub Packages/Azure Artifacts) —
out of scope, deferred to H4. `GeneratePackageOnBuild=true` — optional; pack-on-demand
matches the repo's existing manual-pack convention.

## R8 — Additive guarantee / byte-identical SDD (FR-010, SC-006)

**Decision**: SDD does **not** add a project reference to `FS.GG.Contracts`; only the new
library + test project + solution entries are added. The whole existing SDD test suite is
run unchanged as the regression gate; no existing artifact, golden fixture, or baseline is
edited.

**Rationale**: FR-010/SC-006 demand zero behavioural and zero byte change. The cleanest
proof is that nothing in `src/FS.GG.SDD.*` is touched and every existing test still passes.

**Alternatives considered**: Wiring SDD onto the package now — explicitly out of scope;
that is FS-GG/FS.GG.SDD#9.

## R9 — Self-describing contract version (FR-012)

**Decision**: Expose a `ContractVersion.value : string = "1.0.0"` (and structured
major/minor/patch) so a consumer can detect which contract surface it compiled against,
independent of NuGet package metadata.

**Rationale**: FR-012 requires the surface itself to report its version; NuGet metadata
is not readable from typed code without reflection. A plain constant is the single
authoritative self-report.
