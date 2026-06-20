# Phase 0 Research: Agent Guidance Generation

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION`
markers remain.

## R1. Command surface name and lifecycle position

- **Decision**: Introduce the command as `fsgg-sdd agents`, added as a new
  `SddCommand.Agents` case. It is a cross-cutting generator, not a lifecycle
  authoring stage: `commandStage Agents = "agents"` and
  `nextLifecycleCommand Agents = None`, and the charter->ship stage chain is left
  unchanged (`nextLifecycleCommand Ship = None`).
- **Rationale**: The authored `.fsgg/agents.yml` config and the
  `readiness/<id>/agent-commands/` generated view already use "agents"/
  "agent-commands" vocabulary, so `agents` is the least surprising verb. Phase 8
  of `docs/initial-implementation-plan.md` describes agent guidance as a
  generator over the normalized lifecycle model that can run whenever a current
  work model exists, not as a stage between `ship` and a successor.
- **Alternatives considered**: `generate-agents` (more explicit but inconsistent
  with the single-word command family); folding generation into `fsgg refresh`
  (rejected — refresh is a separate Phase 7 shared concern and would conflate
  multi-view refresh with agent-specific derivation and equivalence checks);
  adding it as a lifecycle stage after `ship` (rejected — guidance is not a
  merge-boundary step and need not gate the lifecycle).

## R2. Generated guidance representation (structured contract vs Markdown)

- **Decision**: For each configured target, the generated view under that
  target's `generatedRoot` is a structured manifest `guidance.json`
  (`schemaVersion: 1`) plus a Markdown projection rendered from the manifest. The
  manifest is the machine contract; currency and equivalence are evaluated over
  the manifest, and Markdown is presentation only.
- **Rationale**: Constitution II/VII require schema-versioned structured
  artifacts as the machine contract while Markdown stays an authoring/projection
  surface. Agents consume Markdown command/skill guidance, but tools (currency,
  equivalence, determinism) must key on a structured artifact with source digests
  and generator identity. The pattern mirrors `summary.md` being a projection of
  structured readiness JSON.
- **Alternatives considered**: Markdown-only generated guidance (rejected —
  no stable structured contract for currency/equivalence and risks Markdown
  drift); JSON-only with no agent-facing Markdown (rejected — defeats the purpose
  of agent guidance, which agents read as Markdown).

## R3. Deriving Claude and Codex guidance from one model

- **Decision**: Derive a single normalized guidance model from the selected work
  item's `readiness/<id>/work-model.json` (lifecycle stages, requirements,
  decisions, tasks, evidence obligations, generated-view state), then render that
  shared model once per configured target. Both targets are produced from the
  same derived model, differing only in per-agent rendering (file naming,
  heading/front-matter conventions), not in described workflow behavior.
- **Rationale**: Constitution VII requires Claude and Codex to operate over the
  same contract. Deriving both from one model makes behavioral equivalence true
  by construction and keeps the generator from becoming a second source of truth.
- **Alternatives considered**: Independent per-target derivation (rejected —
  reintroduces drift the equivalence rule is meant to prevent); reading existing
  authored `CLAUDE.md`/`AGENTS.md` as guidance input (rejected — those are
  hand-owned targets, not lifecycle sources, and must not become inputs that turn
  guidance into a second source of truth).

## R4. Claude/Codex behavior equivalence guardrail

- **Decision**: When `requireEquivalentClaudeAndCodexBehavior` is set in
  `.fsgg/agents.yml`, evaluate equivalence by comparing the normalized
  behavior model embedded in each target's manifest (the derived command/skill
  set and lifecycle facts, excluding presentation-only fields). Divergence — for
  example, an existing stale target manifest whose behavior model no longer
  matches the shared derived model, or a configuration that would render
  different behavior across targets — emits an equivalence diagnostic and blocks a
  `generated-current` disposition until resolved.
- **Rationale**: Because both targets are derived from one model, a freshly
  generated pair is always equivalent; the guardrail exists to catch
  pre-existing or externally-edited generated guidance that has diverged, which
  Constitution VII and VIII require to be a visible finding rather than a silent
  pass.
- **Alternatives considered**: Skipping equivalence when the config requires it
  (rejected — violates the configured obligation); byte-equality of rendered
  Markdown across targets (rejected — targets legitimately differ in presentation;
  only the behavior model must match).

## R5. Generated-view currency and stale detection

- **Decision**: Reuse the established source-digest currency model. Each
  target manifest records its source identities (work-model path, digest, schema
  version, generator identity). On rerun, compare the manifest's recorded source
  digests and generator identity against the current work model via the existing
  `GenerationManifest`/`isStale` machinery; classify each target as current,
  missing, stale, malformed, or blocked. A missing, stale, malformed, or blocked
  work model blocks generation outright (no derivation from an unusable model).
- **Rationale**: Consistent with `analyze`/`verify`/`ship` generated-view
  currency and Constitution II ("presence is not proof of currency").
- **Alternatives considered**: Timestamp-based staleness (rejected —
  non-deterministic, violates the no-implicit-clock constraint); regenerating
  unconditionally every run (rejected — loses the rerun `noChange` signal and
  determinism evidence).

## R6. Non-destructive writes and dry-run

- **Decision**: The command authors no source. Generated writes target only each
  configured target's generated root (manifest + Markdown projection) under
  `AllowGeneratedRefresh` with `ArtifactWriteKind.AgentGuidanceTarget`. Authored
  lifecycle artifacts, `.fsgg/agents.yml`, and the hand-owned
  `CLAUDE.md`/`AGENTS.md` files are read-only and reported as `Preserve`.
  Dry-run plans and reports proposed generated changes without emitting
  `WriteFile`/`CreateDirectory` effects.
- **Rationale**: Matches the non-destructive contract of the other generated-view
  commands and Constitution VII (guidance is a projection, never a second source
  of truth); the `AgentGuidanceTarget` write kind already exists for exactly this
  purpose.
- **Alternatives considered**: Writing into the repository's top-level
  `CLAUDE.md`/`AGENTS.md` (rejected — those are authored, hand-owned targets;
  generated guidance belongs under `readiness/<id>/agent-commands/<target>/`).

## R7. Determinism

- **Decision**: Sort targets by configured id, sort derived command/skill and
  lifecycle entries by stable id, exclude wall-clock timestamps, durations,
  absolute host paths, terminal width, ANSI styling, directory enumeration order,
  host path separators, and random values from manifests and reports. Three
  identical dry-run executions over the `deterministic-report` fixture must
  produce byte-identical manifests and JSON reports.
- **Rationale**: Constitution II/VI and the established deterministic-JSON
  constraint shared by every generated view in the repo.
- **Alternatives considered**: None; determinism is a hard repository constraint.

## R8. No-Governance operation and optional boundary facts

- **Decision**: The command runs fully without Governance installed. Optional
  Governance pointers present in SDD-owned sources are surfaced as advisory
  `GovernanceCompatibilityFact` entries and are never interpreted or enforced.
- **Rationale**: Constitution engineering constraints require SDD to remain
  useful without Governance; this matches `analyze`/`verify`/`ship`.
- **Alternatives considered**: None.

## R9. Reuse of existing contracts

- **Decision**: Reuse `AgentGuidanceConfig`, `AgentGuidanceTarget`,
  `parseAgentGuidanceConfig`, `GeneratedViewKind.AgentCommands`,
  `ArtifactWriteKind.AgentGuidanceTarget`, `SourceIdentity`,
  `GenerationManifest`/`isStale`, `AnalysisSourceRecord`,
  `AnalysisGeneratedViewRecord`, `AnalysisOptionalBoundaryFact`, the
  `CommandReport`/`CommandModel`/`GeneratedViewState` shapes, and the existing
  serialization/rendering helpers. New surface is limited to the generated
  guidance manifest types, a derived guidance model, an `AgentGuidanceSummary`,
  agent-guidance diagnostics, and the `SddCommand.Agents` case.
- **Rationale**: Minimizes new public surface and keeps the feature idiomatic and
  consistent with the existing generated-view commands (Constitution III/IV).
- **Alternatives considered**: New parallel types for sources/views (rejected —
  unnecessary duplication of stable contracts).
