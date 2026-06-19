# Research: Normalized Work Model

## Decision: Extend `FS.GG.SDD.Artifacts` Instead Of Creating A New Package

**Rationale**: The artifact-model library already owns identifiers,
schema-version types, artifact references, diagnostics, generation manifests,
initial lifecycle artifact parsing, the first `WorkModel` shape, and
serialization. The normalized work-model feature is a direct maturation of that
contract, not a separate product boundary.

**Alternatives considered**:

- Create `FS.GG.SDD.WorkModel`: rejected because it would split the first public
  artifact contract before the API proves stable.
- Create a CLI project now: rejected because the spec explicitly excludes
  lifecycle authoring commands and command UX from this feature.

## Decision: Keep The Public Generator Pure Over File Snapshots

**Rationale**: The constitution requires MVU boundaries for stateful commands,
generators, validators, and external I/O workflows. This feature can avoid that
complexity by keeping the public library functions pure over
`LifecycleArtifacts.FileSnapshot` values and returning a generated output
record containing path, JSON bytes/text, digest, model, and diagnostics. Later
commands can own filesystem reads and writes behind an MVU boundary.

**Alternatives considered**:

- Add filesystem-reading and file-writing APIs: rejected for this feature
  because it would introduce I/O workflow behavior without the lifecycle command
  feature.
- Add an MVU wrapper now: rejected because there is no multi-step command
  state to model in this slice.

## Decision: Treat `readiness/<id>/work-model.json` As A Generated Output, Not Authority

**Rationale**: Markdown and structured files are authored sources. The generated
work model is automation truth only when it can be traced to current sources,
schema versions, generator version, and output digest. Existing generated files
must be checked for currency before use.

**Alternatives considered**:

- Trust a generated file if it exists: rejected because the roadmap explicitly
  says generated view presence is not proof of currency.
- Use filesystem timestamps: rejected because deterministic reports cannot
  depend on wall-clock or host metadata.

## Decision: Use Structured Graph Data For Execution And Preserve Prose As Context

**Rationale**: The constitution states that schema-versioned structured
artifacts are the machine contract. When prose and structured lifecycle data
disagree, the work model must preserve prose for human review but use the
structured graph for executable decisions and emit a diagnostic.

**Alternatives considered**:

- Let Markdown prose override structured fields: rejected because it would make
  automation non-deterministic and undermine the typed lifecycle contract.
- Drop conflicting prose from the model: rejected because users need visibility
  into the mismatch they must fix.

## Decision: Add Explicit Requirement Typing Diagnostics

**Rationale**: `requirementNotTyped` is required by the roadmap and catches the
most important Markdown-to-structured drift: a user-authored requirement exists
in prose but is absent from the normalized structured requirement set.

**Alternatives considered**:

- Treat all Markdown bullet requirements as typed automatically: rejected
  because it hides malformed or ambiguous requirement ids.
- Treat missing typed requirements as generic `workModelInconsistent`:
  rejected because the roadmap names `requirementNotTyped` as a distinct,
  actionable diagnostic.

## Decision: Classify Schema Versions Before Normalization

**Rationale**: Current, deprecated, unsupported, malformed, and future schema
versions need different user-facing behavior. Classification before
normalization lets the model preserve valid facts, warn on migration needs, and
block when the contract is unsafe.

**Alternatives considered**:

- Only support exact version `1`: rejected because it gives no migration path.
- Accept future versions silently: rejected because a future schema may change
  meaning in ways the current normalizer cannot safely interpret.

## Decision: Preserve Manual JSON Ordering With System.Text.Json

**Rationale**: The current package already uses explicit writer ordering, which
is the simplest way to guarantee stable property order and avoid reflection or
serializer-option surprises. Deterministic sorting rules are easier to test
when the serializer writes fields in contract order.

**Alternatives considered**:

- Reflection-based serialization: rejected because record field order and
  defaults are easier to accidentally change.
- External JSON libraries: rejected because the BCL is sufficient and keeps the
  package dependency surface small.

## Decision: Keep Governance As Optional Compatibility Facts

**Rationale**: SDD owns lifecycle artifacts, normalized work models, generated
SDD readiness views, and agent guidance contracts. Governance owns route
selection, profiles, evidence freshness, and protected-boundary enforcement.
This feature may surface optional Governance boundary paths but must not
interpret Governance policy or compute enforcement verdicts.

**Alternatives considered**:

- Parse Governance policy in this feature: rejected because that is Phase 2
  Governance-owned work.
- Hide Governance paths entirely: rejected because optional consumers need
  traceability to boundary artifacts without making them required.

## Decision: Fixture Coverage Drives The Implementation Slice

**Rationale**: The feature is a contract and normalization engine. Focused
fixtures provide executable evidence for valid input, conflict diagnostics,
stale generated models, schema migration behavior, deterministic JSON, and
optional Governance boundaries.

**Alternatives considered**:

- Only unit-test helper functions: rejected because it would not prove
  end-to-end work-model generation.
- Use synthetic in-memory data only: rejected because lifecycle artifacts are
  filesystem-shaped contracts and should be tested with representative fixture
  trees.
