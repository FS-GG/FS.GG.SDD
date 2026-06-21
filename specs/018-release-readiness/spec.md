# Feature Specification: Release and Distribution Readiness

**Feature Branch**: `018-release-readiness`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item on the implementation plan."

> Resolved from `docs/initial-implementation-plan.md`: every SDD-owned lifecycle
> phase (artifact model, normalized work model, lifecycle commands, evidence,
> verify, ship, refresh, agent guidance, bootstrap/migration, and the Governance
> handoff contract) is shipped. The only remaining SDD-owned phase is **Phase 13:
> Release And Distribution Readiness** — its SDD package/CLI/schema slice. This
> feature delivers that slice. Governance-owned release rules and gate schemas
> remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Depend on a versioned SDD with a published compatibility contract (Priority: P1)

A consumer or downstream maintainer wants to adopt FS.GG.SDD as a versioned
dependency. They need to know how SDD versions its packages and CLI, what each
version number communicates about backward compatibility of public schemas and
command output, and which Spec Kit and FS.GG.Governance-contract versions a given
SDD release is compatible with — without reading source code or guessing.

**Why this priority**: A release is only safe to depend on when its versioning
meaning and compatibility surface are explicit. This is the heart of "release
readiness"; every other story builds on having a stable, declared version and
compatibility contract.

**Independent Test**: Can be fully tested by reading the published versioning
policy and compatibility matrix for the current release and confirming that, for
a given SDD version, the matrix states the supported Spec Kit version range and
the supported Governance handoff `contractVersion` range, and that the policy
unambiguously maps each kind of change (additive vs breaking schema/command/CLI
change) to a version-bump rule. Delivers value as a standalone, citable
compatibility contract even before schema reference docs exist.

**Acceptance Scenarios**:

1. **Given** a tagged SDD release, **When** a consumer inspects its declared
   version and compatibility record, **Then** they can determine the supported
   Spec Kit version range and the supported Governance handoff `contractVersion`
   range for that release.
2. **Given** the versioning policy, **When** a maintainer makes an additive,
   backward-compatible schema or command-output change, **Then** the policy
   prescribes a non-breaking (minor/patch) version bump and no migration note.
3. **Given** the versioning policy, **When** a maintainer makes a
   backward-incompatible public schema, command-output, or CLI-surface change,
   **Then** the policy prescribes a major version bump and requires a migration
   note before release.
4. **Given** two consecutive releases, **When** their declared version numbers
   differ, **Then** the difference is explainable solely from the documented
   bump rules applied to the set of public-contract changes between them.

---

### User Story 2 - Discover and rely on documented public artifact and command-output schemas (Priority: P2)

A consumer (human, CI, agent, or Governance integrator) wants an authoritative,
versioned reference describing the shape and stability of every public SDD
contract: the generated readiness views (`work-model.json`, `analysis.json`,
`verify.json`, `ship.json`, `governance-handoff.json`, `summary.md`, and the
`agent-commands/` outputs) and the deterministic command-output (`--json`)
reports. They want to build against these shapes confidently and be told which
fields are stable, which are additive-optional, and how each is versioned.

**Why this priority**: Documented, stability-classified schemas turn SDD's
already-deterministic outputs into a dependable integration surface. It depends
on the versioning policy (P1) to express stability guarantees but is independently
valuable as the reference an integrator reads.

**Independent Test**: Can be fully tested by confirming that each public generated
artifact and each `--json` command report has a corresponding schema reference
entry that names its `schemaVersion` (and `contractVersion` where applicable),
enumerates its fields with stability classification, and points back to the
authoritative structured contract. A reviewer can pick any public output and find
its documented schema.

**Acceptance Scenarios**:

1. **Given** the schema reference, **When** a consumer looks up any public
   generated readiness view, **Then** they find its schema version, field
   inventory, determinism guarantee, and stability classification.
2. **Given** a public `--json` command report, **When** a consumer looks it up,
   **Then** they find its documented output schema and the rule for how additions
   are versioned.
3. **Given** the schema reference and an actual produced artifact from a real
   lifecycle run, **When** they are compared, **Then** the produced artifact
   conforms to its documented schema (no undocumented public field, no documented
   field absent).

---

### User Story 3 - Catch accidental breaking changes to public surfaces before release (Priority: P2)

A maintainer changing SDD code wants automated protection against silently
altering a public schema, generated-view shape, or command-output contract. They
want locked baselines (golden fixtures over public schemas and representative
command/artifact output, alongside the existing public-`.fsi` surface baselines)
so that any change to a public contract fails fast and forces a conscious
decision: bump the version and write a migration note, or revert.

**Why this priority**: Documentation and versioning policy (P1/P2) are only
trustworthy if drift is mechanically detected. Baselines make the stability
contract enforceable rather than aspirational.

**Independent Test**: Can be fully tested by making a representative breaking
change to a public schema or command-output shape in a working copy and
confirming the baseline check fails with an actionable diff, and that an
intentional, properly versioned-and-documented change updates the baseline
cleanly. Delivers value as a regression guard independent of the prose docs.

**Acceptance Scenarios**:

1. **Given** locked public-contract baselines, **When** a change alters a public
   schema or command-output shape without updating the baseline, **Then** the
   verification fails with a diff identifying the changed contract.
2. **Given** an intentional, version-bumped, migration-noted contract change,
   **When** the baseline is regenerated, **Then** it updates deterministically
   and review shows only the intended surface change.
3. **Given** identical source inputs, **When** baselines are produced twice,
   **Then** they are byte-identical (no clock, host path, or ordering
   nondeterminism).

---

### User Story 4 - Install the CLI and migrate across breaking releases (Priority: P3)

A new user wants to install the `fsgg-sdd` CLI and a maintainer crossing a major
version wants a migration note explaining exactly what changed in public schemas
or commands and how to adapt. Installation guidance and migration notes complete
the distribution experience.

**Why this priority**: Necessary for a clean first-run and for safe upgrades, but
it builds on the version, schema, and baseline contracts established in P1–P3.

**Independent Test**: Can be fully tested by following the installation docs to
obtain and run the CLI in a clean environment, and by confirming that each
breaking release between two versions has a migration note describing the changed
public contracts and the required consumer action.

**Acceptance Scenarios**:

1. **Given** the installation docs, **When** a new user follows them in a clean
   environment, **Then** they can install and invoke the `fsgg-sdd` CLI through
   `ship` without prior FS.GG repository knowledge.
2. **Given** a major version change, **When** a consumer reads its migration note,
   **Then** they find each breaking public-contract change and the corresponding
   adaptation step.
3. **Given** a non-breaking (minor/patch) release, **When** a consumer checks for
   a migration note, **Then** none is required and the absence is consistent with
   the versioning policy.

---

### Edge Cases

- **No Governance installed**: The compatibility matrix must still state the
  supported Governance handoff `contractVersion` range as an *optional* integration
  fact; SDD release readiness must not require any Governance runtime to be
  present, and installation/use must succeed without it.
- **Schema version vs contract version divergence**: When a generated view's
  internal `schemaVersion` and its cross-repo `contractVersion` (e.g.
  `governance-handoff.json`) move independently, the schema reference and
  versioning policy must specify how each maps to an SDD package version bump.
- **Additive field added to a public artifact**: Policy must classify it as
  non-breaking, require no migration note, and require the schema reference and
  baseline to be updated in the same change.
- **Drift between docs and reality**: If the schema reference or compatibility
  matrix disagrees with an actually produced artifact or the real public surface,
  the structured/produced artifact is authoritative and the discrepancy must be a
  detectable failure, not a silent doc rot.
- **Pre-1.0 / initial release semantics**: The policy must state the bump meaning
  for the first released version line so early adopters are not surprised by
  breaking changes under an unstable major.
- **Unreleasable state**: If a public surface, schema, or command output lacks a
  documented schema entry or a locking baseline, the release readiness check must
  report it as not-ready rather than passing by absence.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The product MUST define and publish a versioning policy for the
  `FS.GG.SDD.*` packages and the `fsgg-sdd` CLI that maps each class of change —
  additive/backward-compatible vs backward-incompatible to a public schema,
  generated-view shape, command-output contract, or CLI surface — to a specific
  version-bump rule (major/minor/patch).
- **FR-002**: The product MUST publish a compatibility matrix that, for each SDD
  release line, states the supported Spec Kit version range and the supported
  FS.GG.Governance handoff `contractVersion` range, with the Governance
  compatibility recorded as an optional integration fact.
- **FR-003**: Each SDD release MUST carry a declared, machine-readable version
  identity for its packages and CLI such that a consumer can determine the
  release's version and its compatibility record deterministically without reading
  source code.
- **FR-004**: The product MUST provide an authoritative schema reference covering
  every public generated readiness view (`work-model.json`, `analysis.json`,
  `verify.json`, `ship.json`, `governance-handoff.json`, `summary.md`, and the
  `agent-commands/` outputs) and every public deterministic command-output
  (`--json`) report, including for each: schema version (and `contractVersion`
  where applicable), field inventory, determinism guarantee, and per-field or
  per-artifact stability classification.
- **FR-005**: The schema reference MUST identify, for each documented contract,
  the authoritative structured artifact it describes, so the documentation is a
  projection of the contracts and never a second source of truth.
- **FR-006**: The product MUST maintain locked baselines (golden fixtures) over
  public schemas and representative command-output and generated-artifact shapes,
  in addition to the existing public-`.fsi` surface baselines, such that any
  change to a public contract is detected.
- **FR-007**: A change to any public schema or command-output shape that is not
  reflected in its baseline MUST fail verification with an actionable diff naming
  the changed contract; an intentional change MUST update the baseline
  deterministically.
- **FR-008**: Baseline fixtures and any release-readiness output MUST be
  byte-stable for identical source inputs and MUST exclude implicit clocks,
  durations, host paths, ordering nondeterminism, and ANSI styling.
- **FR-009**: The product MUST require a migration note for any release that makes
  a backward-incompatible change to a public schema, generated-view shape,
  command-output contract, or CLI surface, and MUST NOT require one for additive,
  backward-compatible releases.
- **FR-010**: Each migration note MUST enumerate the breaking public-contract
  changes in the release and the corresponding consumer adaptation steps.
- **FR-011**: The product MUST provide installation and distribution
  documentation enabling a new user to install and run the `fsgg-sdd` CLI through
  the lifecycle (`init` … `ship`) in a clean environment with no prior FS.GG
  repository knowledge and no Governance runtime installed.
- **FR-012**: Release readiness MUST be verifiable: a check MUST report a public
  surface, schema, or command output as not-ready when it lacks a documented
  schema entry or a locking baseline, rather than passing by absence.
- **FR-013**: The release readiness contract MUST NOT introduce a new lifecycle
  stage and MUST NOT change the existing `charter … ship` chain, agent guidance,
  or authored-source schemas; it documents, versions, and locks existing public
  contracts.
- **FR-014**: The release readiness contract MUST remain entirely SDD-owned and
  MUST NOT define, compute, or enforce Governance-owned release gates,
  `fsgg release`/`fsgg verify` gate schemas, publish/provenance enforcement, route
  selection, evidence freshness, or protected-boundary verdicts.
- **FR-015**: When documentation (schema reference, compatibility matrix, or
  migration note) disagrees with an actually produced artifact or the real public
  surface, the produced/structured artifact MUST be authoritative and the
  discrepancy MUST be a detectable failure.

### Key Entities *(include if feature involves data)*

- **Package/CLI version identity**: The declared version of the `FS.GG.SDD.*`
  packages and `fsgg-sdd` CLI for a release, and its meaning under the versioning
  policy.
- **Versioning policy**: The rules mapping change classes (additive vs breaking,
  across schema/command/CLI surfaces) to version bumps and migration-note
  obligations.
- **Compatibility matrix entry**: A per-release record of supported Spec Kit
  version range and supported Governance handoff `contractVersion` range (optional
  integration fact).
- **Schema reference entry**: The documented shape, version, determinism
  guarantee, and stability classification of one public generated artifact or
  command-output contract, linked to its authoritative structured source.
- **Public-contract baseline**: A locked golden fixture over a public schema,
  generated-artifact shape, or command-output shape used to detect drift.
- **Migration note**: The per-release record of breaking public-contract changes
  and required consumer adaptation steps.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any released SDD version, a consumer can determine its
  supported Spec Kit range and supported Governance handoff `contractVersion`
  range from the published compatibility record without reading source code.
- **SC-002**: 100% of public generated readiness views and public `--json`
  command-output contracts have a schema reference entry with version, field
  inventory, determinism guarantee, and stability classification.
- **SC-003**: A produced artifact from a real lifecycle run conforms to its
  documented schema in 100% of public contracts checked — no undocumented public
  field and no documented field missing.
- **SC-004**: Any breaking change to a public schema or command-output shape is
  detected by the baseline check before release (the check fails on an
  unaccounted breaking change in 100% of tested cases) with an actionable diff.
- **SC-005**: Producing baselines and any release-readiness output twice over
  identical source inputs yields byte-identical results.
- **SC-006**: Every release that includes a backward-incompatible public-contract
  change has an accompanying migration note enumerating each breaking change and
  its adaptation step; no additive-only release requires one.
- **SC-007**: A new user can install and run the `fsgg-sdd` CLI through `ship` in
  a clean environment, with no prior FS.GG knowledge and no Governance runtime, by
  following the installation documentation.
- **SC-008**: No Governance-owned release-gate logic, route/profile/freshness/gate
  computation, or publish/provenance enforcement appears in any artifact produced
  by this feature (boundary-exclusion holds).

## Assumptions

- **Next item resolution**: "The next item on the implementation plan" is the
  SDD-owned slice of Phase 13 (Release And Distribution Readiness) in
  `docs/initial-implementation-plan.md`; all earlier SDD-owned phases and the 017
  Governance handoff contract are already shipped.
- **Versioning scheme**: Semantic versioning is the policy basis (major =
  backward-incompatible public-contract change, minor = additive, patch =
  clarifying/no-contract-change), consistent with the constitution's change
  classification, unless a release decision states otherwise.
- **CLI name stays `fsgg-sdd`**: The constitution's reserved CLI family name is
  used; no rename is in scope.
- **Distribution channel**: Installation guidance targets the standard .NET tool
  distribution path for an `FS.GG.SDD.*` CLI; the specific public registry/account
  setup and any signing/trusted-publishing enforcement are Governance/release-ops
  concerns and out of scope here (this feature documents the install/version/stability
  contract, not the publish pipeline).
- **Lifecycle surface is stable**: Phase 13 begins because the `charter … ship`
  lifecycle, refresh, agents, and Governance handoff surfaces are complete and
  deterministic, so their public contracts are ready to be frozen and documented.
- **Out of scope / deferred**: Governance `fsgg release`/`fsgg verify` gate
  schemas and exit codes, release enforcement rules (publish plans, trusted
  publishing, provenance), richer Spectre.Console human rendering, and scheduled
  exhaustive CI validation matrices are not delivered by this feature; the first
  three are Governance- or separately-owned, and the CI matrix is an operational
  follow-on.
- **No new authored-source schema**: This feature documents, versions, and locks
  existing public contracts; it does not add a new lifecycle stage or change
  authored-source shapes (additive version-identity/baseline machinery only).
