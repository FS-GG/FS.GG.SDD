# Contract: `fsgg-sdd agents` Command

Owner: `FS.GG.SDD` (commands library + CLI host). Status: new in feature
`014-agent-guidance`.

## Command surface

- New `SddCommand.Agents` case in `CommandTypes`.
- `commandName Agents = "agents"`, `commandStage Agents = "agents"`,
  `parseCommand "agents" = Ok Agents`.
- `nextLifecycleCommand Agents = None` (cross-cutting generator, not a lifecycle
  stage); the existing charter->ship chain is unchanged.
- CLI dispatch in `Program.fs` accepts `agents` with the shared
  `--root`, `--work`, `--dry-run`, `--text`, and overwrite-policy options.

## Inputs

- `--root <path>`: project root (default `.`).
- `--work <id>`: required selected work id.
- `--dry-run`: plan and report without writing.
- `--text`: human-readable projection (default JSON).

## Reads (effects)

- `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- `readiness/<id>/work-model.json` (the derivation source)
- existing `readiness/<id>/agent-commands/<target>/guidance.json` for each
  configured target (for currency/rerun)
- `EnumerateDirectory "work"` for duplicate-id detection

The hand-owned `GuidancePath` files (`CLAUDE.md`/`AGENTS.md`) are **not** read as
derivation inputs.

## Writes (effects, non-dry-run only)

- `CreateDirectory` for each target's generated root when missing.
- `WriteFile(<generatedRoot>/guidance.json, …, AgentGuidanceTarget)`.
- `WriteFile(<generatedRoot>/commands.md, …, AgentGuidanceTarget)` and
  `WriteFile(<generatedRoot>/skills.md, …, AgentGuidanceTarget)`.
- No authored source, `.fsgg/agents.yml`, or `CLAUDE.md`/`AGENTS.md` write is
  ever planned. Those appear as `Preserve` artifact changes.
- Overwrite policy `AllowGeneratedRefresh`; refresh that would be unsafe is
  refused with a diagnostic (`Refuse`).

## MVU boundary

Reuses the existing `CommandModel`/`CommandMsg`/`CommandEffect`/`init`/`update`
plus edge interpreter. Pure transitions:

1. `LoadProject` — read and validate project/SDD/agents config.
2. `LoadWorkItem` — read the work model and existing per-target guidance; detect
   duplicate logical ids.
3. `ApplyUserIntent` — derive the `NormalizedGuidanceModel` from a current work
   model; render per-target `GeneratedAgentGuidance`; evaluate equivalence.
4. `PlanGeneratedViewRefresh` — classify each target's currency and plan
   generated writes (or dry-run proposals).
5. `EffectInterpreted` — fold real I/O results.
6. `BuildReport` — assemble the deterministic report.

All derivation and equivalence evaluation are pure and complete before any
generated `WriteFile`/`CreateDirectory` effect is interpreted.

## Outcomes and disposition

- `Succeeded` + disposition `generated-current`: config valid, work model
  current, every target generated or already current, equivalence satisfied or
  not required. Next action is advisory (regenerate on work-model change /
  continue lifecycle).
- `SucceededWithWarnings` + disposition `advisory`: only non-blocking warnings or
  optional Governance boundary facts.
- `NoChange`: rerun where every target is already current (no writes planned).
- `Blocked`: any blocking finding (see Diagnostics). No generated guidance is
  treated as current; next action names the correction.

## Diagnostics (blocking unless noted)

- outside SDD project / project not initialized
- missing `.fsgg/agents.yml`; malformed `.fsgg/agents.yml`; unsupported schema
- no configured targets (`no-targets`)
- missing / malformed / mismatched / duplicate work id
- missing / stale / malformed / blocked work model
- unknown source reference in the work model
- malformed existing generated guidance manifest
- stale generated guidance vs current work model (`stale` disposition unless
  refreshed in the same run)
- Claude/Codex behavior divergence while equivalence is required
- unsafe generated-view refresh
- optional Governance boundary issue (advisory only)

## No-Governance and optional boundary facts

The command runs fully without Governance. Optional Governance pointers in
SDD-owned sources are surfaced as advisory `GovernanceCompatibilityFact` entries
and never interpreted or enforced.
