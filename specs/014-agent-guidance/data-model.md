# Data Model: Agent Guidance Generation

This feature reuses existing artifact-model and command contracts wherever
possible (see [research.md](research.md) R9). New types are limited to the
generated guidance manifest, the derived guidance model, the command summary,
and agent-guidance diagnostics.

## AgentsCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd agents`
- **Fields**: command (`Agents`), project root token, selected work id, output
  format, dry-run flag, overwrite policy, generator version
- **Validation**: Command is `agents`; work id is required and must satisfy the
  existing `WorkId` contract; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths are
  excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `AgentsCommandReport` invocation section. Reuses `CommandRequest`.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, agents config path,
  generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before generated writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the `AgentGuidanceConfig`, the work-model path, and
  optional Governance compatibility facts. Reuses existing project/SDD parsing.

## AgentGuidanceConfig *(existing)*

- **Source**: `.fsgg/agents.yml`, parsed by the existing
  `parseAgentGuidanceConfig`
- **Fields**: schema version, `Targets: AgentGuidanceTarget list`, work-model
  source path, `GeneratedGuidanceIsAuthority: bool`,
  `RequireEquivalentClaudeAndCodexBehavior: bool`
- **Validation**: Schema version must be current or accepted; at least one target
  is required (an empty target list blocks the command with a `no-targets`
  diagnostic); the work-model path and each target's generated root must be
  non-empty and resolve within the project.
- **Relationships**: Drives which targets are generated and whether the
  equivalence guardrail is enforced.

## AgentGuidanceTarget *(existing)*

- **Source**: An entry in `.fsgg/agents.yml` `agents:` list
- **Fields**: `Id` (e.g. `claude`, `codex`), `GuidancePath` (hand-owned authored
  guidance file such as `CLAUDE.md`/`AGENTS.md`, read-only context, never
  written), `GeneratedRoot` (e.g. `readiness/{workId}/agent-commands/claude`)
- **Validation**: Ids are unique and non-empty; the generated root resolves under
  the readiness tree for the selected work id; `GuidancePath` is treated as a
  hand-owned target and is preserved, never an input that turns guidance into a
  second source of truth.
- **Relationships**: Each target receives one rendered `GeneratedAgentGuidance`.

## NormalizedGuidanceModel *(new, derived, in-memory)*

- **Source**: Derived purely from `readiness/<id>/work-model.json`
- **Fields**: work id, lifecycle stage readiness, derived command entries (id,
  title, stage, purpose), derived skill entries (id, title, capability),
  requirement/decision/task/evidence references used by the guidance, and the
  set of source identities the derivation read
- **Validation**: Derivation only proceeds from a current, well-formed work
  model; entries are sorted by stable id; no presentation-only fields are
  included so the same model can drive every target.
- **Relationships**: Rendered once per target into a `GeneratedAgentGuidance`;
  its behavior model is the unit of Claude/Codex equivalence comparison.

## GeneratedAgentGuidance (per-target manifest) *(new)*

- **Source**: Generated `readiness/<id>/agent-commands/<target>/guidance.json`
- **Fields**: `SchemaVersion` (1), view version, work id, target id, generator
  identity, generated marker, `Sources: AnalysisSourceRecord list` (work model
  with digest/schema/status), `BehaviorModelDigest` (digest of the normalized
  behavior model used for equivalence), derived command entries, derived skill
  entries, rendered-file references (the Markdown projection paths), and
  diagnostics
- **Validation**: Must record source digests, schema version, and generator
  identity; presence alone is not proof of currency; on rerun it is compared
  against the current work model to classify currency.
- **Relationships**: One per configured target; its Markdown projection is the
  agent-facing guidance; its `BehaviorModelDigest` participates in the
  equivalence check.

## GeneratedGuidanceMarkdown (projection) *(new, generated)*

- **Source**: Rendered from `GeneratedAgentGuidance` under the target's generated
  root (e.g. `agent-commands/<target>/commands.md`, `.../skills.md`)
- **Fields**: human/agent-facing command and skill guidance text marked as
  generated with a source reference back to the manifest and work model
- **Validation**: Pure projection of the manifest; contains no facts absent from
  the manifest; deterministic for identical manifests.
- **Relationships**: Read by Claude/Codex; never an authored source of truth.

## GuidanceDisposition *(new)*

- **Source**: Computed from configuration validity, work-model currency,
  per-target generated-view state, and equivalence findings
- **Values**: `generated-current`, `stale`, `blocked`, `advisory`
- **Validation**: `generated-current` only when configuration parses, the work
  model is current, every configured target's guidance is generated or already
  current, and no unresolved equivalence divergence remains under a required
  equivalence policy.
- **Relationships**: Surfaced in `AgentGuidanceSummary` and drives the next
  action.

## EquivalenceObligation *(new)*

- **Source**: `RequireEquivalentClaudeAndCodexBehavior` in `.fsgg/agents.yml`
- **Fields**: required flag, compared targets, per-pair equivalence verdict and
  divergence finding ids
- **Validation**: When required, all configured targets' behavior models must
  match the shared derived model; a mismatch blocks `generated-current`.
- **Relationships**: Produces equivalence diagnostics and contributes to the
  disposition.

## AgentGuidanceFinding *(new)*

- **Source**: Configuration, work-model, generated-view, and equivalence
  evaluation
- **Fields**: stable id, severity, category, path, related ids (target id,
  source artifact, work-model record), message, correction
- **Validation**: Every blocking finding names the affected artifact or target,
  severity, explanation, and a user-correctable action.
- **Relationships**: Aggregated into the report and the per-target manifest
  diagnostics.

## AgentGuidanceSummary *(new report field)*

- **Source**: Result of the command workflow
- **Fields**: work id, stage (`agents`), status, generated-root list, generated
  target ids, refused target ids, finding ids, ready/advisory/warning/blocking
  counts, disposition, equivalence-required flag, divergent target ids,
  generated-view state, source snapshot count, readiness
- **Validation**: Deterministic; mirrors the authoritative report; the
  human-readable summary introduces no separate facts.
- **Relationships**: New `AgentGuidance: AgentGuidanceSummary option` field on
  `CommandReport` and `CommandModel`.

## GeneratedViewState *(existing, reused)*

- **Source**: Each target's generated guidance manifest plus the consumed work
  model
- **Fields**: path, kind (`agent-commands`, `work-model`), schema version,
  generator, sources, output digest, currency, diagnostic ids
- **Validation**: Currency classified as current/missing/stale/malformed/blocked.
- **Relationships**: Reported under `GeneratedViews` for each target manifest and
  for the consumed work model.

## NextAction *(existing, reused)*

- **Source**: Computed from the disposition
- **Fields**: action id, optional command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Behavior**: On `generated-current`, the next action is advisory (regenerate
  when the work model changes, or continue the lifecycle); `nextLifecycleCommand
  Agents = None` because agents is not a lifecycle stage. On a blocked
  disposition, the next action names configuration correction, work-model
  refresh, divergence resolution, or stale-source correction.

## State & disposition transitions

- **blocked**: outside project; missing/malformed `.fsgg/agents.yml`; no targets;
  missing/stale/malformed work model; malformed or mismatched work id; duplicate
  logical work id; unknown reference; malformed generated guidance; unresolved
  Claude/Codex divergence under a required equivalence policy; unsafe refresh.
- **stale**: a target's generated guidance no longer matches the current work
  model's source digests, schema version, or generator identity.
- **advisory**: optional Governance boundary facts or non-blocking warnings only.
- **generated-current**: configuration valid, work model current, all targets
  generated or current, equivalence satisfied (or not required).
