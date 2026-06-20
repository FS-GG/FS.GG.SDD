# Phase 0 Research: Generated-View Refresh

This document resolves the planning unknowns for `fsgg-sdd refresh`. All
decisions reuse the existing artifact-model, command-workflow, and fixture
infrastructure established by the `charter -> ship` lifecycle and the `agents`
generator. No `NEEDS CLARIFICATION` markers remain.

## Decision 1: Command identity and lifecycle position

- **Decision**: Add `refresh` as a new `SddCommand` union case that is
  cross-cutting, not a lifecycle authoring stage. `commandName Refresh =
  "refresh"`, `commandStage Refresh = "refresh"`, `parseCommand "refresh" = Ok
  Refresh`, and `nextLifecycleCommand Refresh = None`. The charter->ship stage
  chain is unchanged (`Ship -> None`).
- **Rationale**: FR-001 requires a native command that does not introduce a new
  lifecycle authoring stage between existing stages. `agents` already
  established the cross-cutting precedent (`Agents -> None`); `refresh` follows
  it. Refresh consumes and regenerates derived views; it authors nothing.
- **Alternatives considered**: A per-view `--refresh` flag on each existing
  command — rejected because the spec requires one command that brings the
  *full set* of generated views into agreement together (US1, FR-003) and
  detects cross-view staleness and blocked-upstream dependencies (US2, FR-011),
  which a single-view flag cannot express.

## Decision 2: Reuse existing per-view generators

- **Decision**: `refresh` invokes the same deterministic generators the
  lifecycle and `agents` commands already use to produce `work-model.json`,
  `analysis.json`, `verify.json`, `ship.json`, and `agent-commands/<target>/`,
  rather than reimplementing generation. Refresh orchestrates them in declared
  source-of order and collects their per-view currency and diagnostics.
- **Rationale**: FR-003 mandates "the same deterministic generators that
  originally produce them", and the spec Assumptions state this feature "adds
  the command that refreshes those views together ... rather than introducing
  new generated-view contracts." Reuse guarantees byte-identical outputs to the
  owning commands (FR-018, SC-004) and avoids a second generation path that
  could drift.
- **Alternatives considered**: A standalone refresh generator per view —
  rejected as a second source of generation logic that violates the
  single-contract principle and would require parallel fixture maintenance.

## Decision 3: Source-of dependency ordering

- **Decision**: Refresh brings an upstream generated view to currency before any
  view that declares it as a source. The normalized work model is refreshed
  first (it is the declared source of analysis, verify, ship, agent guidance,
  and summary); the summary is refreshed last (it projects the structured
  readiness data). When an upstream view cannot be brought to currency, every
  dependent view is reported `Blocked` with the upstream view named (FR-011,
  US2 scenario 3).
- **Rationale**: FR-004 requires regenerating each view from current declared
  sources "except where one generated view is the declared source of another",
  and the Assumptions state refresh "brings the upstream view to currency
  first." `GenerationManifest.Sources` already records source relationships and
  digests, so ordering is derived from declared sources, not hard-coded beyond
  the known work-model-first / summary-last invariants.
- **Alternatives considered**: Independent parallel refresh of all views —
  rejected because dependent views would be regenerated from a stale or blocked
  upstream view, fabricating currency the spec forbids (FR-010).

## Decision 4: `summary.md` as a generated projection

- **Decision**: Render `readiness/<id>/summary.md` from the structured readiness
  data using `GeneratedViewKind.Summary` (already defined in
  `GenerationManifest`, value `"summary"`). The file carries a generation
  manifest header (schema version 1, generator identity, source relationships,
  source digests) and presents lifecycle state, per-view generated-view
  currency, diagnostics, outcome, and next action. The renderer reads only the
  structured views/report and introduces no independent facts (FR-005, FR-006).
- **Rationale**: US3 and FR-005/FR-006 require a human-readable projection that
  is marked generated, records its sources, and adds no fact absent from the
  structured views. The `Summary` kind already exists, so no new generated-view
  contract is introduced — only its renderer and manifest header. This mirrors
  how `agents` renders `commands.md`/`skills.md` from its `guidance.json`
  manifest.
- **Alternatives considered**: A hand-authored summary — rejected; it would
  become a competing source of truth (Constitution VII). Embedding the summary
  in the report JSON only — rejected; the spec requires a readable Markdown file
  at `readiness/<id>/summary.md`.

## Decision 5: Cross-view currency model

- **Decision**: Reuse the existing `GeneratedViewState` /
  `GeneratedViewCurrency` (`Current | Missing | Stale | Malformed | Blocked`)
  per view, populated via the existing `GenerationManifest.isStale` source-digest
  comparison and schema-status checks. Add a refresh-level disposition
  (`refreshed-current | partially-blocked | blocked`) mapping per-view states to
  the work-item outcome (FR-014).
- **Rationale**: FR-008/FR-009 require distinguishing current/missing/stale/
  malformed/blocked and naming the affected view and source. The existing state
  model already covers these; refresh adds the aggregate disposition. Staleness
  is detected by source and generator digests, not by file presence (spec
  Assumptions; Phase 7 exit criteria).
- **Alternatives considered**: A new currency enum — rejected as redundant with
  the established contract used by `analyze`/`verify`/`ship`/`agents`.

## Decision 6: Diagnostics and report shape

- **Decision**: Reuse shared diagnostics where they fit (`outsideProject`,
  `malformedWorkId`, `duplicateWorkId`, `unknownSourceReference`,
  `malformedGeneratedView`, `blockedGeneratedViewRefresh`) and add
  refresh-specific diagnostics for a missing/blocked source per view, a blocked
  upstream view naming the upstream to correct first, and an unrenderable
  summary. Add a `Refresh: RefreshSummary option` field to `CommandReport` and
  populate the existing `GeneratedViews` list with one `GeneratedViewState` per
  SDD-owned view. Diagnostics carry stable ids, affected view, affected source
  or upstream view, severity, explanation, and a correctable action (FR-020).
- **Rationale**: FR-017 requires the report to name refreshed/already-current/
  blocked views, preserved authored artifacts, per-view state, diagnostics,
  outcome, and next action. The `CommandReport` already carries
  `ChangedArtifacts`, `GeneratedViews`, `Diagnostics`,
  `GovernanceCompatibility`, and `NextAction`; a `Refresh` summary record
  carries the aggregate facts, paralleling `Ship`/`AgentGuidance` summaries.
- **Alternatives considered**: A bespoke refresh report type — rejected; it
  would fork the report contract that JSON serialization, text rendering, and
  exit-code logic already share across commands.

## Decision 7: Non-destructive, dry-run, deterministic behavior

- **Decision**: Refresh writes only generated views under their configured
  generated roots and never creates/updates/reorders/normalizes/removes authored
  sources or `.fsgg/*.yml` (FR-012). Dry run plans the same effects but emits no
  `WriteFile`/`CreateDirectory` mutations, reporting proposed changes only
  (FR-021). All outputs exclude clocks, durations, ANSI styling, enumeration
  order, host path separators, randomness, and absolute host paths (FR-018).
- **Rationale**: US4/US5 and the constitution require authored artifacts remain
  authoritative and refresh be safe to repeat. The existing
  `OverwritePolicy`/`DryRun`/effect-interpreter machinery already enforces this
  for other generated-view commands; refresh reuses it.
- **Alternatives considered**: In-place authored normalization during refresh —
  rejected outright by FR-012/FR-013.

## Decision 8: Agent-commands applicability and Governance independence

- **Decision**: The `agent-commands/` view is refreshed only when an
  agent-guidance configuration and at least one configured target exist;
  otherwise it is reported "not applicable" rather than blocked (spec
  Assumptions, edge case). Governance is never required; optional Governance
  pointers present in SDD-owned sources are surfaced as advisory
  `GovernanceCompatibilityFact`s and never interpreted as enforcement (FR-022,
  FR-023, FR-024).
- **Rationale**: SDD must remain usable without Governance (Constitution;
  SC-009). Stale-view blocking at a protected boundary stays Governance-owned.
- **Alternatives considered**: Treating missing agent config as a blocking
  finding — rejected; it is a valid no-op for projects without agent targets.

## Summary of resolved unknowns

| Unknown | Resolution |
|---|---|
| New command vs. flag | New cross-cutting `Refresh` case, `nextLifecycleCommand = None` |
| Generation logic | Reuse existing per-view generators |
| View ordering | Source-of order; work model first, summary last; block dependents on blocked upstream |
| `summary.md` contract | Existing `GeneratedViewKind.Summary`, manifest header, projection-only |
| Currency model | Existing `GeneratedViewState`/`GeneratedViewCurrency` + aggregate disposition |
| Report shape | Reuse `CommandReport`; add `Refresh` summary + refresh diagnostics |
| Safety/determinism | Existing overwrite/dry-run/effect machinery; deterministic serialization |
| Agent targets / Governance | Not-applicable when no targets; Governance advisory-only, never required |
