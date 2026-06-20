# Artifact Traceability — Agent Guidance Generation

| Requirement | Implementation | Evidence |
|---|---|---|
| FR-001 native `agents` command, not a lifecycle stage | `CommandTypes.Agents`, `nextLifecycleCommand Agents = None` | prelude `next after agents`, `agents command stage` |
| FR-002 require init + work id + config + current work model | `computeAgentsPlan` precondition gates | `agents blocks on missing work model` |
| FR-003 load/validate config | `parseAgentGuidanceConfig` + `configDiags` | `agents.noTargets`, `agents.invalidGeneratedRoot` |
| FR-004 derive both targets from one model | `deriveGuidanceModel` (shared) | `agents derives equal behavior digest across claude and codex` |
| FR-005 generate/refresh under `agent-commands/` | per-target write effects | `agents generates per-target guidance...` |
| FR-006 mark generated + sources/digests/generator/target | `agentGuidanceManifestJson` | `agents marks generated manifests...` |
| FR-007 every configured target; report generated/refused | `generatedTargetIds`/`refusedTargetIds` | summary assertions |
| FR-008 stale generated guidance | `agentsStaleGeneratedGuidance` + currency compare | `agents-refreshes-stale` path |
| FR-009 equivalence guardrail | `equivalenceRequired` + `agentsBehaviorDivergence` | `agents blocks divergent existing guidance...` |
| FR-010/011 non-destructive; not a second source of truth | `GeneratedView` writes only; authored preserved | `agents preserves authored sources...`, sdd-governance-boundary.md |
| FR-012 diagnose work-model currency; never derive from bad model | work-model gate diagnostics | `agents blocks on malformed work model` |
| FR-013 disposition generated-current/stale/blocked/advisory | `disposition` computation | summary assertions |
| FR-014 block invalid contexts | base + target blocking diagnostics | blocked tests |
| FR-015 next action advisory vs correction | `nextAction` Agents branch | `agentsGenerated` / `correctBlockingDiagnostics` |
| FR-016 report shape | `AgentGuidanceSummary` + serializer | report JSON tests |
| FR-017 deterministic | deterministic serialization | `agents report JSON is deterministic`, byte-identical manifests |
| FR-018 text projection no extra facts | `renderText` agents block | human-summary-review.md |
| FR-019/020 diagnostics distinguish states, stable ids | `CommandReports.agents*` | prelude diagnostic ids |
| FR-021 dry-run no mutation | interpreter dry-run + plan | `agents dry-run writes zero files...` |
| FR-022/023/024 no Governance behavior | advisory facts only | `agents succeeds without governance installed`, sdd-governance-boundary.md |

| Success criterion | Evidence |
|---|---|
| SC-001 one-command generate + next action | `agents generates per-target guidance...` |
| SC-002 valid families succeed | command tests (create/rerun/preserve/refresh/dry-run/text/no-gov) |
| SC-003 blocked families leave authored content unchanged + diagnostic | missing/malformed/divergence/malformed-manifest tests |
| SC-004 three identical runs identical | determinism tests |
| SC-005 affected target/artifact identified before block | divergence/malformed/stale diagnostics name target |
| SC-006 dry-run 0 file changes | dry-run test |
| SC-007 summary surfaces targets/state/divergence/next action | text smoke |
| SC-008 every manifest traces to source + generator + marker | `agents marks generated manifests...` |
| SC-009 usable without Governance | no-governance test |

## Deferred / deviations (disclosed)

- Static fixture directories `tests/fixtures/lifecycle-commands/agents-*` (tasks
  T003–T005) were **not** authored. The repository's fixture YAML manifests are
  documentation pointers and are not consumed by a generic assertion runner
  (only `deterministic-report` is used as a CLI root). The scenarios those
  fixtures describe are instead exercised with **real** evidence by
  `AgentsCommandTests` over full disposable lifecycle projects. This is disclosed
  rather than marked complete.
- Generated guidance files (`guidance.json`, `commands.md`, `skills.md`) are
  written with the `GeneratedView` overwrite kind (refreshable) rather than the
  `AgentGuidanceTarget` kind named in the command contract, because
  `CommandEffects.canOverwrite` (correctly) refuses overwriting
  `AgentGuidanceTarget` files to protect the hand-owned `CLAUDE.md`/`AGENTS.md`.
  Semantics are unchanged: generated guidance is refreshable; hand-owned guidance
  targets stay protected and are only ever preserved.
