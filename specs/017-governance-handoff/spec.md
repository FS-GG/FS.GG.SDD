# Feature Specification: Governance Readiness Handoff Contract

**Feature Branch**: `017-governance-handoff`

**Created**: 2026-06-20

**Status**: Draft

**Change Tier**: Tier 1 (contracted change: introduces the first explicit,
versioned, optional SDD-owned contract *consumed by* FS.GG.Governance. SDD already
emits advisory Governance-compatibility placeholders
(`GovernanceCompatibility`/`GovernanceCompatibilityFact`,
`optionalGovernanceBoundary`); this feature replaces those placeholders with a
concrete, schema-versioned **Governance handoff** projection emitted as a
generated readiness view, derived from the already-complete normalized work
model, declared evidence, and verify/ship readiness. It adds a new generated-view
schema and a new public surface; it adds no new lifecycle stage and does not
change the authored-source schemas. It introduces no rule evaluation, evidence
freshness, routing, profile, or gate enforcement behavior — those remain
FS.GG.Governance-owned.)

**Input**: User description: "Start the next item on the implementation plan."
Clarified by the user: "fs.gg.governance has made progress since last time; check
what has already been implemented and if that frees contingent phases." An audit
of the sibling `FS.GG.Governance` repository shows it has shipped the consumer
side that earlier SDD phases were waiting on: a typed evidence model with
synthetic-taint closure over a declared dependency DAG (F005), strict `.fsgg`
project/policy/capability/tooling schemas parsed into typed facts (F014),
deterministic path→capability routing (F015), git/CI snapshot facts (F016),
unknown-governed-path findings (F017), a typed gate registry (F018), route gate
selection (F019), and route JSON projection (F020). This unblocks the SDD-owned
integration-contract work deferred in `docs/initial-implementation-plan.md`
Phase 6 ("Define Governance effective-evidence inputs…", with SDD producing the
declarations) and Phase 7 ("Block stale generated views at the configured
Governance boundary", with SDD producing the inputs Governance enforces), and it
realizes the plan's standing promise that "SDD emits versioned readiness JSON
that Governance can inspect for routing, freshness, profiles, and enforcement."
Governance does not yet read SDD's `readiness/<id>/` outputs, and SDD's
Governance-facing surface is still advisory booleans; this feature defines and
emits the explicit, versioned handoff that closes that seam — while keeping SDD
fully usable with no Governance runtime installed, and keeping all rule
evaluation, freshness, routing, profile, and enforcement decisions in
FS.GG.Governance.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Project SDD Readiness Into a Versioned Governance Handoff (Priority: P1)

As a generated product that has adopted FS.GG.SDD and later added Governance, I
need each work item's SDD readiness — its normalized work model, declared
evidence, and verify/ship readiness — projected into a single explicit,
schema-versioned handoff artifact so that the Governance route/gate engine can
inspect a work item at the protected boundary without re-reading or
re-interpreting SDD's authored sources, and without SDD itself performing any
governance decision.

**Why this priority**: This is the thesis of the feature and the seam the user
asked to close. Governance has built its consumer surface (evidence model,
`.fsgg` facts, routing, gate registry, route JSON), but there is no SDD output it
can consume: SDD emits only advisory boolean compatibility hints. Without the
projected handoff, the optional Governance integration the whole product design
promises cannot be wired, and "add protected-boundary rigor later" stays
aspirational. Every other story refines, validates, or bounds this projection, so
it ships first.

**Independent Test**: Take a work item advanced through `ship` and confirm SDD
produces a deterministic, schema-versioned Governance handoff readiness view that
identifies its source artifacts and digests, the generator version, and the
declared facts a Governance consumer needs (declared evidence states and their
dependency edges, governed-boundary references, and merge-boundary readiness
disposition) — and that producing it required no Governance runtime, performed no
rule evaluation or freshness computation, and left every authored source
byte-identical.

**Acceptance Scenarios**:

1. **Given** a work item with a current normalized work model, declared evidence,
   and ship readiness, **When** the Governance handoff view is produced, **Then**
   it contains a declared schema version, the contributing source paths and their
   digests, the generator version, and the projected handoff facts, as
   deterministic JSON.
2. **Given** the same source tree produced twice, **When** the handoff view is
   produced each time, **Then** the two machine-readable handoff outputs are
   byte-identical.
3. **Given** no Governance policy/capability/tooling files and no Governance
   runtime present, **When** the handoff view is produced, **Then** it is produced
   successfully, marked as carrying declared SDD facts only, and notes the absence
   of Governance configuration without failing.

---

### User Story 2 - Carry Declared Evidence States and Dependency Edges, Not Computed Taint (Priority: P1)

As the Governance evidence model that computes synthetic-taint closure over a
dependency graph, I need the SDD handoff to supply each work item's *declared*
evidence states (real, synthetic, pending, failed, skipped, accepted deferral)
and the dependency edges between them in the shape my closure consumes, so that I
can compute effective evidence and propagate doubt myself — while SDD never
asserts an effective or tainted state it is not the owner of.

**Why this priority**: Governance's evidence model is explicitly defined over
*declared* states supplied to it; it reads no artifacts itself. SDD is the
component that already parses and normalizes `evidence.yml` declarations and the
task/evidence dependency structure, so SDD is the correct producer of those
declared inputs. This is co-equal P1 with Story 1 because the evidence projection
is the single highest-value fact Governance cannot obtain elsewhere, and getting
the ownership split wrong (SDD computing taint, or Governance re-parsing SDD
sources) would put the boundary in the wrong place.

**Independent Test**: Build a work item whose declared evidence includes real,
synthetic, pending, and deferred entries with dependency edges among them;
produce the handoff and confirm it carries each declared state and each
dependency edge verbatim, carries no computed effective/tainted state, and is
shaped so a Governance evidence-graph consumer can ingest it directly.

**Acceptance Scenarios**:

1. **Given** declared evidence with mixed real/synthetic/pending/failed/skipped
   and accepted-deferral states, **When** the handoff is produced, **Then** every
   declared state is carried verbatim with its stable identity.
2. **Given** evidence/task dependency relationships in the normalized work model,
   **When** the handoff is produced, **Then** the dependency edges are projected
   as the directed edges a dependency-graph consumer expects.
3. **Given** a synthetic evidence declaration with real dependents, **When** the
   handoff is produced, **Then** SDD reports the dependent as its declared real
   state and does **not** mark it tainted/auto-synthetic (taint closure is the
   consumer's computation, not SDD's).

---

### User Story 3 - Project Governed-Boundary and Routing References Without Deciding Routes (Priority: P2)

As the Governance routing and gate engine, I need the handoff to reference the
work item's changed/governed artifacts and the project's `.fsgg` capability and
policy pointers so that I can route paths to capabilities and select gates
myself, with SDD supplying the references but never selecting a route, profile, or
gate.

**Why this priority**: Governance now has deterministic path→capability routing,
a typed gate registry, and route JSON, but those operate over inputs (changed
paths, governed-boundary references, `.fsgg` pointers). SDD already tracks changed
artifacts and the optional governed-boundary references; surfacing them in the
handoff lets Governance route without re-deriving SDD state. It is P2 because
routing can proceed from `.fsgg` and git facts Governance already senses; the
handoff's references make routing cheaper and explicit rather than enabling it for
the first time.

**Independent Test**: Produce a handoff for a work item with changed artifacts and
declared governed-boundary references and confirm the handoff lists them as
references for a routing consumer, includes the project's `.fsgg`
policy/capability/tooling pointers when present, and contains no route, profile,
gate selection, or enforcement verdict of its own.

**Acceptance Scenarios**:

1. **Given** a work item with changed artifacts, **When** the handoff is produced,
   **Then** those artifacts are listed as routing references with stable
   identities.
2. **Given** a project with `.fsgg` policy/capability/tooling files present,
   **When** the handoff is produced, **Then** their pointers are referenced as
   available Governance configuration; **and** when absent, their absence is
   reported without failure.
3. **Given** any handoff, **When** it is inspected, **Then** it contains no
   selected route, profile, gate, severity verdict, or enforcement decision.

---

### User Story 4 - Carry Merge-Boundary Readiness as Advisory Facts, Not a Verdict (Priority: P2)

As CI and the Governance protected-boundary gate, I need the handoff to summarize
the SDD-owned merge-boundary readiness — the ship disposition, advisory/warning/
blocking diagnostic counts, per-view currency, and the blocking diagnostic ids —
as declared advisory facts, so that Governance can decide whether the boundary
passes while SDD never claims to have enforced the boundary itself.

**Why this priority**: SDD's `ship` already aggregates merge-boundary readiness
and points ship-ready work to the Governance handoff; the handoff is where that
readiness becomes a structured input Governance can act on. It is P2 because it
composes the already-computed ship readiness rather than introducing new
readiness logic.

**Independent Test**: Produce a handoff for both a ship-ready and a
ship-blocked work item and confirm each handoff carries the SDD ship disposition
and blocking diagnostic ids as advisory facts, and that neither handoff asserts a
pass/fail enforcement verdict.

**Acceptance Scenarios**:

1. **Given** a ship-ready work item, **When** the handoff is produced, **Then** it
   carries the ship disposition and zero blocking diagnostic ids as advisory
   readiness facts.
2. **Given** a ship-blocked work item, **When** the handoff is produced, **Then**
   it carries the blocking diagnostic ids and disposition without refusing to
   produce the handoff and without asserting an enforcement verdict.
3. **Given** either handoff, **When** it is read by a consumer, **Then** the SDD
   readiness facts are labeled as declared/advisory inputs to a Governance
   decision, not as the decision.

---

### User Story 5 - Detect Stale and Currency-Report the Handoff View (Priority: P3)

As a maintainer or CI job relying on the handoff, I need the handoff treated as a
generated view whose currency comes from regeneration — detectable as stale when
its declared sources change and brought back to currency by the existing refresh
path — so that a present handoff file is never mistaken for a current one.

**Why this priority**: The product rule that "generated views are outputs; their
presence is not proof of currency" applies to the handoff exactly as it does to
the work model and summary. It is P3 because it reuses the established
generated-view currency and refresh machinery rather than inventing new behavior.

**Independent Test**: Produce a handoff, modify a contributing source so digests
change, and confirm the handoff is reported stale; refresh and confirm it is
reported current with updated source digests.

**Acceptance Scenarios**:

1. **Given** a handoff produced from a source tree, **When** a contributing source
   changes, **Then** the handoff is reported stale against the changed source
   digests.
2. **Given** a stale handoff, **When** the work item's generated views are
   refreshed, **Then** the handoff is regenerated and reported current.
3. **Given** a missing handoff for a work item that has reached ship readiness,
   **When** currency is reported, **Then** the missing handoff is reported as a
   stale/absent generated view, not silently treated as current.

---

### Edge Cases

- **No Governance configuration at all**: the handoff is still produced (SDD is
  usable without Governance) and explicitly records that no `.fsgg`
  policy/capability/tooling configuration is present, rather than failing or
  fabricating Governance facts.
- **Partial/incomplete Governance configuration**: when some but not all `.fsgg`
  Governance files are present, the handoff references those present and reports
  the others absent, without blocking or changing any SDD command behavior.
- **Work item not yet at ship readiness**: when `ship` runs, the handoff is
  produced with its readiness disposition reflecting the incomplete stage and the
  blocking diagnostic ids (the same advisory-facts path as a ship-blocked item);
  it never asserts ship readiness that the lifecycle has not reached.
- **Conflicting prose vs structured evidence**: the handoff carries the structured
  declared facts (consistent with the normalized work model's structured-wins
  rule) and surfaces the existing consistency diagnostic rather than resolving the
  conflict itself.
- **Evidence dependency cycle**: SDD carries the declared edges as-is and surfaces
  the existing diagnostic; SDD does not attempt the closure or reject the cycle
  (cycle handling is the consumer's evidence-model concern).
- **Schema evolution**: a future change to the handoff schema is accompanied by an
  explicit version bump and migration note; older consumers can detect the
  version they do not support rather than misreading the document.
- **Governance schema drift**: if the Governance-side consumer schema the handoff
  targets changes, the mismatch is detectable through the declared handoff schema
  version; SDD does not silently track an unversioned Governance shape.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: SDD MUST produce a Governance handoff as a generated readiness view
  for a work item, derived from that work item's normalized work model, declared
  evidence, and verify/ship readiness, without reading or duplicating authored
  sources as a second source of truth.
- **FR-002**: The handoff MUST be schema-versioned and MUST carry an explicit
  declared schema version, an explicit migration posture for future changes, and a
  generator version.
- **FR-003**: The handoff MUST identify every contributing source artifact and its
  source digest so that staleness is detectable from sources and generator
  metadata rather than from file presence.
- **FR-004**: The handoff's machine-readable output MUST be deterministic and
  byte-stable for identical source trees, excluding implicit clocks, durations,
  host paths, ordering nondeterminism, and ANSI/terminal styling.
- **FR-005**: The handoff MUST carry each declared evidence state (real, synthetic,
  pending, failed, skipped, accepted deferral) verbatim with a stable identity, and
  MUST NOT compute, assert, or carry an effective or auto-synthetic/tainted state.
- **FR-006**: The handoff MUST project the evidence/task dependency relationships
  as directed dependency edges in the shape a dependency-graph evidence consumer
  ingests.
- **FR-007**: The handoff MUST reference the work item's changed/governed artifacts
  and the project's `.fsgg` policy/capability/tooling pointers (when present) as
  routing references, and MUST report their absence when not present, without
  selecting any route, profile, or gate.
- **FR-008**: The handoff MUST carry the SDD-owned merge-boundary readiness
  disposition and the advisory/warning/blocking diagnostic ids and per-view
  currency as declared advisory facts, labeled as inputs to a Governance decision
  rather than as an enforcement verdict.
- **FR-009**: SDD MUST NOT perform rule evaluation, evidence freshness computation,
  route or profile selection, gate selection, or protected-boundary enforcement as
  part of producing the handoff; those remain FS.GG.Governance-owned.
- **FR-010**: The handoff MUST be optional and additive: producing it MUST NOT
  require a Governance runtime, MUST NOT change the behavior or output of existing
  lifecycle commands when Governance configuration is absent, and MUST keep SDD
  fully usable without Governance installed.
- **FR-011**: The handoff MUST be produced successfully whether or not `.fsgg`
  Governance configuration is present, recording present configuration as
  references and absent configuration as explicitly absent.
- **FR-012**: The handoff MUST be treated as a generated view with currency
  semantics: it MUST be reported stale when its declared sources change, MUST be
  brought back to currency by the existing refresh path, and MUST be reported as
  absent/stale when missing rather than presumed current.
- **FR-013**: The handoff contract MUST supersede the existing advisory boolean
  Governance-compatibility placeholders by providing the concrete facts they
  approximated, and MUST keep human-readable projections (where rendered) as views
  over the same structured handoff facts, not an independent source of truth.
- **FR-014**: The handoff MUST be documented as an explicit, versioned, optional
  integration contract that is not active until a consuming Governance feature
  adopts it, consistent with the SDD/Governance integration boundary.
- **FR-015**: Authored sources MUST remain byte-identical across handoff
  production; producing or refreshing the handoff MUST only write the generated
  handoff view (and refresh-owned views), never an authored source.

### Key Entities *(include if feature involves data)*

- **Governance Handoff**: the generated readiness view for one work item; carries
  schema version, generator version, contributing source paths and digests, and
  the projected handoff facts; the explicit, versioned, optional contract consumed
  by FS.GG.Governance.
- **Declared Evidence Node Projection**: one declared evidence entry — stable
  identity plus declared state (real, synthetic, pending, failed, skipped, accepted
  deferral) — carried verbatim, with no computed/effective state.
- **Evidence Dependency Edge**: a directed relationship between evidence/task
  nodes, projected for a dependency-graph consumer to run its own closure over.
- **Governed-Boundary / Routing Reference**: a changed or governed artifact
  reference, and the `.fsgg` policy/capability/tooling pointers, supplied for
  Governance routing — never a selected route or gate.
- **Merge-Boundary Readiness Fact**: the SDD ship disposition, advisory/warning/
  blocking diagnostic ids, and per-view currency, carried as declared advisory
  inputs to a Governance decision.
- **Source Digest / Generator Version**: the provenance metadata that makes
  handoff staleness detectable from sources and generator identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Governance consumer can obtain every declared evidence state and
  dependency edge, governed-boundary/routing reference, and merge-boundary
  readiness fact for a work item from the single handoff artifact, without reading
  any SDD authored source.
- **SC-002**: Producing the handoff requires no Governance runtime and changes no
  existing lifecycle command's output when Governance configuration is absent
  (verified by an unchanged no-Governance lifecycle run).
- **SC-003**: The machine-readable handoff is byte-identical across two
  productions over an identical source tree.
- **SC-004**: 100% of evidence states carried in the handoff are declared states;
  no effective/tainted state is ever present in an SDD-produced handoff.
- **SC-005**: The handoff contains zero selected routes, profiles, gates, severity
  verdicts, or enforcement decisions (verified by inspection of the contract).
- **SC-006**: A change to any contributing source causes the handoff to be
  reported stale, and a refresh restores it to current — a present-but-stale
  handoff is never reported as current.
- **SC-007**: Authored sources are byte-identical before and after handoff
  production and refresh.

## Assumptions

- The next SDD-owned item is the SDD→Governance readiness handoff contract: an
  audit of the sibling `FS.GG.Governance` repo confirms it has shipped the
  consumer surface (F005 evidence model, F014 `.fsgg` typed facts, F015 routing,
  F016 snapshot, F017 unknown-path findings, F018 gate registry, F019 route gate
  selection, F020 route JSON), so the integration contract Phases 6–7 deferred is
  now designable against a concrete consumer. Governance-owned Phases 10–12 and
  the Governance portions of Phase 13 remain out of scope for this repository.
- The handoff is emitted as a dedicated generated readiness view under
  `readiness/<id>/` (a Governance handoff document), not embedded into existing
  readiness views, so existing readiness schemas stay stable; the precise filename
  and emitting command surface are a plan-level decision.
- The handoff is produced as part of the merge-boundary readiness step (`ship`)
  and regenerated by the existing cross-cutting `refresh` generator, consistent
  with how other SDD generated views are produced and refreshed; it adds no new
  lifecycle stage.
- The handoff always projects declared SDD facts and is safe to produce with no
  Governance configuration present; it is consumed only when a Governance feature
  adopts the versioned contract.
- The handoff targets the consumer shapes Governance has already built (declared
  evidence states over a dependency DAG, `.fsgg` capability/path routing inputs,
  and gate/route prerequisites); SDD tracks the targeted Governance consumer
  schema only through the handoff's own declared version, not by importing
  Governance code.
- Markdown remains an authoring surface and the schema-versioned handoff JSON is
  the machine contract; any human-readable rendering of the handoff is a
  projection over the same structured facts.
