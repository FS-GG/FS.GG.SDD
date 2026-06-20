# Phase 0 Research: Governance Readiness Handoff Contract

All planning unknowns are resolved against the live state of both repositories
(`FS.GG.SDD` producer surfaces and the sibling `FS.GG.Governance` consumer
surfaces). No `NEEDS CLARIFICATION` markers remain.

## D1 — Dedicated handoff artifact vs. promoting existing views

- **Decision**: Emit a dedicated `readiness/<id>/governance-handoff.json`
  generated view (option A), rather than declaring SDD's internal
  `work-model.json`/`ship.json` to be the consumer contract (option B).
- **Rationale**: `work-model.json` is SDD's internal normalized view and changes
  with essentially every lifecycle feature. Making it the external contract would
  couple Governance to SDD internals and forbid SDD from evolving its model — the
  opposite of CLAUDE.md's "explicit, versioned, optional contract." A purpose-built
  projection is a narrow, independently versioned surface; because the underlying
  data already exists in the work model, the projection is a cheap pure fold, so A
  costs little while preserving the boundary.
- **Alternatives considered**: B (read existing views) — rejected for coupling;
  embedding handoff fields into `ship.json` — rejected because it would churn an
  existing schema and conflate SDD-internal readiness with the cross-repo contract.

## D2 — Evidence state mapping (SDD → Governance declared `EvidenceState`)

- **Decision**: Map `WorkModel.EvidenceEntry` to the five **declared** kernel
  states (`pending`/`real`/`synthetic`/`failed`/`skipped`) per the table in
  [contracts/integration-requirements.md](contracts/integration-requirements.md);
  `Synthetic = true` dominates to `synthetic`; `stale` maps to its base state plus
  a `staleEvidence` diagnostic. Never emit `autoSynthetic`.
- **Rationale**: `Kernel.Evidence` defines exactly six states with `AutoSynthetic`
  computed-only (`build` returns `AutoSyntheticDeclared` if declared). Tokens match
  `Kernel.Json` (`"pending"/"real"/"synthetic"/"failed"/"skipped"/"autoSynthetic"`).
  Staleness is a Governance freshness concern (`Kernel.Freshness`), so SDD reports
  the declared base state and a diagnostic rather than inventing a token.
- **Alternatives considered**: emitting an effective/tainted state from SDD —
  rejected; that is the consumer's `effective` closure and would violate FR-005.
- **Open coordination point**: `deferred → skipped` vs `pending` is flagged in the
  contract for Governance review; it is a one-row change behind `contractVersion`.

## D3 — Evidence dependency-graph node identity and edges

- **Decision**: Namespaced string ids (`evidence:<id>`, `task:<id>`) over a unified
  node set of evidence entries plus the tasks they rest on; edges derived from
  `EvidenceEntry.TaskRefs`, `TaskEntry.Dependencies`, and `TaskEntry.RequiredEvidence`,
  with task node state from `TaskEntry.Status`.
- **Rationale**: `Evidence.build` takes generic `'id: comparison` nodes and
  `(dependent, dependency)` edges; string ids satisfy `comparison`. Including tasks
  lets the taint closure flow end-to-end, mirroring Governance's F10 dogfood adapter
  over `TaskDependsOn`. Namespacing prevents evidence/task id collisions.
- **Alternatives considered**: evidence-only nodes — rejected, would drop the task
  topology the closure needs; numeric ids — rejected, less debuggable and not
  stable across runs.

## D4 — Routing references vs. Governance git sensing

- **Decision**: Emit `governedReferences` (normalized governed/changed paths from
  `GovernanceBoundaries` + `ArtifactChange`) as **optional enrichment**; Governance
  MAY ignore them and route from its own F016 snapshot facts.
- **Rationale**: F015 `Routing.route` consumes F014-normalized `GovernedPath`s,
  which Governance already senses via F016. SDD's references are cheap and make the
  work-item→path linkage explicit, but must not be presented as a selected route.
- **Alternatives considered**: omitting paths entirely — rejected, loses the
  artifact-level provenance tying changes to a work item; emitting a selected route
  — rejected, violates the boundary (SC-005).

## D5 — Emission point and command surface

- **Decision**: Emit the handoff from `ship` (the merge-boundary readiness stage
  that already points to the Governance handoff) and regenerate it from `refresh`,
  as a `GenerationManifest` view of a new kind `GovernanceHandoff`. No new command,
  no new lifecycle stage.
- **Rationale**: This matches how `work-model.json`/`summary.md` are produced and
  refreshed (proven manifest + currency machinery) and keeps `nextLifecycleCommand`
  unchanged. The projection is pure; only the already-MVU `ship`/`refresh` commands
  touch I/O (Constitution IV/V).
- **Alternatives considered**: a dedicated `fsgg-sdd handoff` command — rejected as
  unnecessary surface; emitting at every stage — rejected, the handoff is meaningful
  at merge-boundary readiness.

## D6 — Schema/version ownership and no Governance dependency

- **Decision**: SDD owns the schema (`schemaVersion = 1`, `contractVersion 1.0.0`)
  with no compile-time dependency on any FS.GG.Governance package; the cross-repo
  shape is versioned in `contracts/integration-requirements.md`, validated against
  Governance's published consumer types by inspection + mapping tests.
- **Rationale**: CLAUDE.md / constitution require the integration to be explicit,
  versioned, and optional, and SDD to remain buildable/usable without Governance.
  A package reference would break that.
- **Alternatives considered**: referencing `FS.GG.Governance.Kernel` for the
  `EvidenceState` type — rejected; it would make Governance a hard SDD dependency
  and invert the boundary.

## D7 — Supersession of advisory placeholders

- **Decision**: The handoff supersedes the advisory `GovernanceCompatibility`
  booleans (`RouteAware`/`ProfileAware`/`FreshnessAware`/`EnforceableBySdd`) and the
  per-command `GovernanceCompatibilityFact` by emitting the concrete facts they
  approximated; the placeholders are removed or reduced to a pointer to the handoff.
- **Rationale**: Two parallel Governance-facing surfaces would drift (Constitution
  VII). One concrete contract is the single source.
- **Alternatives considered**: keeping both — rejected for drift risk.
